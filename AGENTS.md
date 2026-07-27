# CaseritoApp — instrucciones para agentes

Fuente neutral para Codex, Claude, Gemini y otros agentes. Mantener este archivo
corto: contiene solo reglas que aplican a casi cualquier tarea.

## Prioridades

- Seguir el pedido explícito del usuario y no ampliar el alcance sin autorización.
- Textos de UI y comentarios en español, con acentos y UTF-8; nunca mojibake.
- Anti-PII no negociable: nunca registrar documentos, imágenes, tokens, pagos,
  credenciales, texto de reportes ni otros datos sensibles. Usar errores genéricos.
- No hacer push ni merge sin autorización explícita.
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
- Usar razonamiento bajo para tareas mecánicas y elevarlo solo ante complejidad.
- No usar subagentes salvo trabajo verdaderamente independiente que compense el
  coste adicional.
- Guardar decisiones duraderas en specs; no convertir el chat en documentación.

Mapa de documentación y plantillas: `docs/ai/README.md`.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- Graphify is optional. Use it only when `graphify-out/graph.json` exists and
  contains a usable graph; otherwise continue directly with scoped `rg`
  searches.
- For codebase questions, run `graphify query "<question>"` when the graph is
  usable. Use `graphify path "<A>" "<B>"` for relationships and
  `graphify explain "<concept>"` for focused concepts.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Do not read `GRAPH_REPORT.md` or traverse `graphify-out/` as a fallback when
  the graph is missing or incomplete.
- After modifying code, run `graphify update .` only when Graphify is configured
  and available.
