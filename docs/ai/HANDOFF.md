# Handoff de sesión

## Objetivo

Completar Chat 3C según el spec y plan activos, con TDD estricto y sin ampliar
el alcance a Chat 3D o Fase 4.

## Estado verificado al iniciar

- Rama y upstream: `feat/chat-3c-seguridad-moderacion` y
  `origin/feat/chat-3c-seguridad-moderacion`.
- Worktree inicial limpio.
- Punta local y remota inicial: `b482a62905c88424065b6db1e3aa7c23b5ad3e16`.
- Tarea 6 cerrada en `91e5d3d feat(chat): implementa workflow de reportes`.
- No se hizo merge, rebase ni eliminación de ramas.

## Documentos activos

- `docs/superpowers/specs/2026-07-20-chat-3c-seguridad-moderacion-design.md`
- `docs/superpowers/plans/2026-07-20-chat-3c-seguridad-moderacion.md`

## Avance parcial de la tarea 7

- Se añadió y observó rojo causal en
  `ChatFlujoTests.Reportar_conversacion_participante_devuelve_201`: la ruta
  devolvía `404`.
- Se publicó el contrato mínimo
  `POST /api/chat/conversaciones/{id}/reportes`, sin identificador de usuario
  objetivo, conectado a `ReportarChatCommand`; devuelve `201` con solo `id`.
- Ese test dirigido quedó verde: 1/1.
- Se añadió y observó rojo causal en
  `ChatFlujoTests.Cerrar_conversacion_participante_devuelve_204`: la ruta
  devolvía `404`.
- Se publicó `PUT /api/chat/conversaciones/{id}/cierre`, conectado a
  `CerrarConversacionCommand`; el test dirigido quedó verde: 1/1.
- Se corrigieron cuatro literales con mojibake preexistente en los comandos de
  cierre y reapertura.
- La tarea 7 sigue incompleta y no corresponde usar todavía su commit final
  `feat(chat): publica endpoints de seguridad y moderacion`.

## Continuación exacta

Próximo test rojo exacto:

`ChatFlujoTests.Reabrir_conversacion_participante_devuelve_204`

Debe crear y cerrar una conversación, enviar
`DELETE /api/chat/conversaciones/{id}/cierre` como participante y afirmar
`204`. Rojo causal esperado: la ruta todavía devuelve `404`.

Después continuar un comportamiento y un rojo por vez con bloqueo/desbloqueo,
variantes de reporte (mensaje y contraparte derivada), matriz `401/404/409`,
moderación administrativa completa, rate limit de baja frecuencia y OpenAPI.
No avanzar a la tarea 8 antes de cerrar, probar, commitear y publicar la 7.
