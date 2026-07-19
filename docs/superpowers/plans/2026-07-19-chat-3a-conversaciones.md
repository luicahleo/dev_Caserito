# Plan TDD — Chat 3A conversaciones y mensajes

Fecha: 2026-07-19  
Spec: `docs/superpowers/specs/2026-07-19-chat-3a-conversaciones-design.md`

## Reglas de ejecución

- Ejecutar una tarea por vez: test rojo, confirmar el fallo causal, implementación
  mínima, verde, autorrevisión y correcciones, commit.
- Ejecutar comandos .NET desde `CaseritoApp/`.
- No incorporar SignalR, notificaciones activas, bloqueo, reportes ni moderación.
- No registrar texto, participantes, claves idempotentes, tokens ni cuerpos HTTP.
- No avanzar si aparece una decisión que contradice el spec.

## Contratos finales previstos

Rutas HTTP:

- `POST /api/chat/conversaciones`;
- `GET /api/chat/conversaciones`;
- `POST /api/chat/conversaciones/{id}/mensajes`;
- `GET /api/chat/conversaciones/{id}/mensajes`;
- `PUT /api/chat/conversaciones/{id}/lectura`.

Puertos principales de Chat Application:

- `IConsultaAvisoContactable`;
- `IRepositorioConversaciones`;
- `IConsultaConversaciones`;
- `IRepositorioMensajes`;
- `IConsultaMensajes`.

El adaptador de `IConsultaAvisoContactable` se ubica en Host y consume
`IConsultaAvisosPublica.ObtenerReferenciaContactableAsync`, que devuelve solo aviso y
vendedor cuando el aviso es activo y visible.

## Tarea 1 — Agregado Conversacion y participantes

**Entradas:** invariantes aprobadas de inicio y participantes.  
**Salidas:** raíz de agregado, errores y evento de creación.

Rutas:

- crear `tests/CaseritoApp.UnitTests/Chat/ConversacionTests.cs`;
- crear `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/Conversacion.cs`;
- crear `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/ErroresConversacion.cs`;
- crear `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/EventosConversacion.cs`;
- actualizar `tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj` con referencia
  a `Chat.Domain`.

Casos rojos:

- crea con IDs opacos distintos y timestamps UTC recibidos;
- rechaza IDs vacíos y comprador igual al vendedor;
- reconoce únicamente a comprador y vendedor como participantes;
- emite `ConversacionIniciada` sin texto ni datos de perfil.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~Chat.ConversacionTests
```

Fallo rojo esperado: tipos de Chat inexistentes. Verde: todos los casos dirigidos
pasan. Autorrevisar que Domain solo dependa de BuildingBlocks.Domain.

Commit: `feat(chat): modela conversaciones y participantes`.

## Tarea 2 — Mensaje inmutable, texto y lectura monotónica

**Entradas:** agregado de la tarea 1.  
**Salidas:** entidad Mensaje, validación de texto y cursores de lectura.

Rutas:

- crear `tests/CaseritoApp.UnitTests/Chat/MensajeTests.cs`;
- ampliar `tests/CaseritoApp.UnitTests/Chat/ConversacionTests.cs`;
- crear `src/Chat/CaseritoApp.Chat.Domain/Conversaciones/Mensaje.cs`;
- ampliar `Conversacion.cs`, errores y eventos.

Casos rojos:

- normaliza extremos y conserva espacios/saltos internos;
- rechaza vacío y más de 2.000 caracteres;
- rechaza remitente no participante y claves vacías;
- crea un mensaje sin setters públicos de contenido;
- avanza lectura del participante sin retroceder ni superar la última secuencia;
- actualiza última actividad sin aceptar fechas del cliente;
- emite `MensajeEnviado` y `LecturaAvanzada` sin texto.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~Chat
```

Fallo rojo esperado: operaciones y entidad ausentes. Verde: suite Chat de dominio.
Autorrevisar inmutabilidad y ausencia de contenido en eventos/errores.

Commit: `feat(chat): modela mensajes inmutables y lectura`.

## Tarea 3 — Iniciar conversación en Application

**Entradas:** dominio y snapshot mínimo de aviso.  
**Salidas:** command, validator, DTO y puertos de escritura/consulta.

Rutas:

- crear `tests/CaseritoApp.UnitTests/Chat/IniciarConversacionCommandHandlerTests.cs`;
- actualizar el csproj unitario con referencia a `Chat.Application`;
- crear `src/Chat/CaseritoApp.Chat.Application/Conversaciones/DtosChat.cs`;
- crear `.../Conversaciones/IConsultaAvisoContactable.cs`;
- crear `.../Conversaciones/IRepositorioConversaciones.cs`;
- crear `.../Conversaciones/IniciarConversacionCommand.cs`.

Contrato:

- command: `CompradorId`, `AvisoId`;
- puerto devuelve `ReferenciaAvisoContactable(AvisoId, VendedorId)?`;
- resultado distingue `Creada` de `Existente` para mapear 201/200;
- preconsulta por comprador-aviso vuelve idempotente el caso normal;
- el índice único resolverá la carrera en persistencia.

Casos rojos:

- devuelve existente sin duplicar;
- aviso no contactable produce error no encontrado genérico;
- autochat produce conflicto;
- creación usa `TimeProvider` y agrega una sola vez;
- validator rechaza GUID vacíos.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~IniciarConversacion
```

Fallo rojo esperado: command/puertos inexistentes. Verde: handler y validator pasan.
Autorrevisar que Chat Application no referencie Catalog.

Commit: `feat(chat): implementa inicio idempotente de conversacion`.

## Tarea 4 — Enviar mensajes con idempotencia

**Entradas:** conversación persistible y secuencia estable.  
**Salidas:** command de envío y puertos de mensajes.

Rutas:

- crear `tests/CaseritoApp.UnitTests/Chat/EnviarMensajeCommandHandlerTests.cs`;
- crear `src/Chat/CaseritoApp.Chat.Application/Mensajes/IRepositorioMensajes.cs`;
- crear `src/Chat/CaseritoApp.Chat.Application/Mensajes/EnviarMensajeCommand.cs`;
- ampliar DTOs y repositorio de conversaciones según sea necesario.

Contrato:

- command: conversación, remitente autenticado, clave y texto;
- `IRepositorioMensajes` obtiene por conversación/remitente/clave, reserva la próxima
  secuencia desde SQL y agrega;
- reintento con texto normalizado igual devuelve el original;
- misma clave con texto distinto devuelve conflicto;
- no participante recibe el mismo error que conversación inexistente.

Casos rojos:

- nuevo mensaje reserva secuencia, actualiza conversación y agrega;
- reintento idéntico no reserva ni agrega;
- clave incompatible produce conflicto;
- conversación ausente y tercero producen error no encontrado;
- validator cubre GUID y longitud sin reflejar el texto en mensajes de error.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~EnviarMensaje
```

Fallo rojo esperado: command/puerto inexistentes. Verde: casos dirigidos pasan.
Autorrevisar que ninguna excepción o log reciba texto o clave.

Commit: `feat(chat): implementa envio idempotente de mensajes`.

## Tarea 5 — Consultas por cursor y marcado de lectura

**Entradas:** DTOs y reglas de autorización.  
**Salidas:** queries para listar conversaciones, mensajes y avanzar lectura.

Rutas:

- crear `tests/CaseritoApp.UnitTests/Chat/ConsultasChatHandlerTests.cs`;
- crear `tests/CaseritoApp.UnitTests/Chat/MarcarLecturaCommandHandlerTests.cs`;
- crear `src/Chat/CaseritoApp.Chat.Application/Conversaciones/IConsultaConversaciones.cs`;
- crear `.../Conversaciones/ListarConversacionesQuery.cs`;
- crear `src/Chat/CaseritoApp.Chat.Application/Mensajes/IConsultaMensajes.cs`;
- crear `.../Mensajes/ObtenerMensajesQuery.cs`;
- crear `.../Conversaciones/MarcarLecturaCommand.cs`;
- crear `src/Chat/CaseritoApp.Chat.Application/Paginacion/ResultadosCursor.cs`.

Contrato:

- Application recibe fronteras ya tipadas, no strings opacos;
- listado: límite 20 por defecto/50 máximo, orden por última actividad e ID;
- mensajes: 50 por defecto/100 máximo, frontera de secuencia exclusiva;
- repositorios siempre filtran por participante;
- lectura valida la última secuencia existente y solo avanza;
- conteo no leído excluye mensajes propios.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter "FullyQualifiedName~ConsultasChat|FullyQualifiedName~MarcarLectura"
```

Fallo rojo esperado: queries y contratos ausentes. Verde: autorización, límites y
monotonicidad pasan. Autorrevisar que las consultas no carguen agregados completos.

Commit: `feat(chat): agrega consultas paginadas y estado de lectura`.

## Tarea 6 — Mapeo EF, repositorios y migración de Chat

**Entradas:** contratos de Application y entidades Domain.  
**Salidas:** persistencia SQL Server completa en schema `chat`.

Rutas:

- crear `tests/CaseritoApp.IntegrationTests/ChatPersistenciaTests.cs`;
- actualizar csproj de integración con referencias requeridas a Chat;
- ampliar `src/Chat/CaseritoApp.Chat.Infrastructure/ChatDbContext.cs`;
- crear `.../Conversaciones/ConfiguracionChat.cs`;
- crear `.../Conversaciones/RepositorioConversacionesEfCore.cs`;
- crear `.../Conversaciones/ConsultaConversacionesEfCore.cs`;
- crear `.../Mensajes/RepositorioMensajesEfCore.cs`;
- crear `.../Mensajes/ConsultaMensajesEfCore.cs`;
- crear `.../UnitOfWorkChat.cs`;
- crear `.../DependencyInjection.cs`;
- crear `.../DesignTimeChatDbContextFactory.cs`;
- generar `src/Chat/CaseritoApp.Chat.Infrastructure/Migrations/*ChatInicial*` y snapshot;
- actualizar `tests/.../Infrastructure/CaseritoApiFactory.cs` para registrar y migrar
  `ChatDbContext`.

Detalles cerrados:

- `chat.SecuenciaMensajes` será una secuencia SQL `bigint`; el repositorio obtiene
  `NEXT VALUE FOR` antes de construir el mensaje, por lo que el handler conoce el valor
  y los huecos son válidos;
- índices únicos nombrados para conversación y clave idempotente;
- FK interna restrictiva;
- texto `nvarchar(2000)`;
- `rowversion` en conversación;
- consultas `AsNoTracking` y proyección directa;
- `UnitOfWorkChat` traduce concurrencia y los dos índices únicos a excepciones técnicas
  sin incluir valores sensibles, y limpia tracking tras un fallo recuperable.

Primero escribir tests que inspeccionen modelo y, con Testcontainers, prueben:

- unicidad comprador-aviso;
- unicidad de clave por conversación/remitente;
- secuencias crecientes;
- orden y fronteras de cursor;
- no leídos y aislamiento por participante;
- conflicto optimista.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~ChatPersistenciaTests
```

Fallo rojo esperado: tablas/mapeos/repositorios ausentes. Verde: pruebas dirigidas con
SQL Server. Si Docker no está disponible, registrar el error causal y no declarar
verde.

Comando de migración:

```powershell
dotnet ef migrations add ChatInicial --project src/Chat/CaseritoApp.Chat.Infrastructure/CaseritoApp.Chat.Infrastructure.csproj --context ChatDbContext --output-dir Migrations
```

Commit: `feat(chat): persiste conversaciones y mensajes`.

## Tarea 7 — Contrato contactable de Catalog y adaptador de Host

**Entradas:** puerto neutral de Chat y reglas públicas actuales de Catalog.  
**Salidas:** lectura mínima de vendedor sin acoplar Chat.

Rutas:

- crear o ampliar test unitario en
  `tests/CaseritoApp.UnitTests/Catalog/ObtenerAvisoContactableTests.cs`;
- ampliar `src/Catalog/CaseritoApp.Catalog.Application/Avisos/IConsultaAvisosPublica.cs`;
- ampliar `src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs`;
- crear `src/Host/CaseritoApp.Host/Chat/ConsultaAvisoContactableAdapter.cs`;
- actualizar el registro DI de Host.

Contrato Catalog:

```text
ReferenciaAvisoContactableDto(AvisoId, VendedorId)?
```

Solo retorna valor para `Activo + Visible`; los demás estados y ausencia devuelven
null. El DTO no se agrega al endpoint público de avisos.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~ObtenerAvisoContactable
```

Fallo rojo esperado: método/DTO inexistentes. Verde: estados permitidos y denegados.
Ejecutar además el test de aislamiento de contextos.

```powershell
dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj --filter FullyQualifiedName~AislamientoEntreContextosTests
```

Commit: `feat(chat): adapta avisos contactables desde catalog`.

## Tarea 8 — Endpoints, cursores opacos y rate limiting

**Entradas:** commands/queries listos y usuario autenticado.  
**Salidas:** superficie HTTP completa de 3A.

Rutas:

- crear `tests/CaseritoApp.IntegrationTests/ChatCursoresTests.cs` para probar el codec
  del Host sin agregar una dependencia de Host al proyecto unitario;
- crear `src/Host/CaseritoApp.Host/Endpoints/ChatEndpoints.cs`;
- crear `src/Host/CaseritoApp.Host/Chat/CursoresChat.cs`;
- actualizar `src/Host/CaseritoApp.Host/Program.cs`;
- actualizar `src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj` con referencia a
  `Chat.Infrastructure`.

Cursores:

- Base64Url de una versión y campos invariantes;
- conversación: ticks UTC de última actividad + GUID de desempate;
- mensajes: secuencia exclusiva;
- cualquier versión, Base64 o payload inválido produce 400 genérico;
- nunca incorporar usuario, texto ni participante al cursor.

Rate limiting particionado por claim `sub`:

- inicio: ventana fija de 10 por hora, sin cola;
- envío: token bucket con capacidad 25, reposición de 20 por minuto y sin cola;
- consultas/lectura: ventana fija de 120 por minuto, sin cola;
- partición por IP solo antes de autenticación;
- respuesta 429 sin eco de la clave de partición.

Mapeo:

- 201/200 para creación y envío según `FueCreado`;
- 400 ValidationProblem/cursor;
- 404 tanto para inexistencia como no participante;
- 409 para autochat, clave incompatible y conflicto agotado;
- 401 y 429 declarados en OpenAPI.

Los endpoints reintentan una vez las carreras recuperables de unicidad/concurrencia
volviendo a ejecutar el command con el mismo input; nunca escriben input en logs.

Comando rojo/verde inicial:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~ChatCursoresTests
```

Luego:

```powershell
dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj
```

Fallo rojo esperado: codec/endpoints ausentes. Verde: codec dirigido y Host compila.
Autorrevisar claims confiables, respuestas 404 y ausencia de datos en logs.

Commit: `feat(chat): expone api protegida y rate limiting`.

## Tarea 9 — Flujo HTTP y autorización estricta

**Entradas:** API y SQL completos.  
**Salidas:** cobertura integral del comportamiento visible.

Rutas:

- crear `tests/CaseritoApp.IntegrationTests/ChatFlujoTests.cs`;
- ampliar helpers de integración solo si son reutilizables y no registran secretos.

Escenarios rojos, implementando solo las correcciones necesarias:

- anónimo recibe 401;
- comprador inicia sobre aviso activo ajeno: 201;
- segundo inicio y carrera devuelven la misma conversación sin duplicar;
- dueño del aviso recibe 409;
- aviso pausado/oculto/eliminado/no existente recibe 404;
- conversación existente continúa después del cambio de estado del aviso;
- ambos participantes envían y un tercero recibe 404 en lectura/escritura;
- reintento de mensaje devuelve el mismo mensaje y texto distinto da 409;
- mensajes se ordenan y cargan hacia atrás sin duplicados;
- solo aparecen conversaciones propias;
- lectura avanza, no retrocede y actualiza no leídos;
- texto inválido da 400;
- límites superados dan 429 sin filtrar participante.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~ChatFlujoTests
```

Fallo rojo esperado: primer comportamiento aún no cubierto o discrepancia HTTP.
Verde: todos los escenarios. Autorrevisar cada respuesta desde la perspectiva de un
atacante que intenta enumerar conversaciones.

Commit: `test(chat): cubre flujo y autorizacion de conversaciones`.

## Tarea 10 — Contrato futuro de eventos y guardas anti-PII

**Entradas:** eventos de dominio y política anti-PII.  
**Salidas:** contrato inerte para Notifications y tests de no exposición.

Rutas:

- crear `src/BuildingBlocks/CaseritoApp.BuildingBlocks.Contracts/Chat/ChatMessageSent.cs`;
- ampliar `tests/CaseritoApp.ArchitectureTests/PiiRedactionTests.cs`;
- crear `tests/CaseritoApp.ArchitectureTests/Chat/ChatPiiTests.cs` si hace falta
  inspección estructural.

Contrato `ChatMessageSent`:

- `EventId`, `OcurridoEn`, `ConversacionId`, `MensajeId`, `RemitenteId`,
  `DestinatarioId`;
- no contiene texto, preview, aviso ni datos de perfil;
- no se publica en 3A para no simular entrega antes del commit.

Casos rojos:

- redacción reconoce `texto`, `mensaje`, `participante`, `remitente`,
  `destinatario` y `claveIdempotencia`, sin distinguir mayúsculas;
- reflexión confirma ausencia de campos de contenido en el contrato;
- eventos de dominio tampoco contienen texto.

Comando rojo/verde:

```powershell
dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj --filter "FullyQualifiedName~PiiRedactionTests|FullyQualifiedName~ChatPiiTests"
```

Fallo rojo esperado: campos aún no redactados/contrato ausente. Verde: guardas
anti-PII. Autorrevisar que IDs sensibles existan solo en persistencia, DTO autorizado
y contrato funcional, nunca en logging.

Commit: `feat(chat): prepara contrato seguro de nuevo mensaje`.

## Tarea 11 — Integración, OpenAPI y cierre

**Entradas:** implementación completa de 3A.  
**Salidas:** artefactos derivados y evidencia final.

Rutas potenciales:

- actualizar `src/Host/CaseritoApp.Host/Program.cs` para migrar Chat al arranque;
- actualizar `artifacts/openapi/CaseritoApp.Host.json` mediante generación, nunca a
  mano;
- no modificar `web/`: la UI no pertenece a 3A.

Pasos:

1. Ejecutar tests vecinos de Chat y corregir solo hallazgos del spec.
2. Generar OpenAPI con el mecanismo ya configurado por el proyecto.
3. Revisar el diff completo contra spec, capas, PII, migración y alcance.
4. Ejecutar verificaciones completas:

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
git diff --check
git status --short --branch
```

5. Confirmar que la suite de integración realmente usó Docker/SQL Server.
6. Confirmar que no hay contenido sensible en mensajes de log añadidos.
7. Confirmar que `Chat` no depende de otros bounded contexts.

Resultado esperado: build, suite completa y formato verdes; OpenAPI actualizado;
worktree limpio después del commit.

Commit: `chore(chat): integra contrato api de conversaciones` (solo si hay artefactos
o cableado final pendientes; no crear un commit vacío).

## Cierre

Entregar:

- resultado funcional de 3A;
- rama y lista de commits;
- cantidades y comandos de pruebas ejecutadas;
- cualquier prueba omitida con error causal;
- confirmación de ausencia de push y merge.

No crear 3B, 3C ni 3D. No mergear ni pushear sin autorización explícita.
