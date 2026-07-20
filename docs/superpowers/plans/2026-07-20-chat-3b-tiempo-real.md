# Plan TDD — Chat 3B tiempo real

Fecha: 2026-07-20  
Spec: `docs/superpowers/specs/2026-07-20-chat-3b-tiempo-real-design.md`

## Reglas de ejecución

- Ejecutar una tarea por vez: test rojo, confirmar el fallo causal, implementación
  mínima, verde, verificaciones vecinas, autorrevisión crítica, correcciones y commit.
- Ejecutar comandos .NET desde `CaseritoApp/` y comandos npm desde `web/`.
- HTTP es la única vía de escritura; el hub nunca acepta texto para persistir.
- No incorporar bloqueo/moderación de 3C ni notificaciones de 3D.
- No registrar texto, payloads, tokens, query strings, IDs sensibles, participantes,
  grupos concretos ni claves de idempotencia.
- Si una decisión contradice el spec, detener la ejecución y actualizar diseño y plan
  sólo después de obtener aprobación.
- Los tests de integración usan SQL Server real mediante Testcontainers y requieren
  Docker.

## Contratos finales previstos

Superficie HTTP ampliada:

- `GET /api/chat/conversaciones/{id}/mensajes?despuesDeSecuencia={long}&limite={int}`;
- `cursor` y `despuesDeSecuencia` son mutuamente excluyentes.

Hub:

- ruta `/hubs/chat`;
- invocaciones `SuscribirConversacion(Guid)` y `DesuscribirConversacion(Guid)`;
- evento `MensajeCreado(MensajeTiempoRealDto)`;
- autenticación JWT obligatoria y cierre al expirar.

Persistencia:

- tabla `chat.EntregasTiempoReal` sin texto ni participantes;
- intención creada en la transacción del mensaje;
- reclamación con lease y entrega al menos una vez.

Frontend:

- módulo de conexión SignalR sin UI;
- recuperación hacia delante, deduplicación por ID y orden por secuencia.

## Tarea 1 — Recuperación HTTP hacia delante

**Entradas:** consulta y endpoint de mensajes de 3A.  
**Salidas:** recuperación autenticada posterior a una secuencia persistida.

Rutas:

- ampliar `tests/CaseritoApp.UnitTests/Chat/ConsultasChatHandlerTests.cs`;
- ampliar `tests/CaseritoApp.IntegrationTests/ChatPersistenciaTests.cs`;
- ampliar `tests/CaseritoApp.IntegrationTests/ChatFlujoTests.cs`;
- ampliar `src/Chat/CaseritoApp.Chat.Application/Mensajes/IConsultaMensajes.cs`;
- ampliar `src/Chat/CaseritoApp.Chat.Application/Mensajes/ObtenerMensajesQuery.cs`;
- ampliar `src/Chat/CaseritoApp.Chat.Infrastructure/Mensajes/ConsultaMensajesEfCore.cs`;
- ampliar `src/Host/CaseritoApp.Host/Endpoints/ChatEndpoints.cs`.

Contrato:

- `ObtenerMensajesQuery` recibe fronteras opcionales `AntesDeSecuencia` y
  `DespuesDeSecuencia`;
- ambas fronteras juntas son inválidas;
- `DespuesDeSecuencia >= 0`, donde cero permite sincronización inicial;
- hacia delante devuelve `Secuencia > frontera`, orden ascendente, hasta `limite`;
- no necesita cursor siguiente: el consumidor conserva la última secuencia recibida;
- sin frontera conserva exactamente el comportamiento reciente de 3A;
- autorización continúa dentro de la consulta y un tercero recibe 404.

Casos rojos:

- validator rechaza dos fronteras, negativos y límites inválidos;
- secuencia cero y secuencias intermedias devuelven sólo posteriores en orden;
- no hay duplicados al encadenar recuperaciones;
- tercero e inexistente siguen siendo indistinguibles;
- endpoint rechaza `cursor` junto con `despuesDeSecuencia`.

Comandos rojo/verde:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~ConsultasChatHandlerTests
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~ChatPersistenciaTests|FullyQualifiedName~ChatFlujoTests"
```

Fallo rojo esperado: contrato sin frontera hacia delante o resultados con el orden de
paginación histórica. Autorrevisar compatibilidad con 3A, autorización y que ninguna
secuencia aparezca en errores.

Commit: `feat(chat): recupera mensajes posteriores por secuencia`.

## Tarea 2 — Outbox transaccional de mensajes

**Entradas:** inserción EF de `Mensaje` y `ChatDbContext`.  
**Salidas:** una intención durable por cada mensaje nuevo confirmado.

Rutas:

- crear `src/Chat/CaseritoApp.Chat.Infrastructure/TiempoReal/EntregaTiempoReal.cs`;
- crear `src/Chat/CaseritoApp.Chat.Infrastructure/TiempoReal/ConfiguracionEntregaTiempoReal.cs`;
- ampliar `src/Chat/CaseritoApp.Chat.Infrastructure/ChatDbContext.cs`;
- ampliar `src/Chat/CaseritoApp.Chat.Infrastructure/Conversaciones/ConfiguracionChat.cs`
  sólo si la aplicación de configuraciones aún no es automática;
- ampliar `tests/CaseritoApp.IntegrationTests/ChatPersistenciaTests.cs`;
- generar migración `ChatEntregasTiempoReal` y actualizar snapshot.

Modelo técnico:

- `Id`, `ConversacionId`, `MensajeId`, `Secuencia`, `CreadaEn`, `Intentos`,
  `ProximoIntentoEn`, `ProcesadaEn`, `LeaseHasta`;
- índice único por `MensajeId`;
- índice de pendientes por `ProcesadaEn`, `ProximoIntentoEn` y `LeaseHasta`;
- FK interna restrictiva a `chat.Mensajes`;
- ninguna columna para texto, usuario, destinatario, token o clave idempotente.

Antes de guardar, `ChatDbContext` detecta únicamente entidades `Mensaje` añadidas y
añade una entrega por mensaje al mismo change set. Usa el timestamp del mensaje para
`CreadaEn`; no inventa otro dato de negocio. EF confirma mensaje y entrega en una sola
transacción. No se modifica Domain ni Application y no se despacha SignalR aquí.

Casos rojos con SQL Server:

- guardar un mensaje crea exactamente una entrega;
- rollback provocado no deja mensaje ni entrega;
- consultar/reintentar un mensaje idempotente existente no crea otra entrega;
- índice único impide duplicar intención;
- el modelo no contiene columnas sensibles;
- migración y snapshot pertenecen al schema `chat`.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~ChatPersistenciaTests
```

Migración:

```powershell
dotnet ef migrations add ChatEntregasTiempoReal --project src/Chat/CaseritoApp.Chat.Infrastructure/CaseritoApp.Chat.Infrastructure.csproj --startup-project src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj --context ChatDbContext --output-dir Migrations
```

Fallo rojo esperado: tabla/tipo ausente y ninguna intención tras guardar. Autorrevisar
atomicidad, tracking, índices, FK interna y ausencia de duplicación de contenido.

Commit: `feat(chat): persiste outbox de entrega en tiempo real`.

## Tarea 3 — Reclamación, lease y reintentos de outbox

**Entradas:** tabla de entregas pendiente.  
**Salidas:** puerto y repositorio SQL seguro para uno o varios workers.

Rutas:

- crear `src/Chat/CaseritoApp.Chat.Infrastructure/TiempoReal/IAlmacenEntregasTiempoReal.cs`;
- crear `src/Chat/CaseritoApp.Chat.Infrastructure/TiempoReal/AlmacenEntregasTiempoRealSql.cs`;
- crear DTOs técnicos internos de trabajo en el mismo directorio;
- ampliar `src/Chat/CaseritoApp.Chat.Infrastructure/DependencyInjection.cs`;
- crear `tests/CaseritoApp.IntegrationTests/ChatOutboxTests.cs`.

Contrato del almacén:

- reclamar un lote pequeño de pendientes vencidas en orden `Secuencia, Id`;
- asignar `LeaseHasta` de forma atómica con SQL Server;
- cargar el mensaje persistido sólo después de reclamarlo;
- marcar procesada validando que la lease siga vigente;
- al fallar incrementar `Intentos`, limpiar lease y calcular `ProximoIntentoEn` con
  backoff acotado;
- una lease vencida vuelve a ser reclamable;
- no retornar participantes ni claves de idempotencia.

La reclamación usa una transacción corta y hints apropiados de SQL Server, por ejemplo
`UPDLOCK`, `READPAST` y `ROWLOCK`, sin mantener una transacción durante la publicación
de red. Los tiempos entran mediante `TimeProvider` para pruebas deterministas.

Casos rojos:

- dos reclamantes concurrentes no obtienen la misma entrega con lease vigente;
- se respeta el orden estable;
- lease vencida se recupera;
- éxito marca procesada;
- fallo programa reintento y aumenta intentos;
- un fallo después de publicar permite una segunda reclamación, documentando al menos
  una vez;
- proyección de trabajo contiene sólo el mensaje necesario para el grupo autorizado.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~ChatOutboxTests
```

Fallo rojo esperado: almacén ausente o dos workers reclaman la misma fila. Autorrevisar
bloqueos, transacciones cortas, cancelación, backoff y mensajes de excepción sin datos.

Commit: `feat(chat): reclama y reintenta entregas en tiempo real`.

## Tarea 4 — Consulta de participación y grupos seguros

**Entradas:** participantes persistidos e identidad `sub`.  
**Salidas:** autorización reusable y nombres internos de grupo.

Rutas:

- ampliar `src/Chat/CaseritoApp.Chat.Application/Conversaciones/IConsultaConversaciones.cs`;
- crear `src/Chat/CaseritoApp.Chat.Application/Conversaciones/PuedeAccederConversacionQuery.cs`;
- ampliar `src/Chat/CaseritoApp.Chat.Infrastructure/Conversaciones/ConsultaConversacionesEfCore.cs`;
- crear `src/Host/CaseritoApp.Host/Chat/GruposChat.cs`;
- ampliar `tests/CaseritoApp.UnitTests/Chat/ConsultasChatHandlerTests.cs`;
- crear pruebas estructurales o dirigidas en
  `tests/CaseritoApp.IntegrationTests/ChatTiempoRealTests.cs`.

Contrato:

- Application responde sólo un booleano para `(conversacionId, usuarioId)`;
- Infrastructure filtra comprador o vendedor sin cargar historial;
- `GruposChat.ParaConversacion` genera internamente
  `chat:conversacion:{guid-N}`;
- no se expone una API que acepte nombres arbitrarios;
- errores de autorización no incluyen IDs.

Casos rojos:

- comprador y vendedor autorizados;
- tercero, conversación ausente y GUID vacío no autorizados;
- nombre de grupo determinista, invariante y sin texto o usuarios;
- Application continúa sin referencia a Host o SignalR.

Comandos rojo/verde:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~ConsultasChatHandlerTests
dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj --filter FullyQualifiedName~AislamientoEntreContextosTests
```

Fallo rojo esperado: consulta y generador ausentes. Autorrevisar indistinguibilidad de
ausencia/tercero y límites de capas.

Commit: `feat(chat): autoriza grupos por participante`.

## Tarea 5 — Hub autenticado, suscripciones y límites

**Entradas:** consulta de participación y grupos seguros.  
**Salidas:** hub protegido sin vía de escritura de mensajes.

Rutas:

- crear `src/Host/CaseritoApp.Host/Chat/ChatHub.cs`;
- crear `src/Host/CaseritoApp.Host/Chat/EstadoSuscripcionesChat.cs`;
- crear `src/Host/CaseritoApp.Host/Chat/OpcionesTiempoRealChat.cs`;
- ampliar `src/Host/CaseritoApp.Host/Program.cs`;
- ampliar `src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ConfigurarJwtBearer.cs`;
- ampliar `src/Host/CaseritoApp.Host/appsettings.json`;
- ampliar `tests/CaseritoApp.IntegrationTests/ChatTiempoRealTests.cs`;
- añadir versión central y referencia de test para
  `Microsoft.AspNetCore.SignalR.Client` en `Directory.Packages.props` y
  `tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj`.

Configuración:

- `AddSignalR` con detalles de error desactivados;
- tamaño, buffers, paralelismo y handshake conservadores y configurables;
- `MapHub<ChatHub>("/hubs/chat").RequireAuthorization()`;
- `CloseOnAuthenticationExpiration = true`;
- JWT `OnMessageReceived` lee `access_token` sólo bajo `/hubs/chat`;
- máximo 20 grupos y límite local de invocaciones por conexión;
- estado scoped por conexión, sin IDs en logs.

`SuscribirConversacion` valida `sub`, límite y participación antes de
`Groups.AddToGroupAsync`. `DesuscribirConversacion` es idempotente. El hub no contiene
métodos para enviar texto, marcar lectura ni enumerar conversaciones.

Casos rojos con cliente SignalR real:

- anónimo no conecta;
- Bearer válido conecta;
- token en query funciona para el hub y no autentica un endpoint HTTP cualquiera;
- identidad inválida se rechaza;
- comprador y vendedor se suscriben;
- tercero obtiene error genérico;
- exceso de grupos y abuso se rechazan sin eco de argumentos;
- desconexión elimina estado y reconexión requiere suscribirse de nuevo;
- el contrato público del hub sólo expone las dos invocaciones aprobadas.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~ChatTiempoRealTests
```

Fallo rojo esperado: ruta de hub ausente o handshake 404. Autorrevisar scope del token
en query, expiración, límites, errores y ausencia de escritura por hub.

Commit: `feat(chat): expone hub autenticado y suscripciones seguras`.

## Tarea 6 — Worker y publicación post-commit

**Entradas:** outbox reclamable y hub con grupos autorizados.  
**Salidas:** publicación al menos una vez de `MensajeCreado`.

Rutas:

- crear `src/Host/CaseritoApp.Host/Chat/MensajeTiempoRealDto.cs`;
- crear `src/Host/CaseritoApp.Host/Chat/IPublicadorMensajesTiempoReal.cs`;
- crear `src/Host/CaseritoApp.Host/Chat/PublicadorSignalRMensajes.cs`;
- crear `src/Host/CaseritoApp.Host/Chat/DespachadorEntregasTiempoReal.cs`;
- ampliar `src/Host/CaseritoApp.Host/Program.cs`;
- crear pruebas unitarias aisladas en
  `tests/CaseritoApp.IntegrationTests/ChatDespachadorTiempoRealTests.cs`;
- ampliar `tests/CaseritoApp.IntegrationTests/ChatTiempoRealTests.cs`.

Contrato:

- `MensajeTiempoRealDto` tiene exactamente ID, conversación, remitente, secuencia,
  texto y fecha;
- el publicador recibe una proyección ya cargada y emite `MensajeCreado` al grupo;
- el worker crea scopes breves, reclama lotes, publica y confirma cada resultado;
- cancelación y apagado no marcan trabajo inconcluso;
- fallo de red programa reintento sin cambiar el éxito HTTP original;
- no hay logging de DTO, excepción con argumentos, grupo ni IDs.

Casos rojos:

- contrato exacto del evento;
- no se publica antes de que exista la outbox confirmada;
- rollback no produce evento;
- commit produce evento para participantes suscritos;
- reintento HTTP idempotente no crea una nueva entrega;
- fallo del publisher reprograma y luego entrega;
- fallo tras emitir puede duplicar con mismo ID y secuencia;
- conexiones no suscritas no reciben el evento;
- varias conexiones suscritas reciben el mismo contrato.

Comandos rojo/verde:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~ChatDespachadorTiempoRealTests|FullyQualifiedName~ChatTiempoRealTests"
```

Fallo rojo esperado: outbox queda pendiente y ningún evento llega. Autorrevisar
post-commit real, comportamiento al apagar, al menos una vez y exposición mínima.

Commit: `feat(chat): publica mensajes confirmados por signalr`.

## Tarea 7 — Guardas de arquitectura, PII y observabilidad

**Entradas:** hub, outbox y worker completos.  
**Salidas:** límites estructurales y telemetría segura.

Rutas:

- ampliar `tests/CaseritoApp.ArchitectureTests/Chat/ChatPiiTests.cs`;
- ampliar `tests/CaseritoApp.ArchitectureTests/PiiRedactionTests.cs` si aparecen nuevos
  nombres sensibles;
- crear `tests/CaseritoApp.ArchitectureTests/Chat/ChatTiempoRealArchitectureTests.cs`;
- ajustar únicamente logs y métricas de los componentes de 3B.

Casos rojos:

- outbox no tiene texto, usuarios, destinatarios, tokens o claves;
- el contrato SignalR no agrega campos fuera de los seis aprobados;
- Domain y Application no referencian SignalR;
- Hub no expone métodos adicionales;
- logs no reciben DTOs, IDs, grupos, argumentos o query strings;
- métricas sólo usan categorías de cardinalidad baja;
- errores de hub no contienen valores de entrada.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj --filter "FullyQualifiedName~Chat|FullyQualifiedName~PiiRedactionTests|FullyQualifiedName~AislamientoEntreContextosTests"
```

Fallo rojo esperado: nueva superficie sin guarda o propiedad sensible detectada.
Autorrevisar falsos negativos, no sólo nombres exactos.

Commit: `test(chat): protege tiempo real contra pii y acoplamiento`.

## Tarea 8 — Cliente web mínimo y sincronización

**Entradas:** protocolo SignalR y recuperación HTTP aprobados.  
**Salidas:** infraestructura tipada consumible por una futura UI.

Rutas:

- actualizar `web/package.json` y `web/package-lock.json` con
  `@microsoft/signalr`;
- crear `web/src/api/chat.ts` para recuperación HTTP tipada;
- crear `web/src/chat/tiempoReal.ts`;
- crear `web/src/chat/sincronizacionMensajes.ts`;
- crear `web/src/chat/tiempoReal.test.ts`;
- crear `web/src/chat/sincronizacionMensajes.test.ts`.

Contrato:

- factoría de conexión a `/hubs/chat` con `accessTokenFactory` que llama
  `getAccessToken` bajo demanda;
- reconexión automática con backoff y jitter acotados;
- al reconectar, resuscribe sólo conversaciones registradas localmente;
- callback tipado de `MensajeCreado`;
- sincronizador deduplica por ID, ordena por secuencia y detecta huecos;
- un hueco o reconexión llama a recuperación HTTP desde la última secuencia;
- no persiste token ni contenido adicional y no registra payloads;
- no se agregan rutas ni componentes visuales.

Casos rojos con una factoría/conexión falsa:

- token se obtiene al conectar y no se copia a storage;
- suscripción registra el ID y desuscripción lo elimina;
- reconexión resuscribe y recupera;
- mensaje duplicado se ignora;
- mensajes fuera de orden quedan ordenados;
- hueco activa una sola recuperación coordinada;
- dos instancias funcionan sin estado global compartido;
- errores públicos son genéricos.

Comandos rojo/verde:

```powershell
npm run test -- --run src/chat/tiempoReal.test.ts src/chat/sincronizacionMensajes.test.ts
npm run typecheck
```

Fallo rojo esperado: módulos/dependencia ausentes. Autorrevisar tipos estrictos,
limpieza de handlers, carreras de reconexión y ausencia de UI accidental.

Commit: `feat(web): prepara cliente signalr de chat`.

## Tarea 9 — OpenAPI, contratos y cierre

**Entradas:** backend y cliente completos.  
**Salidas:** artefactos derivados y evidencia integral de 3B.

Rutas potenciales:

- regenerar `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`;
- regenerar `web/src/api/schema.d.ts`;
- corregir sólo discrepancias de integración de 3B;
- no crear UI, 3C ni 3D.

Pasos:

1. Generar OpenAPI sin editar el JSON manualmente:

```powershell
dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -p:GenerateOpenApi=true
```

2. Regenerar tipos:

```powershell
npm run generate:api
```

3. Confirmar que OpenAPI sólo refleja la recuperación HTTP; el protocolo SignalR se
   valida mediante sus tests tipados.
4. Revisar el diff completo contra el spec: autorización, post-commit, leases,
   duplicados, orden, reconexión, PII, capas y alcance diferido.
5. Ejecutar backend completo desde `CaseritoApp/`:

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

6. Ejecutar frontend completo desde `web/`:

```powershell
npm run typecheck
npm run lint
npm run test -- --run
npm run build
npm run format:check
```

7. Desde la raíz:

```powershell
git diff --check
git status --short --branch
```

8. Confirmar que integración usó Docker/SQL Server y que no se registró ningún dato
   sensible durante pruebas.

Resultado esperado: build, suite completa, formato, lint, typecheck y build web
verdes; artefactos regenerados; worktree limpio después del commit.

Commit: `chore(chat): integra contratos de tiempo real 3b`.

## Cierre

Entregar:

- resultado funcional de 3B;
- rama y commits;
- cantidades y comandos de pruebas ejecutadas;
- cualquier verificación omitida y su error causal;
- limitación explícita de una sola instancia sin backplane;
- confirmación de que no hubo merge ni push.

No implementar 3C ni 3D. No mergear ni pushear sin autorización explícita. Si la
sesión termina incompleta, reemplazar un único `docs/ai/HANDOFF.md` desde la plantilla;
eliminarlo al cerrar completamente 3B.
