# Handoff — KYC automático con ARGOS

> Fecha: 2026-07-28  
> Rama: `feat/kyc-argos`  
> Estado: Tasks 1, 2, 3, 4, 5, 6 y 7 completadas; pendiente Task 8 en adelante.

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

## Siguiente sesión

Continuar con **Task 8: Frontend — actualizar mensajes de KYC automático** del plan.

Archivo principal:
- `web/src/routes/KycPage.tsx`

Qué hacer:
1. Actualizar título y mensajes para indicar verificación automática.
2. Añadir manejo del error HTTP 503.
3. Verificar `npm run typecheck` y `npm run lint`.
4. Commit.

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
