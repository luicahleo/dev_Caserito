# Economía de tokens en sesiones con agentes

> Pautas para reducir el consumo de contexto y quota sin sacrificar calidad.
> Aplica a toda sesión con agentes de IA en este proyecto.

## 1. Elegir el modelo según la tarea

Antes de iniciar la sesión, ajustar `default_model` en `~/.kimi-code/config.toml`:

| Tipo de sesión | Modelo recomendado | Razón |
|---|---|---|
| Planificación, diseño de fase, refactoring grande | `k3` | Necesita razonamiento profundo y contexto amplio. |
| Desarrollo rutinario de features, fixes, tests | `k3-256k` | Misma calidad que `k3` dentro de 256k y consume ~la mitad de quota. |
| Tareas mecánicas, exploración simple, búsquedas | `kimi-for-coding` | Suficiente para tareas repetitivas y es el más económico. |

El agente **no puede cambiar de modelo en caliente**. La elección la hace el usuario antes de iniciar la sesión.

## 2. Delegar en subagentes con modelo más barato

Si se habilita `secondary_model`, configurarlo con un modelo más económico (`k3-256k` o `kimi-for-coding`). El agente principal puede usarlo para subagentes de:

- exploración de codebase;
- ejecución de planes mecánicos;
- búsquedas puntuales;
- revisiones de calidad paralelas.

Esto ahorra quota del modelo principal en tareas que no requieren su capacidad máxima.

## 3. Sesiones cortas y enfocadas

- **Una feature o bloque por sesión.** No acumular varios temas en el mismo historial.
- Al cerrar una sesión, dejar `docs/ai/HANDOFF.md` breve si hay trabajo pendiente.
- No arrastrar conversaciones tangenciales dentro de una sesión de código.

## 4. Minimizar exploración innecesaria

Cada `Read`, `Grep` o `Glob` consume tokens. Reducir la exploración:

- Indicar al agente la ruta exacta si se la conoce.
- Pedir búsquedas específicas en lugar de "revisame todo".
- Evitar listados recursivos masivos o `find` sin filtros.
- Leer fragmentos, no archivos completos.

## 5. Aprovechar documentos aprobados

Si ya existe un spec o plan para la fase/feature, el agente debe usarlo y ejecutar directamente. No redescubrir el diseño en cada sesión. Esto es especialmente relevante para:

- `docs/superpowers/specs/`
- `docs/superpowers/plans/`
- `docs/ai/HANDOFF.md` en sesiones continuadas

## 6. Ser explícito con el alcance

Las vueltas de replanteamiento son las más costosas. Antes de que el agente escriba código, definir:

- qué fase o feature se toca;
- qué queda explícitamente fuera de alcance;
- preferencias de implementación o restricciones conocidas.

## 7. Evitar explicaciones extensas

Si solo se necesita la acción, decirlo upfront:

- "Solo ejecutá, no expliques."
- "Resumen de una línea."
- "No justifiques, solo el comando."

## 8. Usar el modo plan solo cuando corresponda

El Plan mode genera documentación extra y razonamiento previo extenso. Usarlo solo para:

- features nuevas;
- bloques de fase;
- cambios arquitectónicos de riesgo medio/alto.

Para cambios pequeños y bien definidos, no es necesario.

## 9. Reglas ya vigentes en AGENTS.md

Estas reglas complementan las de `AGENTS.md`:

- no releer todos los specs históricos;
- no ejecutar listados recursivos sin filtros al iniciar;
- preferir sesiones nuevas con handoff breve frente a historiales largos;
- usar subagentes solo cuando el trabajo sea verdaderamente independiente.

---

**Nota:** El modo `auto` acelera las aprobaciones de herramientas, pero no reduce el consumo de tokens por sí solo. El ahorro real viene de la combinación de modelo adecuado, sesiones enfocadas y exploración mínima.
