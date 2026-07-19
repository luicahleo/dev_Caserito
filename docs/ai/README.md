# Trabajo con agentes de IA

Este directorio define un proceso neutral para Codex, Claude, Gemini y futuros
agentes. La información se divide por frecuencia para no pagar contexto inútil.

## Capas

1. `AGENTS.md`: reglas esenciales, cargadas siempre.
2. `CaseritoApp/AGENTS.md` y `web/AGENTS.md`: reglas locales, solo al trabajar
   en esos árboles.
3. `WORKFLOW.md`: proceso completo para features, fases y cambios complejos.
4. Specs y planes: decisiones de un bloque concreto.
5. `HANDOFF.md`: estado efímero de una sesión incompleta, si existe.

## Qué leer al iniciar

| Tipo de tarea | Contexto inicial |
|---|---|
| Pregunta o diagnóstico | `AGENTS.md` + archivos directamente relevantes |
| Cambio pequeño | Lo anterior + tests vecinos |
| Feature/bloque | `AGENTS.md` + `WORKFLOW.md` + spec actual |
| Continuación | Lo anterior + `HANDOFF.md`, validado contra Git |

Nunca cargar en bloque todos los specs y planes históricos. Buscar primero por
nombre, símbolo o dependencia y abrir solamente los resultados relevantes.

## Compatibilidad

- Codex descubre `AGENTS.md` jerárquicamente.
- Claude carga `CLAUDE.md`; los adaptadores raíz y locales importan el núcleo
  compartido correspondiente.
- Gemini CLI carga `GEMINI.md`; los adaptadores raíz y locales importan el
  núcleo compartido correspondiente.
- Otros agentes deben recibir `AGENTS.md` como archivo de instrucciones.

Las capacidades particulares del proveedor —memoria, hooks, subagentes o
comandos— son optimizaciones opcionales. Nunca deben ser necesarias para
entender o ejecutar el proceso del proyecto.
