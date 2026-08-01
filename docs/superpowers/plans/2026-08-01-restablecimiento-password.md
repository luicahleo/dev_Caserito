# Restablecimiento de contraseña — Plan de implementación

**Objetivo:** implementar recuperación de contraseña con token de 30 minutos, respuesta anti-enumeración, revocación de sesiones y dos pantallas públicas.

**Arquitectura:** Identity Application orquesta los casos de uso mediante puertos; Infrastructure adapta `UserManager`, tokens, SMTP y refresh tokens; Host publica los endpoints y limita por IP; React consume el contrato OpenAPI generado. No hay migración.

**Spec:** `docs/superpowers/specs/2026-08-01-restablecimiento-password-design.md`

## Restricciones globales

- TDD estricto: prueba roja, implementación mínima, prueba verde y revisión antes de cada commit.
- Nunca registrar correo, usuario, contraseña, token, URL, payload ni cuerpo del correo.
- UI y comentarios en español UTF-8.
- No cambiar la confirmación de correo ni la política global de contraseñas.
- No persistir tokens de recuperación.
- No hacer push ni merge.

## Estructura prevista

| Ruta | Responsabilidad |
|---|---|
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/IRepositorioRestablecimientoPassword.cs` | Puerto para generar y consumir tokens sin filtrar Identity a Application. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/SolicitarRestablecimientoPasswordCommand.cs` | Caso de uso anti-enumeración y envío de correo. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/RestablecerPasswordCommand.cs` | Caso de uso de cambio y revocación de sesiones. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/IRevocadorSesionesUsuario.cs` | Puerto para invalidar todas las sesiones renovables. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/RepositorioRestablecimientoPassword.cs` | Adaptador sobre `UserManager<ApplicationUser>`. |
| `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ProveedorTokenRestablecimientoPassword.cs` | Proveedor Data Protection exclusivo de 30 minutos. |
| `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs` | Contratos y endpoints públicos. |
| `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs` | Políticas de rate limiting por IP. |
| `web/src/routes/OlvidePasswordPage.tsx` | Solicitud de recuperación. |
| `web/src/routes/RestablecerPasswordPage.tsx` | Lectura/limpieza del fragmento y contraseña nueva. |

---

## Tarea 1: proveedor exclusivo de token y repositorio Identity

**Archivos:**

- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/IRepositorioRestablecimientoPassword.cs`.
- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ProveedorTokenRestablecimientoPassword.cs`.
- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/RepositorioRestablecimientoPassword.cs`.
- Modificar `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/ProveedorTokenRestablecimientoPasswordTests.cs`.

**Interfaces producidas:**

```csharp
public sealed record SolicitudRestablecimiento(
    Guid UsuarioId, string Email, string Nombre, string Token);

public interface IRepositorioRestablecimientoPassword
{
    Task<SolicitudRestablecimiento?> CrearSolicitudAsync(string email, CancellationToken ct);
    Task<Result<Guid>> RestablecerAsync(
        Guid usuarioId, string token, string password, CancellationToken ct);
}
```

El adaptador busca por email, exige un email utilizable, llama a
`GeneratePasswordResetTokenAsync` y devuelve el DTO solo hacia el caso de uso. Para consumir,
busca por id y llama a `ResetPasswordAsync`; usuario/token inválidos se traducen al mismo
`Result` genérico. Ningún error contiene entradas recibidas.

**TDD:**

1. Escribir una prueba que compruebe que las opciones exclusivas tienen
   `TokenLifespan == TimeSpan.FromMinutes(30)`.
2. Ejecutar:
   `dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~ProveedorTokenRestablecimientoPasswordTests`
   y observar fallo de compilación/tipo ausente.
3. Implementar `OpcionesTokenRestablecimientoPassword : DataProtectionTokenProviderOptions` y
   `ProveedorTokenRestablecimientoPassword : DataProtectorTokenProvider<ApplicationUser>`.
4. En `AddIdentityCore`, asignar un nombre constante exclusivo a
   `opciones.Tokens.PasswordResetTokenProvider`; registrarlo con `AddTokenProvider` sin tocar el
   proveedor de confirmación.
5. Registrar el repositorio como scoped y ejecutar nuevamente la prueba; esperado: verde.
6. Ejecutar build de Infrastructure.

**Commit previsto:** `feat(auth): configura tokens de recuperación de 30 minutos`

---

## Tarea 2: plantilla y solicitud anti-enumeración

**Archivos:**

- Modificar `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Correo/IPlantillaCorreo.cs`.
- Modificar `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Correo/PlantillaCorreoTextoPlano.cs`.
- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/SolicitarRestablecimientoPasswordCommand.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/SolicitarRestablecimientoPasswordHandlerTests.cs`.
- Modificar `CaseritoApp/tests/CaseritoApp.UnitTests/Correo/PlantillaCorreoTextoPlanoTests.cs`.
- Ajustar fakes de `IPlantillaCorreo` existentes por el nuevo contrato.

**Interfaces producidas:**

```csharp
public sealed record SolicitarRestablecimientoPasswordCommand(string Email) : ICommand;
```

`IPlantillaCorreo` incorpora asunto y cuerpo de recuperación. El cuerpo recibe la URL completa;
la construye con `OpcionesApp.UrlPublica`, ruta `/restablecer-password` y fragmento con
`usuarioId` y `token` codificados mediante `Uri.EscapeDataString`.

**TDD:**

1. Probar estos comportamientos del handler:
   - cuenta existente: envía un correo cuyo enlace usa el fragmento;
   - cuenta inexistente: devuelve éxito y no envía;
   - SMTP lanza excepción: devuelve éxito;
   - el log de error no contiene email, token, URL ni valores del comando.
2. Probar que la plantilla contiene el enlace, indica 30 minutos y no incluye contraseña.
3. Ejecutar el filtro `FullyQualifiedName~SolicitarRestablecimientoPassword` y observar rojo.
4. Implementar lo mínimo. Usar un único `LoggerMessage` técnico sin parámetros sensibles.
5. Ejecutar los tests dirigidos de Auth y Correo; esperado: verdes.

**Commit previsto:** `feat(auth): envía correo de recuperación sin enumerar cuentas`

---

## Tarea 3: revocación total de refresh tokens

**Archivos:**

- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/IRevocadorSesionesUsuario.cs`.
- Modificar `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ServicioRefreshTokens.cs`.
- Modificar `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` si el puerto requiere registro adicional.
- Modificar `CaseritoApp/tests/CaseritoApp.IntegrationTests/RefreshTokensTests.cs`.

**Interfaz producida:**

```csharp
public interface IRevocadorSesionesUsuario
{
    Task RevocarTodasAsync(Guid usuarioId, CancellationToken ct);
}
```

`ServicioRefreshTokens` implementa el puerto con un `ExecuteUpdateAsync` que asigna
`RevocadoEn = TimeProvider.GetUtcNow()` solo a tokens activos del usuario indicado.

**TDD:**

1. Añadir un test que emita dos tokens al usuario A y uno al B, revoque A y compruebe que solo
   los dos primeros dejan de rotar.
2. Ejecutar el test dirigido y observar rojo por operación ausente.
3. Implementar la actualización única y registrar el puerto.
4. Repetir el test; esperado: verde.

**Commit previsto:** `feat(auth): revoca todas las sesiones de un usuario`

---

## Tarea 4: comando para restablecer contraseña

**Archivos:**

- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Auth/RestablecerPasswordCommand.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/RestablecerPasswordHandlerTests.cs`.

**Interfaz producida:**

```csharp
public sealed record RestablecerPasswordCommand(
    Guid UsuarioId, string Token, string Password) : ICommand;
```

El handler llama primero al repositorio. Solo si el resultado es exitoso llama a
`IRevocadorSesionesUsuario.RevocarTodasAsync`. Devuelve un error público único para usuario
inexistente, token inválido, usado o caducado. La contraseña nunca forma parte del error.

**TDD:**

1. Probar éxito + revocación una vez.
2. Probar fallo del repositorio + ausencia de revocación.
3. Probar que error y logs no contienen token ni contraseña.
4. Ejecutar el test dirigido y observar rojo.
5. Implementar el handler mínimo y repetir; esperado: verde.

**Commit previsto:** `feat(auth): restablece contraseña y cierra sesiones`

---

## Tarea 5: endpoints, validación y rate limiting

**Archivos:**

- Modificar `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`.
- Modificar `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.IntegrationTests/RestablecimientoPasswordTests.cs`.

**Contratos producidos:**

```csharp
public sealed record OlvidePasswordRequest(string Email);
public sealed record RestablecerPasswordRequest(Guid UsuarioId, string Token, string Password);
```

- `POST /api/auth/forgot-password`: envía el comando y siempre traduce el resultado funcional a
  `204`; política `auth-forgot-password`, 3/hora/IP.
- `POST /api/auth/reset-password`: `204` en éxito, `400 ProblemDetails` genérico en fallo;
  política `auth-reset-password`, 10/15 minutos/IP.
- Ambos endpoints declaran `429` y sus contratos OpenAPI.

**TDD de integración:**

1. Crear usuario, generar una solicitud y comprobar `204`.
2. Solicitar un correo inexistente y comprobar el mismo `204`.
3. Obtener un token con `UserManager` en el scope de prueba, restablecer y comprobar:
   - contraseña anterior rechazada;
   - contraseña nueva aceptada;
   - `EmailConfirmed` no cambia;
   - segundo uso del token devuelve `400`.
4. Generar dos tokens antes del cambio y comprobar que el segundo queda inválido después del
   primero.
5. Iniciar sesión antes del cambio y comprobar que su refresh token ya devuelve `401`.
6. Probar token/usuario inválidos con el mismo `400` público.
7. Probar 4.ª solicitud y 11.º consumo desde clientes aislados/particiones controladas; esperado
   `429`, sin contaminar otros tests.
8. Ejecutar el test dirigido con Docker; observar rojo, implementar endpoints/políticas y repetir
   hasta verde.

**Commit previsto:** `feat(auth): expone recuperación de contraseña protegida`

---

## Tarea 6: OpenAPI y cliente frontend

**Archivos:**

- Modificar artefacto OpenAPI generado bajo `CaseritoApp/artifacts/openapi/` mediante el comando del proyecto.
- Regenerar `web/src/api/schema.d.ts`.
- Modificar `web/src/api/auth.ts`.
- Crear o modificar `web/src/api/auth.test.ts` si el patrón actual lo admite; en caso contrario cubrir llamadas desde las páginas.

**Interfaces producidas:**

```typescript
export function solicitarRestablecimientoPassword(email: string): Promise<void>;
export function restablecerPassword(
  usuarioId: string,
  token: string,
  password: string,
): Promise<void>;
```

**TDD/verificación:**

1. Añadir una prueba roja que espere las rutas y payloads exactos.
2. Regenerar OpenAPI con el mecanismo configurado por el repositorio; no editar a mano el schema.
3. Desde `web/`, ejecutar `npm.cmd run generate:api` y añadir las funciones tipadas sin casts que
   oculten el contrato.
4. Ejecutar prueba dirigida y `npm.cmd run typecheck`; esperado: verde.

**Commit previsto:** `feat(api): publica contrato de recuperación de contraseña`

---

## Tarea 7: pantalla para solicitar recuperación

**Archivos:**

- Modificar `web/src/routes/LoginPage.tsx`.
- Modificar `web/src/routes/LoginPage.test.tsx`.
- Crear `web/src/routes/OlvidePasswordPage.tsx`.
- Crear `web/src/routes/OlvidePasswordPage.test.tsx`.
- Modificar `web/src/app/router.tsx`.

**Comportamiento:**

- El login muestra «¿Olvidaste tu contraseña?» hacia `/olvide-password`.
- La nueva pantalla valida el formato del correo.
- Tras `204` muestra siempre el texto anti-enumeración aprobado.
- `429` muestra un mensaje de espera; otros errores usan uno genérico y no reflejan el correo.

**TDD:**

1. Probar enlace desde login, validación, payload, éxito genérico y `429`.
2. Ejecutar `npm.cmd run test -- --run src/routes/LoginPage.test.tsx src/routes/OlvidePasswordPage.test.tsx`; observar rojo.
3. Implementar ruta y pantalla MUI mínima.
4. Repetir tests y ejecutar typecheck + lint; esperado: verde.

**Commit previsto:** `feat(web): agrega solicitud de recuperación de contraseña`

---

## Tarea 8: pantalla para establecer contraseña nueva

**Archivos:**

- Crear `web/src/routes/RestablecerPasswordPage.tsx`.
- Crear `web/src/routes/RestablecerPasswordPage.test.tsx`.
- Modificar `web/src/app/router.tsx`.

**Comportamiento:**

- Lee `usuarioId` y `token` desde el fragmento una vez y llama a
  `history.replaceState` antes de cualquier petición.
- Mantiene ambos valores solo en estado de memoria.
- Campos «Nueva contraseña» y «Confirmar contraseña», mínimo 8, coincidencia y ojos
  independientes con `aria-label`.
- El payload excluye la confirmación.
- En éxito presenta acceso al login; token ausente/`400` muestra el mismo enlace inválido o
  caducado; `429` muestra espera.

**TDD:**

1. Probar lectura y limpieza del hash sin exponer el token en el DOM.
2. Probar hash incompleto, mínimo, coincidencia, ojos independientes y botón bloqueado.
3. Probar payload exacto, éxito, `400` y `429`.
4. Ejecutar la prueba dirigida y observar rojo.
5. Implementar con React Hook Form, Zod y MUI; repetir hasta verde.
6. Ejecutar typecheck y lint; esperado: verde.

**Commit previsto:** `feat(web): permite establecer una contraseña nueva`

---

## Tarea 9: integración, revisión y cierre

**Artefactos:** solución completa, spec, plan y diff de la rama.

1. Regenerar cualquier artefacto OpenAPI pendiente y confirmar que no hay cambios manuales.
2. Desde `CaseritoApp/` ejecutar:
   - `dotnet build CaseritoApp.sln`
   - `dotnet test CaseritoApp.sln` (Docker disponible para integración)
   - `dotnet format CaseritoApp.sln --verify-no-changes`
3. Desde `web/` ejecutar:
   - `npm.cmd run typecheck`
   - `npm.cmd run lint`
   - `npm.cmd run test -- --run`
   - `npm.cmd run build`
4. Ejecutar `git diff --check` y revisar el diff completo contra los 9 criterios de aceptación.
5. Confirmar con búsquedas puntuales que no existen logs con correo, token, contraseña, URL o
   cuerpos de mensaje introducidos por esta feature.
6. No crear migración: el modelo persistente no cambia.
7. Si todo queda cerrado, no conservar un `docs/ai/HANDOFF.md` obsoleto.

**Resultado esperado:** todas las suites y comprobaciones verdes, worktree limpio tras los commits
lógicos y rama lista para revisión, sin push ni merge.
