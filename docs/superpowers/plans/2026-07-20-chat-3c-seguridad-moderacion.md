# Plan TDD — Chat 3C seguridad y moderación

Spec: `docs/superpowers/specs/2026-07-20-chat-3c-seguridad-moderacion-design.md`

## Reglas de ejecución

- Ejecutar comandos backend desde `CaseritoApp/` y frontend desde `web/`.
- Una tarea y un test rojo a la vez; no avanzar sin observar el fallo esperado.
- No registrar ni incluir en excepciones texto, payloads, tokens, IDs sensibles,
  participantes, grupos, claves de idempotencia o argumentos recibidos.
- Mantener `404` indistinguible para recursos de participante inexistentes o no
  autorizados y errores genéricos de SignalR.
- No añadir notificaciones, badges, presencia, escritura, adjuntos, edición o
  eliminación de mensajes.

## 1. Estados de conversación en el dominio

**Entradas:** agregado `Conversacion` y reglas aprobadas de cierre.

**Prueba roja:** ampliar
`tests/CaseritoApp.UnitTests/Chat/ConversacionTests.cs` para demostrar:

- estado inicial `Activa`;
- cierre/reapertura de participante idempotentes;
- un participante no revierte `CerradaPorModeracion`;
- cierre/reapertura de moderación idempotentes;
- `CrearMensaje` falla con el mismo error público cuando no está activa.

**Implementación:**

- crear `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/EstadoConversacion.cs`;
- ampliar `Conversacion.cs`, `ErroresConversacion.cs` y
  `EventosConversacion.cs` con estado, origen/fecha de cierre, transiciones y
  eventos sin contenido ni actores;
- exponer métodos separados para participante y moderación para que Application
  no tenga que inferir permisos.

**Salida:** agregado capaz de imponer el ciclo de vida sin dependencias externas.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~ConversacionTests
```

Rojo esperado: miembros/estados inexistentes. Verde esperado: todas las
transiciones e idempotencias pasan.

**Commit previsto:** `feat(chat): modela cierre de conversaciones`

## 2. Bloqueo dirigido entre usuarios

**Entradas:** pareja comprador/vendedor de una conversación.

**Prueba roja:** crear
`tests/CaseritoApp.UnitTests/Chat/BloqueoUsuarioTests.cs` para dirección,
identificadores distintos e idempotencia a nivel de handler; ampliar
`IniciarConversacionCommandHandlerTests.cs` y
`EnviarMensajeCommandHandlerTests.cs` para bloqueo en cualquiera de las dos
direcciones.

**Implementación:**

- crear `Domain/Seguridad/BloqueoUsuario.cs`;
- crear `Application/Seguridad/IRepositorioBloqueosUsuario.cs` con
  `ExisteEntreAsync(usuarioA, usuarioB)`, `ObtenerAsync(bloqueador, bloqueado)`,
  `Agregar` y `Quitar`;
- crear `Application/Seguridad/BloquearUsuarioCommand.cs` y
  `DesbloquearUsuarioCommand.cs`; ambos reciben conversación y actor, derivan la
  contraparte del agregado y son idempotentes;
- inyectar el puerto en `IniciarConversacionCommand.cs` y
  `EnviarMensajeCommand.cs`; cualquier bloqueo entre la pareja produce
  `chat_conversacion_no_disponible_para_envio` sin revelar dirección.

**Salida:** ningún caller puede elegir un usuario objetivo arbitrario y el
bloqueo afecta inicio y envío globalmente.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~BloqueoUsuarioTests|FullyQualifiedName~IniciarConversacionCommandHandlerTests|FullyQualifiedName~EnviarMensajeCommandHandlerTests"
```

Rojo esperado: handlers/puerto inexistentes. Verde esperado: bloqueo propio,
inverso e idempotencia cubiertos.

**Commit previsto:** `feat(chat): aplica bloqueo global entre participantes`

## 3. Reportes y registros de moderación en dominio

**Entradas:** objetivos y workflow aprobados.

**Prueba roja:** crear
`tests/CaseritoApp.UnitTests/Chat/ReporteChatTests.cs` y
`RegistroModeracionChatTests.cs` para categorías, tipos de objetivo, transición
`Pendiente → EnRevision → Atendido/Descartado`, liberación, asignación exclusiva
y registro sin contenido.

**Implementación:** crear en `Domain/Moderacion/`:

- `TipoObjetivoReporteChat.cs`, `CategoriaReporteChat.cs` y
  `EstadoReporteChat.cs`;
- `ReporteChat.cs`, que guarda una única referencia discriminada, detalle de
  hasta 1000 caracteres y moderador asignado;
- `AccionModeracionChat.cs` y `RegistroModeracionChat.cs`, append-only y sin
  campos de contenido o participantes.

Las fábricas validan combinaciones: mensaje exige `MensajeId`; los otros tipos
lo prohíben; el objetivo participante se deriva y no se persiste desde un
argumento cliente.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~ReporteChatTests|FullyQualifiedName~RegistroModeracionChatTests"
```

Rojo esperado: tipos inexistentes. Verde esperado: máquina de estados completa.

**Commit previsto:** `feat(chat): modela reportes y auditoria de moderacion`

## 4. Persistencia SQL Server y migración

**Entradas:** entidades de tareas 1–3.

**Prueba roja:** ampliar
`tests/CaseritoApp.IntegrationTests/ChatPersistenciaTests.cs` para columnas y
default `Activa`, token de concurrencia, índices de bloqueos, unicidad filtrada de
reportes abiertos y registros append-only.

**Implementación:**

- ampliar `Infrastructure/ChatDbContext.cs` con `DbSet` nuevos;
- ampliar `Infrastructure/Conversaciones/ConfiguracionChat.cs` y crear
  `Infrastructure/Seguridad/ConfiguracionSeguridadChat.cs` y
  `Infrastructure/Moderacion/ConfiguracionModeracionChat.cs`;
- crear repositorios EF en esos dos directorios e inscribirlos en
  `Infrastructure/DependencyInjection.cs`;
- generar `ChatSeguridadModeracion` con
  `dotnet ef migrations add ChatSeguridadModeracion --project src/Chat/CaseritoApp.Chat.Infrastructure --startup-project src/Host/CaseritoApp.Host --context ChatDbContext`;
- revisar la migración generada y snapshot: schema `chat`, sin FK externas,
  defaults compatibles con datos 3A/3B.

**Salida:** modelo persistente concurrente y migrable.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~ChatPersistenciaTests
```

Rojo esperado: metadatos/tablas ausentes. Verde esperado: modelo e índices
verificados contra SQL Server de Testcontainers.

**Commit previsto:** `feat(chat): persiste seguridad y moderacion`

## 5. Comandos de participantes y proyecciones de acceso

**Entradas:** dominio, repositorios y restricciones de bloqueo.

**Prueba roja:** crear
`tests/CaseritoApp.UnitTests/Chat/SeguridadConversacionHandlerTests.cs` y ampliar
`ConsultasChatHandlerTests.cs` y `MarcarLecturaCommandHandlerTests.cs` para:

- bloquear/desbloquear y cerrar/reabrir solo como participante;
- impedir cierre/reapertura cuando hay bloqueo;
- conservar listado, mensajes y marcado de lectura con bloqueo/cierre;
- proyectar `Estado`, `OrigenCierre` y `PuedeEnviar`, sin dirección del bloqueo;
- devolver el mismo no-encontrado para inexistente y ajena.

**Implementación:**

- crear `Application/Seguridad/CerrarConversacionCommand.cs` y
  `ReabrirConversacionCommand.cs`;
- completar los handlers de bloqueo de la tarea 2;
- ampliar `ConversacionDto`, `ConversacionResumenDto`,
  `IConsultaConversaciones` y `ConsultaConversacionesEfCore` para estado y
  elegibilidad calculada;
- mantener `ObtenerMensajesQuery` y `MarcarLecturaCommand` autorizados por
  participación, no por capacidad de envío.

**Salida:** API de Application completa para participantes.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter "FullyQualifiedName~SeguridadConversacionHandlerTests|FullyQualifiedName~ConsultasChatHandlerTests|FullyQualifiedName~MarcarLecturaCommandHandlerTests"
```

Rojo esperado: comandos/campos faltantes. Verde esperado: matriz de permisos y
lectura histórica cubierta.

**Commit previsto:** `feat(chat): expone seguridad de conversaciones`

## 6. Creación y workflow de reportes en Application

**Entradas:** entidades y persistencia de moderación.

**Prueba roja:** crear
`tests/CaseritoApp.UnitTests/Chat/ModeracionChatHandlerTests.cs` con dobles de
repositorios para validar pertenencia, mensaje dentro de conversación,
contraparte derivada, duplicados, toma concurrente, moderador asignado y cierre
atómico al atender.

**Implementación:** crear `Application/Moderacion/` con:

- `IRepositorioReportesChat`, `IRepositorioRegistrosModeracionChat` y
  `IConsultaModeracionChat`;
- `ReportarChatCommand` y validador de combinaciones/categorías;
- `ListarReportesChatQuery`, `TomarReporteChatCommand`,
  `LiberarReporteChatCommand`, `AtenderReporteChatCommand`,
  `DescartarReporteChatCommand`, `CerrarPorModeracionCommand` y
  `ReabrirPorModeracionCommand`;
- DTO de cola sin contenido/participantes y DTO de evidencia separado;
- `ObtenerEvidenciaReporteChatQuery`, que primero añade y confirma
  `ConsultarEvidencia` mediante un puerto de auditoría y solo entonces consulta
  detalle y ventana acotada.

El servicio de evidencia devuelve roles relativos y nunca IDs de usuario. Una
auditoría fallida produce fallo genérico sin contenido.

**Salida:** workflow transaccional y evidencia condicionada a auditoría.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.UnitTests --filter FullyQualifiedName~ModeracionChatHandlerTests
```

Rojo esperado: casos de uso inexistentes. Verde esperado: permisos de dominio,
asignación y auditoría pasan.

**Commit previsto:** `feat(chat): implementa workflow de reportes`

## 7. Contratos HTTP y autorización Host

**Entradas:** casos de uso de participantes y moderadores.

**Prueba roja:** ampliar `tests/CaseritoApp.IntegrationTests/ChatFlujoTests.cs` y
crear `ModeracionChatTests.cs` para rutas, autenticación, permiso
`chat.moderar`, `404` indistinguible, `409` genérico y cuerpos mínimos.

**Implementación:**

- ampliar `Host/Endpoints/ChatEndpoints.cs` con requests y rutas de bloqueo,
  cierre y reporte definidas por el spec;
- crear `Host/Endpoints/ModeracionChatEndpoints.cs` bajo
  `/api/admin/moderacion/chat`, protegido con
  `PoliticasAutorizacion.Permiso(Permisos.ChatModerar)`;
- mapearlo en `Host/Program.cs` y aplicar límites `chat-consultas` a lectura y
  una policy específica de baja frecuencia para reportes/acciones;
- no aceptar usuario objetivo ni reflejar argumentos en ProblemDetails;
- conservar `401`, `403` administrativo, `404` indistinguible y `429`.

**Salida:** OpenAPI describe todos los contratos 3C.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~ChatFlujoTests|FullyQualifiedName~ModeracionChatTests"
```

Rojo esperado: `404` de rutas/campos inexistentes. Verde esperado: matriz HTTP
completa.

**Commit previsto:** `feat(chat): publica endpoints de seguridad y moderacion`

## 8. Revocación efectiva en SignalR

**Entradas:** cambios confirmados de bloqueo/cierre y outbox 3B.

**Prueba roja:** ampliar
`tests/CaseritoApp.IntegrationTests/ChatTiempoRealTests.cs` y
`ChatDespachadorTiempoRealTests.cs` para múltiples conexiones, retiro de grupos,
limpieza al desconectar, suscripción rechazada, carrera con entrega pendiente y
nueva suscripción obligatoria tras desbloquear/reabrir.

**Implementación:**

- crear `Host/Chat/RegistroConexionesChat.cs`, singleton thread-safe que registra
  solo en memoria `(usuario, conversación, connectionId)` y nunca los loguea;
- ampliar `ChatHub.cs` y `EstadoSuscripcionesChat.cs` para registrar/quitar en
  suscripción, desuscripción y desconexión;
- crear `Host/Chat/IRevocadorTiempoRealChat.cs` y
  `RevocadorTiempoRealChat.cs`, que usa `IHubContext<ChatHub>` para retirar todas
  las conexiones de los grupos afectados después del commit;
- crear una notificación de Application sin datos de participantes que active la
  revocación consultando los afectados en servidor;
- ampliar la consulta de `PuedeAccederConversacionQuery` o crear
  `PuedeRecibirTiempoRealQuery`: exige participante, `Activa` y sin bloqueos;
- hacer que `ChatHub.SuscribirConversacion` use esa elegibilidad;
- hacer que `DespachadorEntregasTiempoReal` compruebe elegibilidad antes de
  `PublicadorSignalRMensajes`; si fue revocada, marca la entrega procesada sin
  emitir contenido.

**Salida:** retirar grupos es inmediato y la comprobación previa a publicación
elimina la ventana de fuga aun si el registro efímero está obsoleto.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~ChatTiempoRealTests|FullyQualifiedName~ChatDespachadorTiempoRealTests"
```

Rojo esperado: conexiones siguen recibiendo. Verde esperado: cero eventos tras
revocación y recuperación HTTP intacta.

**Commit previsto:** `feat(chat): revoca acceso de tiempo real`

## 9. Evidencia, concurrencia y flujo integral SQL Server

**Entradas:** endpoints y tiempo real completos.

**Prueba roja:** completar `ModeracionChatTests.cs` y crear
`ChatSeguridadConcurrenciaTests.cs` para:

- bloqueo que afecta varias conversaciones y nuevo inicio;
- carreras bloquear/enviar y cerrar/enviar, con un único resultado válido;
- unicidad de reporte abierto y toma por un solo moderador;
- evidencia de mensaje con 5 anteriores/5 posteriores y conversación con 10
  últimos, sin IDs de participantes;
- imposibilidad de recibir evidencia cuando falla la auditoría;
- atender con cierre en una transacción y descartar sin cierre.

**Implementación:** ajustar consultas SQL, índices, tokens de concurrencia y el
reintento de `ChatEndpoints` solo donde los tests demuestren la necesidad. No
ensanchar ventanas ni devolver texto en la cola.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~ModeracionChatTests|FullyQualifiedName~ChatSeguridadConcurrenciaTests"
```

Rojo esperado: carrera, ventana o atomicidad incorrecta. Verde esperado: todos
los invariantes sobreviven ejecución real con SQL Server.

**Commit previsto:** `test(chat): cubre seguridad y moderacion integrales`

## 10. Guardas arquitectónicas y anti-PII

**Entradas:** implementación backend completa.

**Prueba roja:** ampliar `tests/CaseritoApp.ArchitectureTests/Chat/ChatPiiTests.cs`
y `ChatTiempoRealArchitectureTests.cs`; crear
`ChatModeracionArchitectureTests.cs` para detectar:

- campos de contenido/participante en DTO de cola y registros de auditoría;
- plantillas de log con mensajes, reportes, payloads, tokens, IDs, grupos,
  idempotencia o argumentos;
- eventos de revocación con participantes o contenido;
- acceso de moderación sin `chat.moderar`;
- publicación SignalR sin consulta de elegibilidad;
- dependencias de Domain/Application hacia Host, Infrastructure, Identity o
  Catalog.

**Implementación:** corregir únicamente hallazgos de las guardas; reutilizar
`LoggingBehavior` sin serializar requests. No añadir métricas de alta
cardinalidad.

**Verificación:**

```powershell
dotnet test tests/CaseritoApp.ArchitectureTests --filter FullyQualifiedName~Chat
```

Rojo esperado: nuevas superficies aún no protegidas. Verde esperado: reglas de
capas, autorización y anti-PII pasan.

**Commit previsto:** `test(chat): blinda moderacion contra pii y acoplamiento`

## 11. Cliente OpenAPI y adaptadores tipados

**Entradas:** OpenAPI backend estable. No existe aún una pantalla de conversación
de participantes, por lo que no se crea en 3C.

**Prueba roja:** ampliar `web/src/chat/tiempoReal.test.ts` para resuscripción
explícita tras revocación; crear `web/src/api/chat.test.ts` y
`web/src/api/moderacionChat.test.ts` para serialización tipada, conversión de
secuencias con `Number()` y ausencia de usuario objetivo.

**Implementación:**

- ejecutar `npm run generate:api` y actualizar `web/src/api/schema.d.ts`;
- ampliar `web/src/api/chat.ts` con cierre, bloqueo, reporte y campos
  `estado/origenCierre/puedeEnviar` usando exclusivamente tipos generados;
- crear `web/src/api/moderacionChat.ts` para cola, toma, evidencia y resolución;
- adaptar `web/src/chat/tiempoReal.ts` para tratar una revocación/desuscripción
  como estado que requiere suscripción explícita, sin notificaciones ni badges.

**Salida:** contratos web consumibles, sin `any` ni UI de participante fuera de
alcance.

**Verificación:**

```powershell
npm run test -- --run src/api/chat.test.ts src/api/moderacionChat.test.ts src/chat/tiempoReal.test.ts
npm run typecheck
```

Rojo esperado: paths/tipos faltantes. Verde esperado: cliente generado y
adaptadores estrictos.

**Commit previsto:** `feat(web): prepara cliente para seguridad de chat`

## 12. UI administrativa mínima

**Entradas:** cliente `moderacionChat` y patrón actual de moderación de avisos.

**Prueba roja:** crear `web/src/routes/AdminModeracionChatPage.test.tsx` y ampliar
`web/src/app/AppLayout.test.tsx` y tests del router para permiso, cola sin
contenido, carga bajo demanda de evidencia, acciones y limpieza de evidencia al
cerrar el diálogo.

**Implementación:**

- crear `web/src/routes/AdminModeracionChatPage.tsx` con TanStack Query y MUI;
- añadir ruta protegida con `RequierePermiso permiso="chat.moderar"` en
  `web/src/app/router.tsx`;
- añadir enlace separado `Moderación de chat` en `AppLayout.tsx` solo con ese
  permiso;
- mostrar categorías/estados en español, sin IDs técnicos ni contenido en la
  lista; solicitar evidencia al abrir y eliminarla del estado/cache inmediato al
  cerrar;
- ofrecer tomar/liberar, atender/descartar y cierre/reapertura por moderación.

**Salida:** superficie administrativa mínima y auditada; sin construir la UI de
chat de participantes.

**Verificación:**

```powershell
npm run test -- --run src/routes/AdminModeracionChatPage.test.tsx src/app/AppLayout.test.tsx
npm run typecheck
npm run lint
npm run build
```

Rojo esperado: ruta/vista ausente. Verde esperado: permiso y flujo de evidencia
bajo demanda cubiertos, build exitoso.

**Commit previsto:** `feat(web): agrega moderacion segura de chat`

## 13. Integración y revisión final

**Entradas:** todos los commits anteriores.

**Acciones:**

1. Aplicar migraciones en la fábrica de integración y confirmar que OpenAPI y el
   cliente generado no tienen diff pendiente.
2. Revisar el diff completo contra el spec: estados, permisos, 404
   indistinguible, concurrencia, retiro de grupos, evidencia, PII y límites 3D.
3. Buscar texto sospechoso con `rg` en logs, excepciones, eventos y DTO.
4. Corregir solo defectos de integración; no sumar features.

**Verificación backend:**

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

**Verificación frontend:**

```powershell
npm run generate:api
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

**Verificación repositorio:**

```powershell
git diff --check
git status --short --branch
```

Resultado esperado: suites completas verdes, generación reproducible, sin
mojibake ni diff pendiente. Si Docker no está disponible, registrar comando y
error causal y no declarar verdes las pruebas de integración.

**Commit previsto:** `chore(chat): integra seguridad y moderacion 3c`
