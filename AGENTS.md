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

1. Empezar con `git status --short --branch`, `git log -5 --oneline` y `rg --files`.
2. Buscar con `rg` antes de abrir archivos completos.
3. Leer solo el spec, plan y código vinculados con la tarea actual.
4. Ampliar contexto únicamente cuando exista una duda concreta.
5. No pegar logs o diffs extensos en el chat; resumir el error causal.

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
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
