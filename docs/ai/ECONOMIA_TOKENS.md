# Economía de tokens en sesiones con agentes

> Pautas para reducir el consumo de contexto y quota sin sacrificar calidad.
> Aplica a toda sesión con agentes de IA en este proyecto.

## 1. Elegir el modelo según la tarea

La regla se expresa por **nivel de capacidad**, no por nombre de modelo, para que
valga con cualquier proveedor. El criterio es siempre el mismo: usar el nivel más
económico que mantenga la calidad, y subir solo cuando la tarea exija criterio.

| Tipo de trabajo | Nivel |
|---|---|
| Brainstorming, diseño, refactor arquitectónico, decisiones de alcance, diagnóstico de fallos no evidentes | **Alto** |
| Desarrollo rutinario de features, fixes, tests, escritura de documentación | **Intermedio** |
| Ejecución de un plan que ya trae el código escrito, exploración, búsquedas, cambios repetitivos | **Económico** |

Equivalencias por proveedor:

| Nivel | Kimi | Claude | Codex |
|---|---|---|---|
| Alto | `k3` | Opus | El razonamiento más alto disponible |
| Intermedio | `k3-256k` | Sonnet | Razonamiento medio |
| Económico | `kimi-for-coding` | Haiku | Razonamiento bajo |

**No bajar de nivel** en tareas que tocan configuración de build o de
verificación —`Directory.Build.props`, `.editorconfig`, `.csproj`, workflows de
CI, scripts de `quality/`—, aunque parezcan mecánicas. Un error ahí no rompe nada
visible pero puede invalidar silenciosamente una verificación, y el ahorro no
compensa el riesgo.

En muchos harnesses el agente **no puede cambiar de modelo en caliente**: la
elección la hace el usuario antes de iniciar la sesión.

## 2. Delegar en subagentes con un nivel más barato

Cuando el harness permita fijar el modelo de un subagente, asignarle un nivel
inferior al del agente principal para:

- exploración de codebase;
- ejecución de tareas de un plan que ya trae el código escrito;
- búsquedas puntuales;
- revisiones de calidad paralelas.

Esto ahorra quota del modelo principal en trabajo que no requiere su capacidad
máxima. La excepción es la misma de la sección anterior: las tareas que tocan
configuración de build o de verificación conservan el nivel del agente principal.

Al ejecutar un plan por tareas, conviene declarar el nivel de cada una por
adelantado, en el propio plan o en el prompt de arranque, en lugar de decidirlo
sobre la marcha.

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
