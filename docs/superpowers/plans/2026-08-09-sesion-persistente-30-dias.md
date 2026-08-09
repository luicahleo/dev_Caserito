# Plan de implementación: sesión persistente durante 30 días

Especificación:
`docs/superpowers/specs/2026-08-09-sesion-persistente-30-dias-design.md`.

## Tarea 1: fijar el contrato temporal mediante pruebas

**Archivos:**

- Modificar
  `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthFlowTests.cs`.
- Modificar
  `CaseritoApp/tests/CaseritoApp.IntegrationTests/RefreshTokensTests.cs`.

**Trabajo:**

1. Exigir que la cookie emitida por login contenga `Max-Age=2592000`.
2. Añadir una prueba de refresh que compruebe que la cookie rotada recupera el
   mismo `Max-Age` completo.
3. Comprobar que el registro de refresh token emitido expira 30 días después de
   `CreadoEn`.
4. Comprobar la misma ventana en el token nuevo producido por una rotación.

**Rojo esperado:** la cookie no contiene `Max-Age` y los tokens persistidos
expiran actualmente a los 7 días.

**Verificación:** desde `CaseritoApp/`, ejecutar los filtros dirigidos de
`AuthFlowTests` y `RefreshTokensTests`; deben fallar por la vigencia actual.

## Tarea 2: unificar y aplicar la vigencia

**Archivos:**

- Modificar
  `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ServicioRefreshTokens.cs`.
- Modificar
  `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`.

**Trabajo:**

1. Exponer una única constante de infraestructura `VigenciaSesion` equivalente
   a 30 días.
2. Usarla al emitir y rotar los refresh tokens persistidos.
3. Usarla como `MaxAge` de la cookie de refresh emitida por login y refresh.
4. Mantener sin cambios sus flags, path, borrado, rotación y revocación.

**Verde esperado:** los filtros dirigidos pasan sin cambios de esquema ni
migraciones.

**Verificación:** ejecutar las pruebas dirigidas de la tarea 1.

## Tarea 3: integrar y cerrar

1. Ejecutar las pruebas vecinas de autenticación y restablecimiento.
2. Ejecutar `dotnet build CaseritoApp.sln` y
   `dotnet format CaseritoApp.sln --verify-no-changes` si no quedan cubiertos por
   la puerta.
3. Revisar el diff, `git diff --check` y ausencia de PII.
4. Ejecutar `./verify.ps1 -Changed` desde la raíz.
5. Crear y publicar en `develop` el commit
   `fix(auth): mantiene la sesión durante 30 días`.
