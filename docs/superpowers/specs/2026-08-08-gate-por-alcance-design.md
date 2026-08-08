# Gate de calidad por alcance — diseño

## Objetivo

Reducir el coste de verificar cambios pequeños antes de cada commit sin debilitar
la revisión general previa a integrar. El modo nuevo se invoca con `-Changed` en
PowerShell o `--changed` en POSIX/Node.

## Decisiones

- `verify` y `verify --full` conservan su comportamiento actual.
- El modo por alcance inspecciona cambios pendientes respecto de `HEAD`, incluidos
  archivos versionados, no versionados y renombrados.
- Cambios bajo `web/` ejecutan los gates web; cambios bajo `CaseritoApp/` ejecutan
  los gates .NET.
- Infraestructura transversal (`quality/`, wrappers, configuración raíz, CI y
  reglas de agentes) ejecuta ambos grupos.
- Cambios exclusivamente documentales ejecutan solo los gates comunes.
- Si no hay cambios pendientes, el comando falla de forma explícita para evitar
  una verificación vacía que parezca exitosa.
- `--changed` y `--full` son incompatibles.
- Vitest limita la concurrencia a dos workers para evitar que la contención
  haga superar el timeout a tests que pasan de forma aislada. No se modifican
  timeouts, exclusiones ni aserciones.

## Seguridad y compatibilidad

No se registran contenidos de archivos ni datos de formularios: solo rutas Git.
El gate general continúa siendo obligatorio antes de integrar y el completo antes
de producción o cuando lo exija el workflow.

## Criterios de aceptación

1. La clasificación pura cubre frontend, backend, ambos, documentación y vacío.
2. Los wrappers exponen el nuevo modo y rechazan combinaciones incompatibles.
3. El orquestador omite gates fuera del alcance sin alterar los modos existentes.
4. La documentación prescribe `Changed` antes de commit y el gate general antes
   de integrar.
5. La suite web completa pasa sin timeouts causados por exceso de workers.
