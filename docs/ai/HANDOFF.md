# Handoff — KYC automático con ARGOS

> Fecha: 2026-07-28  
> Rama: `feat/kyc-argos`  
> Estado: Tasks 1 y 2 completadas; pendiente Task 3 en adelante.

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

## Siguiente sesión

Continuar con **Task 3: Application — handler envío automático** del plan.

Archivo principal:
- `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/EnviarSolicitudKycCommand.cs`

Qué hacer:
1. Inyectar `IVerificadorIdentidadArgos` y `IPublicadorEventosIntegracion` en el handler.
2. Reordenar el flujo: validar invariants → guardar blobs → llamar ARGOS → aplicar `Aprobar`/`Rechazar` con `SistemaActor.Id` → publicar `UserVerified` si aprobó.
3. Si ARGOS falla, compensar eliminando blobs y devolver `ServicioVerificacionNoDisponible`.
4. Build de Application.
5. Commit.

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
