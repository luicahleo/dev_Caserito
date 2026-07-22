# Handoff de sesión

## Objetivo

Completar Chat 3C según el spec y plan activos, sin ampliar alcance a Chat 3D o Fase 4.

## Estado verificado

- Rama: `feat/chat-3c-seguridad-moderacion`, con upstream homónimo en `origin`.
- Base de esta sesión: `331aab76547e4337629267966fda432c8a570b52`.
- No se hizo merge, rebase ni eliminación de ramas.
- Documentos activos:
  - `docs/superpowers/specs/2026-07-20-chat-3c-seguridad-moderacion-design.md`
  - `docs/superpowers/plans/2026-07-20-chat-3c-seguridad-moderacion.md`

## Avance parcial de la tarea 7

- Se agregó `Reabrir_conversacion_participante_devuelve_204`. El rojo real fue
  `405` (la plantilla ya tenía `PUT`), no el `404` anticipado; se publicó
  `DELETE /api/chat/conversaciones/{id}/cierre` y quedó verde.
- Se agregó `Bloquear_contraparte_derivada_devuelve_204`; rojo `404`; se publicó
  `PUT /api/chat/conversaciones/{id}/bloqueo` sin request ni usuario objetivo y quedó verde.
- Se agregó `Desbloquear_contraparte_derivada_devuelve_204`; rojo `405`; se publicó
  `DELETE /api/chat/conversaciones/{id}/bloqueo` sin request ni usuario objetivo y quedó verde.
- Verificación vecina: los cinco tests dirigidos de reporte de conversación,
  cierre, reapertura, bloqueo y desbloqueo pasan (5/5).
- La tarea 7 sigue incompleta; no usar aún el commit final
  `feat(chat): publica endpoints de seguridad y moderacion`.

## Continuación exacta

Próximo test a escribir, uno solo:

`ChatFlujoTests.Reportar_mensaje_de_la_conversacion_devuelve_201`

Debe crear conversación y mensaje, enviar `POST /api/chat/conversaciones/{id}/reportes`
con `tipoObjetivo = Mensaje`, el `mensajeId`, categoría y detalle mínimo, y afirmar
`201` con cuerpo que contenga únicamente `id`. Si queda verde por la implementación
general existente, conservarlo como caracterización y elegir inmediatamente el rojo
mínimo de reporte de contraparte sin identificador objetivo.

Después completar secuencialmente la matriz `404/409/401`, moderación administrativa,
rate limit y OpenAPI. No avanzar a tarea 8 antes de cerrar, probar, commitear y publicar la 7.
