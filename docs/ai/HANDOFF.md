# Handoff — KYC automático con ARGOS

> Fecha: 2026-07-28  
> Rama: `feat/kyc-argos`  
> Estado: Tasks 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 y 11 completadas; pendiente Task 12 (verificación global y formato) o cierre del bloque.

## Contexto

CaseritoApp necesita usuarios con identidad verificada. El KYC actual es manual (admin aprueba/rechaza DNI + selfie). Se aprobó integrar ARGOS, un microservicio Python propio de reconocimiento facial (DeepFace/ArcFace), para automatizar la decisión.

## Spec y plan

- Spec: `docs/superpowers/specs/2026-07-28-kyc-argos-design.md`
- Plan: `docs/superpowers/plans/2026-07-28-kyc-argos.md`

## Progreso de esta sesión

### Task 1: Domain — score, errores y actor de sistema ✅

Archivos modificados:
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/SolicitudKyc.cs` — agrega `ScoreSimilitud` y `RegistrarScoreSimilitud`.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/ErroresKyc.cs` — agrega `VerificacionFacialFallida` y `ServicioVerificacionNoDisponible`.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/SistemaActor.cs` — actor de sistema para resoluciones automáticas.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/VerificacionKyc.cs` — agrega `PuedeEnviarSolicitud()` para validar invariants antes de escribir blobs.

Verificación:
- `dotnet build src/Identity/CaseritoApp.Identity.Domain/CaseritoApp.Identity.Domain.csproj` → exit 0, 0 warnings.

Commit: `d646762` — `feat(kyc-argos): score, errores y actor de sistema en dominio`

### Task 2: Application — puerto `IVerificadorIdentidadArgos` ✅

Archivo creado:
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/IVerificadorIdentidadArgos.cs` — define el puerto y el record `VerificacionFacialResultado`.

Verificación:
- `dotnet build src/Identity/CaseritoApp.Identity.Application/CaseritoApp.Identity.Application.csproj` → exit 0, 0 warnings.

Commit: `6a4d659` — `feat(kyc-argos): puerto IVerificadorIdentidadArgos en Application`

### Task 3: Application — handler envío automático ✅

Archivos modificados:
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/EnviarSolicitudKycCommand.cs` — inyecta `IVerificadorIdentidadArgos` e `IPublicadorEventosIntegracion`; valida invariants antes de guardar blobs; consulta ARGOS; compensa eliminando blobs si falla; aplica `Aprobar`/`Rechazar` con `SistemaActor.Id`; publica `UserVerified` solo si aprobó.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Kyc/SolicitudKyc.cs` — `RegistrarScoreSimilitud` pasa a `public` para poder invocarse desde Application (no hay `InternalsVisibleTo`).

Verificación:
- `dotnet build src/Identity/CaseritoApp.Identity.Application/CaseritoApp.Identity.Application.csproj` → exit 0, 0 warnings.

Commit: `50eb01a` — `feat(kyc-argos): envío automático aprueba/rechaza según ARGOS`

### Task 4: Infrastructure — adaptador HTTP a ARGOS ✅

Archivos creados:
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/OpcionesArgos.cs` — sección `Argos` con `Url` y `ApiKey`.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/VerificadorIdentidadArgosHttp.cs` — adaptador HTTP que envía documento/selfie en base64 a `POST {Url}/api/verify`, mapea errores de red/HTTP/JSON a `ServicioVerificacionNoDisponible` y `success: false` a `VerificacionFacialFallida`.

Archivos modificados:
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` — registra `OpcionesArgos`, validación fail-fast de `Argos:Url` fuera de Development/Testing, y `HttpClient` tipado para `IVerificadorIdentidadArgos`.

Verificación:
- `dotnet build src/Identity/CaseritoApp.Identity.Infrastructure/CaseritoApp.Identity.Infrastructure.csproj` → exit 0, 0 warnings.
- `dotnet format src/Identity/CaseritoApp.Identity.Infrastructure/CaseritoApp.Identity.Infrastructure.csproj` aplicado para finales de línea CRLF.
- `dotnet build CaseritoApp.sln` → exit 0, 0 warnings (requirió ajustar tests existentes a la nueva firma del handler).
- `dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter "FullyQualifiedName~Kyc"` → 19 superados, 0 fallos.

Commits:
- `acc2f27` — `feat(kyc-argos): adaptador HTTP a ARGOS y registro DI`
- `c7c8412` — `test(kyc-argos): ajusta tests existentes a firma del handler`
- `9083351` — `test(kyc-argos): ajusta asserts de test de pendiente a flujo actual`

### Task 5: Infrastructure — mapeo EF Core y migración ✅

Archivos modificados:
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConfiguracionKyc.cs` — agrega mapeo de `SolicitudKyc.ScoreSimilitud`.

Archivos creados:
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Migrations/20260728145214_KycArgosScoreSimilitud.cs` — agrega columna `ScoreSimilitud` nullable de tipo `float` en el schema `identity`.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Migrations/20260728145214_KycArgosScoreSimilitud.Designer.cs` — snapshot actualizado.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Migrations/IdentityDbContextModelSnapshot.cs` — snapshot actualizado.

Verificación:
- `dotnet ef migrations add KycArgosScoreSimilitud --startup-project ../../Host/CaseritoApp.Host/CaseritoApp.Host.csproj` → migración generada.
- Revisión manual: `Up()` solo contiene `AddColumn<double>(name: "ScoreSimilitud", table: "SolicitudesKyc", type: "float", nullable: true)`.
- `dotnet build CaseritoApp.sln` → exit 0, 0 errores, 0 advertencias.

Commit: `4f2eb86` — `feat(kyc-argos): mapeo y migracion EF Core para ScoreSimilitud`

Nota: los archivos de migración generados por EF Core incluían BOM; se eliminó para cumplir `charset = utf-8` del `.editorconfig` y pasar el hook `dotnet-format-staged`.

### Task 6: Host — mapear error de servicio no disponible a 503 ✅

Archivo modificado:
- `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs` — extiende `DesdeResult` para devolver HTTP 503 cuando el error es `ErroresKyc.ServicioVerificacionNoDisponible`, y documenta el endpoint POST con `.ProducesProblem(StatusCodes.Status503ServiceUnavailable)`.

Verificación:
- `dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj` → exit 0, 0 errores, 0 advertencias.

Commit: `845bd12` — `feat(kyc-argos): mapea error de ARGOS a HTTP 503`

### Task 7: Application — exponer score en el listado admin ✅

Archivos modificados:
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/DtosKyc.cs` — extiende `SolicitudKycResumenDto` con `ScoreSimilitud` y `ResueltaPor`.
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/RepositorioVerificacionKycEfCore.cs` — actualiza la proyección `Select(...)` para incluir `s.ScoreSimilitud` y `s.ResueltaPor`.

Verificación:
- `dotnet build CaseritoApp.sln` → exit 0, 0 errores, 0 advertencias.

Commit: `9a0e382` — `feat(kyc-argos): expone score y resolutor en listado admin`

### Task 8: Frontend — actualizar mensajes de KYC automático ✅

Archivo modificado:
- `web/src/routes/KycPage.tsx` — título cambiado a "Verificación de identidad automática", mensaje de pendiente actualizado a "está siendo verificada automáticamente", descripción del formulario indica que el sistema comparará las imágenes automáticamente, y se añade alerta específica para el error HTTP 503.

Verificación:
- `npm run typecheck` → 0 errores.
- `npm run lint` → 0 errores.

Commit: `ab93dfd` — `feat(kyc-argos): mensajes de verificacion automatica en KycPage`

### Task 9: Frontend — admin ve score y decisión automática ✅

Archivos modificados:
- `web/src/routes/AdminKycPage.tsx` — columnas Score y Resolutor en la tabla; helper `formatearScore`/`formatearResolutor`; mensaje de resolución automática para solicitudes pendientes; se eliminaron botones y mutaciones de Aprobar/Rechazar.
- `web/src/routes/AdminKycPage.test.tsx` — se ajustó el mock de solicitud con los nuevos campos y se reemplazaron tests de aprobar/rechazar por verificación de que una solicitud pendiente no muestra botones y muestra el mensaje de resolución automática.
- `web/src/routes/KycPage.test.tsx` — se actualizó el test de estado pendiente al nuevo mensaje de verificación automática.
- `web/src/api/schema.d.ts` — regenerado desde OpenAPI; incluye `scoreSimilitud` y `resueltaPor`.
- `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` — regenerado con `ASPNETCORE_ENVIRONMENT=Testing dotnet build ... -p:GenerateOpenApi=true`.

Verificación:
- `ASPNETCORE_ENVIRONMENT=Testing dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -p:GenerateOpenApi=true` → exit 0.
- `npm run generate:api` → tipos actualizados.
- `npm run typecheck` → 0 errores.
- `npm run lint` → 0 errores.
- `npm run test -- --run` → 131 tests passed, 0 failed.

Commit: `97288f2` — `feat(kyc-argos): admin muestra score y decision automatica`

### Task 10: Tests unitarios — handler y adaptador HTTP ✅

Archivos modificados:
- `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/EnviarSolicitudKycCommandHandlerTests.cs` — fakes inyectables para `IVerificadorIdentidadArgos` e `IPublicadorEventosIntegracion`; tests de coincidencia automática, rechazo automático y compensación de blobs cuando ARGOS está caído.
- `CaseritoApp/tests/CaseritoApp.UnitTests/Kyc/VerificadorIdentidadArgosHttpTests.cs` — adaptador HTTP testeado con `HttpMessageHandler` fake; cubre `verified: true`, `verified: false`, error de red, HTTP 500, JSON inválido y `success: false`.

Verificación:
- `dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter "FullyQualifiedName~Kyc"` → 28 superados, 0 fallos.
- `dotnet build CaseritoApp.sln` → exit 0, 0 errores, 0 advertencias.
- `dotnet format CaseritoApp.sln --verify-no-changes` → 0 cambios necesarios.

Commit: `32e935a` — `test(kyc-argos): handler y adaptador HTTP`

### Task 11: Tests de integración — flujo completo con ARGOS mockeado ✅

Archivos modificados:
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/KycArgosFlujoTests.cs` — creado; cubre coincidencia automática (estado `Aprobada`), rechazo automático (estado `Rechazada` con motivo) y servicio caído (HTTP 503) reemplazando `IVerificadorIdentidadArgos` en `WebApplicationFactory`.
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/KycFlujoTests.cs` — ajustado al flujo automático: usa fake aprobador, elimina aprobación manual del admin y verifica que el segundo envío da 409 por `YaVerificado`.
- `CaseritoApp/tests/CaseritoApp.IntegrationTests/KycConcurrenciaTests.cs` — eliminado el test de aprobaciones HTTP concurrentes porque con KYC automático las aprobaciones manuales ya no ocurren; se conservan los tests de concurrencia a nivel de DbContext.

Verificación:
- `dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~Kyc"` → 9 superados, 0 fallos (requiere Docker).
- `dotnet build CaseritoApp.sln` → exit 0, 0 errores, 0 advertencias.
- `dotnet format CaseritoApp.sln --verify-no-changes` → 0 cambios necesarios.

Commit: `bccb735` — `test(kyc-argos): tests de integracion con ARGOS mockeado`

## Siguiente sesión

Continuar con **Task 12: Verificación global y formato** del plan, o cerrar el bloque si ya no queda trabajo.

Archivos principales:
- Todos los archivos modificados en el bloque.

Qué hacer:
1. Ejecutar `dotnet build CaseritoApp.sln`.
2. Ejecutar `dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj`.
3. Ejecutar `dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj` (requiere Docker).
4. Ejecutar `dotnet format CaseritoApp.sln --verify-no-changes`.
5. Ejecutar frontend checks: `npm run generate:api`, `npm run typecheck`, `npm run lint`, `npm run test -- --run`, `npm run build`.
6. Commit final.

Nota: si se decide cerrar el bloque sin Task 12, crear un handoff que indique el estado y los checks pendientes.

## Restricciones importantes

- No escribir código de implementación en la sesión de planificación (ya terminó; ahora se implementa).
- Clean Architecture / CQRS-lite: domain puro, puertos en Application, adaptadores en Infrastructure.
- Anti-PII: nunca loguear bytes de imágenes, base64, embeddings ni datos del DNI.
- ARGOS es un servicio interno; no llamarlo desde el frontend.
- No hacer push/merge sin autorización explícita.
- Priorizar economía de tokens: una o dos tareas por sesión, handoff breve al cerrar.

## Verificaciones esperadas por paso

- Backend: `dotnet build CaseritoApp.sln`, `dotnet test CaseritoApp.sln`, `dotnet format CaseritoApp.sln --verify-no-changes`.
- Frontend: `npm run typecheck`, `npm run lint`, `npm run test -- --run`, `npm run build`.

## Notas

- ARGOS no hace liveness ni OCR; eso queda como follow-up futuro.
- El umbral de verificación se delega al default de ARGOS (`0.68` distancia coseno) en este bloque.
