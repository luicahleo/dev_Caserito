# Handoff de sesión

## Objetivo

Completar Chat 3C según el spec y plan activos, sin ampliar alcance a Chat 3D o Fase 4.

## Estado verificado al iniciar

- Rama: `feat/chat-3c-seguridad-moderacion`, con upstream homónimo en `origin`.
- Punta local y remota inicial:
  `a3c01851e3a31cd9a8cefef406718297f4bf65b6`.
- El worktree estaba limpio.
- No se hizo merge, rebase ni eliminación de ramas.
- Documentos activos:
  - `docs/superpowers/specs/2026-07-20-chat-3c-seguridad-moderacion-design.md`
  - `docs/superpowers/plans/2026-07-20-chat-3c-seguridad-moderacion.md`

## Avance parcial de la tarea 7

- Se agregó `ChatFlujoTests.Reportar_mensaje_de_la_conversacion_devuelve_201`.
  La implementación general existente lo dejó verde inmediatamente; se conserva
  como caracterización y comprueba `201` con cuerpo que contiene únicamente `id`.
- Se agregó
  `ChatFlujoTests.Reportar_contraparte_rechaza_identificador_de_usuario_objetivo`.
  El rojo causal fue `201` en lugar de `400`: System.Text.Json ignoraba el campo
  no mapeado `usuarioObjetivoId`.
- Se aplicó `JsonUnmappedMemberHandling.Disallow` únicamente a
  `ReportarChatRequest`; el test anterior quedó verde y el request no incorpora
  ningún identificador de usuario objetivo.
- Se agregó `ChatFlujoTests.Reportar_contraparte_derivada_devuelve_201`.
  Quedó verde y caracteriza que el reporte válido se crea sin enviar usuario
  objetivo, por lo que la contraparte continúa derivándose en servidor.
- La tarea 7 sigue incompleta; no usar aún el commit final
  `feat(chat): publica endpoints de seguridad y moderacion`.

## Tests ejecutados

- Verde inicial:
  `dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~ChatFlujoTests.Reportar_mensaje_de_la_conversacion_devuelve_201`
  (1/1).
- Rojo:
  `dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~ChatFlujoTests.Reportar_contraparte_rechaza_identificador_de_usuario_objetivo`
  (esperado `400`, real `201`).
- Verde del rojo anterior: mismo comando (1/1).
- Verde:
  `dotnet test tests/CaseritoApp.IntegrationTests --filter FullyQualifiedName~ChatFlujoTests.Reportar_contraparte_derivada_devuelve_201`
  (1/1).
- Verificación vecina de los tres tests nuevos: 3/3.
- `dotnet format CaseritoApp.sln` completó sin errores.
- `git diff --check` completó sin errores.

## Continuación exacta

Próximo test a escribir, uno solo:

`ChatFlujoTests.Reportar_recurso_inexistente_o_ajeno_devuelve_404_indistinguible`

Debe crear una conversación propia y otra ajena, intentar reportar un identificador
inexistente y la conversación ajena con el mismo request válido, afirmar `404` en
ambos casos y comprobar que sus `ProblemDetails` públicos son indistinguibles y
genéricos, sin reflejar identificadores ni argumentos.

Después continuar secuencialmente con `409` genérico, `401`, moderación
administrativa, rate limit y OpenAPI según la tarea 7. No avanzar a tarea 8 antes
de cerrar, probar, commitear y publicar la 7.

## Puntas

- Padre exacto del checkpoint: `a3c01851e3a31cd9a8cefef406718297f4bf65b6`.
- La punta local/remota final es el commit de checkpoint que contiene este
  handoff; verificar su hash exacto con `git rev-parse HEAD` y
  `git rev-parse '@{upstream}'` al retomar.
