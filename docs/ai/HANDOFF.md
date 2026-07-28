# Handoff — KYC automático con ARGOS

> Fecha: 2026-07-28  
> Estado: diseño aprobado, listo para planificación.

## Contexto

CaseritoApp necesita usuarios con identidad verificada. El KYC actual es manual (admin aprueba/rechaza DNI + selfie). Se aprobó integrar ARGOS, un microservicio Python propio de reconocimiento facial (DeepFace/ArcFace), para automatizar la decisión.

## Spec aprobado

`docs/superpowers/specs/2026-07-28-kyc-argos-design.md`

Resumen del diseño:

- El usuario sube **foto del DNI + selfie** desde `KycPage.tsx`.
- El backend de Caserito (`EnviarSolicitudKycCommandHandler`) valida, guarda blobs cifrados y llama a `POST {ARGOS_URL}/api/verify` con ambas imágenes en base64.
- Si ARGOS responde `verified: true`, la solicitud se aprueba automáticamente (`Aprobada`) y se publica `UserVerified`.
- Si responde `verified: false` o no detecta rostro, se rechaza con motivo.
- Si ARGOS no responde, se devuelve 503 sin persistir la solicitud.
- Se registra el `ScoreSimilitud` en `SolicitudKyc` para auditoría.
- La aprobación automática usa un actor de sistema (`SistemaActor.Id`) en lugar de un admin humano.

## Proyecto ARGOS

Ubicación local: `/c/Users/lrcahuana/source/repos/dev/ARGOS`

- Endpoints relevantes: `POST /api/verify` (comparación 1:1), `POST /api/extract-embedding`, `POST /api/compare-embeddings`.
- Grafo Graphify ya generado en `ARGOS/graphify-out/`.
- Variables de configuración esperadas por Caserito: `Argos:Url`, `Argos:ApiKey` (mapeado a header `X-Service-Key`).

## Siguiente paso

Invocar el skill `superpowers:writing-plans` y crear el plan de implementación en:

`docs/superpowers/plans/YYYY-MM-DD-kyc-argos.md`

El plan debe descomponer el trabajo en tareas secuenciales y verificables: domain, application, infrastructure, host, frontend, tests.

## Restricciones importantes

- No escribir código de implementación en la sesión de planificación.
- Respetar `AGENTS.md` raíz, `CaseritoApp/AGENTS.md` y `web/AGENTS.md`.
- Clean Architecture / CQRS-lite: domain puro, puertos en Application, adaptadores en Infrastructure.
- Anti-PII: nunca loguear bytes de imágenes, base64, embeddings ni datos del DNI.
- ARGOS es un servicio interno; no llamarlo desde el frontend.
- Fail-fast: fuera de `Development`/`Testing`, Caserito no debe arrancar sin `Argos:Url`.
- No hacer push/merge sin autorización explícita.
- Priorizar economía de tokens: si el plan es extenso, dividir en sesiones nuevas.

## Verificaciones esperadas por paso

- Backend: `dotnet build CaseritoApp.sln`, `dotnet test CaseritoApp.sln`, `dotnet format CaseritoApp.sln --verify-no-changes`.
- Frontend: `npm run typecheck`, `npm run lint`, `npm run test -- --run`, `npm run build`.

## Notas

- ARGOS no hace liveness ni OCR; eso queda como follow-up futuro.
- El umbral de verificación se delega al default de ARGOS (`0.68` distancia coseno) en este bloque.
