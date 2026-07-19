# Workflow neutral de desarrollo

Usar este proceso para una feature, bloque de fase, cambio arquitectónico o
trabajo de riesgo medio/alto. Escalar la ceremonia al riesgo: una corrección
pequeña y totalmente definida no necesita documentos nuevos.

## 0. Preparación

1. Comprobar rama, worktree y últimos commits.
2. Identificar el bloque y crear una rama dedicada.
3. Localizar specs/planes relacionados por nombre; no leer históricos en masa.
4. Registrar supuestos y detectar decisiones que cambiarían el alcance.

## 1. Brainstorming

- Explorar arquitectura, comportamiento existente y precedentes cercanos.
- Hacer una pregunta por vez, preferiblemente con opciones y recomendación.
- Resolver alcance, modelo de dominio, permisos, estados, UI, auditoría,
  seguridad, migración, compatibilidad y trabajo diferido.
- Presentar un diseño coherente y pedir aprobación antes de implementar.

No escribir código durante esta fase. Cuando el usuario apruebe, crear:

```text
docs/superpowers/specs/AAAA-MM-DD-<tema>-design.md
```

El spec documenta objetivo, no objetivos, decisiones, flujos, errores,
seguridad/PII, persistencia, UI, pruebas y criterios de aceptación. Commit
separado para el spec.

## 2. Plan

Crear:

```text
docs/superpowers/plans/AAAA-MM-DD-<tema>.md
```

El plan debe estar ordenado por dependencias y contener tareas pequeñas con:

- entradas y salidas;
- rutas exactas;
- interfaces que produce o consume;
- comportamiento y código suficientemente completo para evitar decisiones
  ocultas durante la ejecución;
- prueba roja esperada;
- comando de verificación y resultado esperado;
- commit previsto.

Revisar que el plan cubra el spec sin agregar alcance. Commit separado.

## 3. Ejecución TDD

Ejecutar una tarea a la vez:

1. Escribir el test más pequeño que describa el comportamiento.
2. Ejecutarlo y observar el fallo correcto.
3. Implementar lo mínimo para hacerlo pasar.
4. Ejecutar el test dirigido y observar verde.
5. Ejecutar verificaciones vecinas proporcionales al riesgo.
6. Autorrevisar: spec, simplicidad, capas, PII, concurrencia y errores.
7. Corregir hallazgos antes de continuar.
8. Commit lógico y pequeño.

No mezclar refactors no relacionados. Si aparece una decisión nueva que cambia
el diseño, detenerse y pedir aprobación; actualizar spec/plan después.

## 4. Integración

- Regenerar artefactos derivados: migraciones, OpenAPI, tipos o clientes.
- Añadir pruebas de integración para contratos y flujos críticos.
- Ejecutar suites completas al final, no después de cada edición.
- Verificar formato, lint, typecheck y build aplicables.
- Si una dependencia externa impide probar, informar el comando, error causal y
  alcance no verificado. No disfrazarlo como éxito.

## 5. Revisión final

Revisar el diff completo contra el spec:

- comportamiento y estados;
- límites de bounded contexts/capas;
- autorización y anti-PII;
- migración y compatibilidad;
- contrato backend/frontend;
- cobertura y ausencia de trabajo extra;
- documentación y artefactos generados;
- `git diff --check` y worktree limpio.

## 6. Cierre

Entregar un resumen con:

- resultado funcional;
- commits/rama;
- verificaciones y cantidades;
- limitaciones o pruebas pendientes;
- siguiente acción que requiere autorización.

No mergear ni pushear salvo autorización explícita. Si la sesión termina antes,
crear o reemplazar `docs/ai/HANDOFF.md` usando la plantilla; eliminarlo cuando el
trabajo quede completamente cerrado para que no se convierta en memoria obsoleta.
