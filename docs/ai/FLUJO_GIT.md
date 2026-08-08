# Flujo Git y despliegue

## Desarrollo habitual

1. Crear una rama de trabajo desde `develop`.
2. Implementar y verificar el cambio.
3. Publicar la rama, abrir un pull request hacia `develop` y esperar el CI verde.
4. Fusionar el pull request y borrar la rama de trabajo.
5. En PC2, actualizar `develop` y levantar el entorno local:

   ```powershell
   git switch develop
   git pull --ff-only origin develop
   .\iniciar-pc2.ps1
   ```

Los cambios de `develop` nunca despliegan producción.

## Promoción a producción

Solo se realiza cuando el usuario lo pide expresamente:

1. Ejecutar la puerta de calidad completa sobre `develop`.
2. Abrir un pull request de `develop` hacia `master` y fusionarlo con CI verde.
3. Esperar a que el workflow `CI` del commit de `master` termine en verde.
4. Ejecutar manualmente `Deploy` desde la rama `master` y escribir
   `PRODUCCION` como confirmación.

El workflow rechaza ramas distintas de `master` y commits sin un CI de `push`
exitoso para el mismo SHA.

## Protecciones remotas

- `develop` es la rama predeterminada y exige CI antes de integrar cambios.
- `master` solo acepta promociones mediante pull request y exige CI.
- El entorno `production` admite únicamente despliegues desde `master`.
