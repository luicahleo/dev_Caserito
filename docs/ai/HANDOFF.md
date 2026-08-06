# Handoff de sesión

## Objetivo

Cerrar los cabos sueltos de la sesión de observabilidad + diagnóstico KYC
(2026-08-06): verificar el deploy actual y las respuestas pendientes del
agenteVPS.

## Rama y estado de Git

- Solo existe `master` (local y remoto); el usuario pidió explícitamente no
  dejar ramas pendientes. Flujo acordado: rama → merge a master → push →
  borrar rama.
- `master` está pusheado hasta `ceac925` (fix KYC rostro no detectado).

## Completado (todo mergeado y pusheado el 2026-08-06)

1. Observabilidad base front-back (rama feat/observabilidad-front-back).
2. Diagnóstico de flujo frontend: `?debug=1`, buffer de 100 eventos
   (`flow.navigation`/`flow.api_call`), exportación JSON, `X-Session-Id`,
   contrato backend `sessionId`+`flowEvents`, logs `frontend.flow`.
   Spec: `docs/superpowers/specs/2026-08-06-diagnostico-flujo-frontend-design.md`.
3. Enmienda: breadcrumbs se capturan siempre en memoria y solo se envían
   adjuntos a errores (últimos 30). Debug sigue existiendo solo para
   exportación manual.
4. Fix KYC: `Kyc.RostroNoDetectado` (422) distinguible de
   `Kyc.ServicioVerificacionNoDisponible` (503); el adaptador tolera el 500
   con payload conocido del ARGOS actual y el 422 estructurado futuro.
   Mensaje accionable en `KycPage`.

## Decisiones aprobadas

- Compensación de `EnviarSolicitudKycCommand` no cambia: foto sin rostro → no
  persiste nada, el usuario reintenta.
- Contrato ARGOS fix A confirmado al agenteVPS: 422 +
  `{success, code: "face-not-detected", image: "img1"|"img2", error}`.
- Soporte a clientes será solo vía formulario → el error debe llegar con
  contexto (de ahí la enmienda de breadcrumbs).

## Pendiente externo (agenteVPS, docs 24-27 en
`preguntasrespuestasCaseritoApp_AgenteLocal_AgenteVPS/`)

1. Re-consultar `[identity].[AspNetUsers]` con `Nombres`/`Apellidos` (la
   consulta anterior usó la columna legacy `Nombre` → falso positivo casi
   seguro).
2. Aplicar fix A en ARGOS (`/app/ARGOS/views.py`) cuando pueda; independiente
   del deploy de CaseritoApp.

## Deuda registrada (sin plan aún)

- Migración contract para eliminar columnas legacy `Nombre`/`Ciudad` de
  `[identity].[AspNetUsers]`.
- `Argos__ApiKey` puede eliminarse de la config (ARGOS no la valida).
- Retención de logs: ~50 MB por rotación json-file y se pierde al recrear el
  contenedor; evaluar Loki/Seq o volumen si se necesita histórico.
- Posible activación alternativa del modo debug dentro de la PWA/app
  Capacitor (hoy solo `?debug=1` en navegador) y descarga del JSON vía
  plugin Filesystem en la app.

## Próximo paso

1. Verificar en GitHub Actions que CI + deploy del push `ceac925` terminaron
   en verde.
2. Reprobar KYC en https://caserito.app con fotos con rostro: `POST /api/kyc`
   debe dar 204 y crear fila `Pendiente` en `[identity].[SolicitudesKyc]`;
   con foto sin rostro debe dar 422 con el mensaje nuevo.
3. Esperar/integrar la respuesta del agenteVPS (doc 28 esperado).

## Cómo diagnosticar en producción

- Usuario reporta `ERR-XXXX` → `docker logs caseritoapp | grep ERR-XXXX` →
  `grep SES-XXXX` para el flujo completo.
- Debug manual: `https://caserito.app/?debug=1` → botón flotante "Descargar
  diagnóstico" → el JSON se pega al agente.
- `gh` CLI tiene credenciales caducadas (401); renovar con `gh auth login`
  si se quiere consultar Actions desde terminal.

## Verificaciones ejecutadas (última sesión)

- Backend: 379 unit + 85 arquitectura + 236 integración (suite completa) y
  formato, todo verde; KYC fix: 16 tests integración dirigidos.
- Frontend: typecheck, lint, 224 tests, build — todo verde.
- OpenAPI + `schema.d.ts` regenerados tras cada cambio de contrato.
