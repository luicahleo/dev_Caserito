# CaseritoApp — instrucciones para agentes

Fuente neutral para Codex, Claude, Gemini y otros agentes. Mantener este archivo
corto: contiene solo reglas que aplican a casi cualquier tarea.

## Prioridades

- Seguir el pedido explícito del usuario y no ampliar el alcance sin autorización.
- Textos de UI y comentarios en español, con acentos y UTF-8; nunca mojibake.
- Anti-PII no negociable: nunca registrar documentos, imágenes, tokens, pagos,
  credenciales, texto de reportes ni otros datos sensibles. Usar errores genéricos.
- Flujo git autorizado de forma permanente (aprobado 2026-08-06): al terminar
  un cambio verificado, hacer commit → merge a master → push → borrar la rama,
  sin pedir confirmación cada vez. No dejar ramas pendientes.
- Preservar cambios ajenos y evitar operaciones destructivas.

## Descubrimiento eficiente

1. Empezar solo con `git status --short --branch` y `git log -5 --oneline`.
2. No ejecutar listados recursivos ni `rg --files` sin filtros al iniciar.
3. Buscar por término, símbolo o ruta probable con `rg`; limitar primero a
   30 archivos o 100 líneas y refinar antes de ampliar.
4. Leer fragmentos antes que archivos completos. Abrir solo el spec, plan,
   código y tests vinculados con la tarea actual.
5. Revisar primero `git diff --stat` y después solo los diffs relevantes.
6. Resumir logs y errores por causa, archivo y línea; no pegar salidas extensas.

No releer todos los specs históricos. `docs/superpowers/specs/` y
`docs/superpowers/plans/` son memoria consultable, no contexto obligatorio.

## Selección de proceso

- Pregunta, explicación o diagnóstico: investigar y responder; no modificar.
- Cambio pequeño y claro: implementar, probar proporcionalmente y resumir.
- Feature, bloque de fase o cambio arquitectónico: seguir
  `docs/ai/WORKFLOW.md` desde brainstorming hasta cierre.
- Continuación de otra sesión: leer primero `docs/ai/HANDOFF.md` si existe y
  verificar cada afirmación importante con Git o los archivos actuales.

## Proyecto

- Backend: .NET, Clean Architecture por bounded context, CQRS-lite con
  MediatR + Result + FluentValidation, versiones centralizadas y
  warnings-as-errors. Ejecutar comandos desde `CaseritoApp/`.
- Frontend: React + TypeScript estricto, MUI, React Router, TanStack Query y
  cliente OpenAPI generado. Ejecutar comandos desde `web/`.
- Integración backend usa Testcontainers.MsSql y requiere Docker.
- Las reglas locales de `CaseritoApp/AGENTS.md` y `web/AGENTS.md` complementan
  este archivo cuando se trabaja en esos árboles.

## Verificación

- Durante TDD, ejecutar el test dirigido; suite completa al integrar o cerrar.
- Backend: build, test y `dotnet format --verify-no-changes` según el riesgo.
- Frontend: typecheck, lint, test y build al integrar rutas/contrato.
- Informar pruebas no ejecutadas y el motivo; nunca afirmar verde sin evidencia.

## Economía de contexto

- Una feature o bloque por sesión.
- Preferir sesiones nuevas con handoff breve frente a historiales largos.
- Usar el nivel de modelo y razonamiento más económico que mantenga la calidad;
  elevarlo ante decisiones de criterio. No bajarlo en cambios de configuración de
  build o verificación, aunque parezcan mecánicos.
- No usar subagentes salvo trabajo verdaderamente independiente que compense el
  coste adicional.
- Guardar decisiones duraderas en specs; no convertir el chat en documentación.
- Seguir las pautas de `docs/ai/ECONOMIA_TOKENS.md` para modelo, sesiones y
  exploración eficiente.

Mapa de documentación y plantillas: `docs/ai/README.md`.

## graphify

Este proyecto tiene un grafo de conocimiento en `graphify-out/` con nodos, comunidades y relaciones entre archivos. El grafo ya está construido (`graphify-out/graph.json`, ~8 MB, 4319 nodos / 9884 aristas / 258 comunidades) y puede usarse para responder preguntas sobre el codebase ahorrando tokens en búsquedas amplias.

Graphify es **opcional**: usarlo cuando `graphify-out/graph.json` exista y sea usable; si no, continuar directamente con búsquedas puntuales (`rg`).

Cuando el usuario escriba `/graphify`, seguir las instrucciones del skill o comando instalado antes de cualquier otra acción.

Comandos útiles:
- `graphify query "<pregunta>"` — responder preguntas sobre el código.
- `graphify path "<A>" "<B>"` — encontrar relaciones entre dos símbolos/archivos.
- `graphify explain "<concepto>"` — explicar un concepto del proyecto.
- `graphify . --code-only` — reconstruir el grafo completo solo con código (no requiere API key).
- `graphify cluster-only .` — regenerar comunidades y `GRAPH_REPORT.md`.
- `graphify update .` — actualizar el grafo después de modificar código (solo si Graphify está configurado y disponible).

Reglas:
- Usar el grafo para preguntas amplias o de arquitectura; seguir usando `rg` para búsquedas exactas de texto o símbolos cuando sea más directo.
- Los archivos sucios en `graphify-out/` son normales después de hooks o actualizaciones incrementales; no son motivo para ignorar graphify. Solo omitir graphify si la tarea trata sobre output obsoleto/incorrecto, o el usuario lo pide explícitamente.
- Si existe `graphify-out/wiki/index.md`, usarlo para navegación general en lugar de recorrer el código fuente directamente.
- No leer `GRAPH_REPORT.md` ni recorrer `graphify-out/` como fallback cuando el grafo falte o esté incompleto.
- El grafo actual se generó en modo `--code-only`; no incluye documentos ni imágenes. Para indexar también docs, configurar una API key (p. ej. `GOOGLE_API_KEY`) y ejecutar `graphify .` sin `--code-only`.
