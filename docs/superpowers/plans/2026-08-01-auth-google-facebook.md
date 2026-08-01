# Plan: autenticación con Google y Facebook

**Fecha:** 2026-08-01
**Spec:** `docs/superpowers/specs/2026-08-01-auth-google-facebook-design.md`
**Objetivo:** permitir registro, login y vinculación mediante Google y Facebook en la PWA, manteniendo la sesión JWT/refresh y todas las reglas de Identity de Caserito.

## Reglas de ejecución

- Ejecutar por bloques y en sesiones nuevas. Leer primero `docs/ai/HANDOFF.md`, este plan y solo la sección necesaria del spec.
- TDD estricto: test rojo, implementación mínima, test verde, revisión y commit.
- No usar credenciales reales en tests ni versionar secretos.
- No llamar a Google o Meta desde la suite: sustituir/autenticar el esquema externo con identidades controladas.
- Textos y comentarios en español UTF-8; logs genéricos sin email, claims, identificadores externos, códigos ni tokens.
- Si el SDK real no expone un claim con la forma supuesta, adaptar solo el mapeo del proveedor, no relajar la política de seguridad.

## Bloque 1 — Infraestructura común backend

### Tarea 1. Extraer la emisión común de sesión Caserito

**Entradas:** lógica duplicada de login/refresh en `AuthEndpoints.cs`.
**Salida:** servicio reutilizable para contraseña, refresh y login externo.

Archivos:

- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/EmisorSesion.cs`.
- Modificar `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`.
- Modificar `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/EmisorSesionTests.cs` o cubrir mediante integración si `UserManager` impide una unidad simple.

Interfaz concreta:

```csharp
public interface IEmisorSesion
{
    Task<SesionEmitida> EmitirAsync(ApplicationUser usuario, CancellationToken ct);
}

public sealed record SesionEmitida(string AccessToken, string RefreshToken);
```

`EmisorSesion` obtiene roles, deriva permisos, consulta KYC, calcula `identidadHabilitada`, genera el JWT y emite el refresh. La escritura/borrado de cookie HTTP permanece en Host. Reemplazar la duplicación de `LoginAsync` y `RefreshAsync` sin cambiar sus contratos.

Prueba roja: login y refresh siguen emitiendo claims/refresh correctos mediante el nuevo servicio.
Verificación:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~AuthFlowTests|FullyQualifiedName~RefreshTokensTests"
```

Esperado: verde y sin cambio observable en auth tradicional.
Commit: `refactor(auth): centraliza emisión de sesión`

### Tarea 2. Registrar esquemas y configuración externa

**Entradas:** JWT es hoy el único esquema; versiones centralizadas en `Directory.Packages.props`.
**Salida:** Google/Facebook configurables, cookie externa temporal y capacidades seguras.

Archivos:

- Modificar `CaseritoApp/Directory.Packages.props` con versiones `10.0.0` de `Microsoft.AspNetCore.Authentication.Google` y `.Facebook`.
- Modificar `CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj` con ambos `PackageReference` sin versión.
- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/OpcionesAutenticacionExterna.cs`.
- Modificar `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`.
- Añadir secciones vacías, nunca secretos, a los `appsettings` pertinentes.
- Modificar `CaseritoApp/tests/CaseritoApp.IntegrationTests/Infrastructure/CaseritoApiFactory.cs` para configuración ficticia y handlers controlados.

Configuración:

```text
Authentication:Google:ClientId / ClientSecret
Authentication:Facebook:AppId / AppSecret
```

Mantener `DefaultAuthenticateScheme` y `DefaultChallengeScheme` de API en Bearer. Añadir `IdentityConstants.ExternalScheme` con cookie de 10 minutos, `HttpOnly`, `SameSite=Lax`, `Secure` fuera de Development/Testing y `Path=/api/auth/external`. Google y Facebook usan esa cookie como `SignInScheme`. Validar configuración al arrancar fuera de Development/Testing; en esos entornos permitir proveedores deshabilitados.

Crear una capacidad pública (`GET /api/auth/external/providers`) que devuelva solo proveedores realmente habilitados, para que la PWA no muestre botones inservibles.

Prueba roja: Bearer sigue protegiendo la API, la cookie externa tiene atributos esperados y la capacidad no anuncia proveedores sin configuración.
Verificación:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~AuthExternaConfiguracionTests
```

Esperado: verde sin red real.
Commit: `feat(auth): configura Google y Facebook`

### Tarea 3. Modelar y proteger el login pendiente

**Entradas:** callback externo puede requerir onboarding o vinculación.
**Salida:** ticket temporal cifrado/autenticado, corto y no legible por React.

Archivos:

- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/LoginExternoPendiente.cs`.
- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/IGestorLoginExternoPendiente.cs`.
- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/GestorLoginExternoPendienteDataProtector.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/GestorLoginExternoPendienteTests.cs`.

Contrato mínimo:

```csharp
public sealed record LoginExternoPendiente(
    string Proveedor,
    string ClaveProveedor,
    string? Email,
    bool EmailConfiable,
    string? Nombre,
    string Retorno,
    DateTimeOffset ExpiraEn);
```

El gestor usa Data Protection con propósito versionado, vigencia máxima de 10 minutos y TimeProvider. El valor se guarda en cookie `httpOnly`; `GET pending` solo proyecta `requiereEmail`, `requiereNombre`, `requiereCiudad`, `requiereVinculacion` y nombre visible permitido. Nunca devuelve proveedor key o email existente.

Validar proveedores mediante lista cerrada (`google`, `facebook`) y retorno mediante `IUrlHelper.IsLocalUrl` o función equivalente probada. Google solo marca confiable el email con `email_verified=true`; Facebook siempre `false`.

Prueba roja: ticket válido round-trip; expirado/manipulado falla; proveedor/retorno inválido se rechaza; proyección no filtra PII.
Verificación:

```powershell
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --filter FullyQualifiedName~LoginExterno
```

Esperado: todos verdes.
Commit: `feat(auth): protege login externo pendiente`

## Bloque 2 — Flujos backend

### Tarea 4. Challenge, callback y login de usuario ya asociado

**Entradas:** esquemas externos y emisor común.
**Salida:** navegación proveedor → callback → sesión Caserito.

Archivos:

- Crear `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthExternaEndpoints.cs`.
- Modificar `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs` para mapearlos.
- Crear `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthExternaLoginTests.cs`.

Mapear:

- `GET /api/auth/external/providers`.
- `GET /api/auth/external/{provider}/start?returnUrl=...`.
- callback interno cuyo `AuthenticationProperties.RedirectUri` termina en Caserito.

Usar `SignInManager.ConfigureExternalAuthenticationProperties` y `GetExternalLoginInfoAsync`, o APIs equivalentes de Identity, para no reinterpretar el protocolo. Si `FindByLoginAsync` encuentra usuario: limpiar cookie externa, llamar `IEmisorSesion`, establecer cookie refresh con el helper actual y redirigir a `/auth/external/completado?returnUrl=<ruta-local>`. La query solo contiene la ruta local, nunca token/PII.

Errores/cancelación redirigen a `/login?authExterna=cancelado|no_disponible|fallo`. Registrar únicamente código genérico y proveedor normalizado.

Prueba roja: asociado recibe refresh y puede hacer `POST /refresh`; callback cancelado no crea usuario; retorno externo se reemplaza por `/perfil`; repetición no duplica.
Verificación:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~AuthExternaLoginTests
```

Esperado: verde sin red.
Commit: `feat(auth): inicia sesión con proveedor externo`

### Tarea 5. Alta externa y onboarding mínimo

**Entradas:** login pendiente sin usuario asociado.
**Salida:** nuevo `ApplicationUser` válido con rol, asociación y sesión.

Archivos:

- Crear `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ServicioRegistroExterno.cs`.
- Modificar `AuthExternaEndpoints.cs`.
- Crear `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthExternaRegistroTests.cs`.

Mapear:

- `GET /api/auth/external/pending`.
- `POST /api/auth/external/complete` con DTO de campos requeridos.

Reglas:

- Si falta email, nombre o ciudad, exigirlo con FluentValidation/validación endpoint y límites coherentes con Perfil.
- Normalizar email mediante Identity; reconsultar email y login justo antes de crear para cerrar carreras.
- Crear sin contraseña, asignar `Cliente`, añadir login y publicar `UsuarioRegistrado` tras completar persistencia.
- Ante fallo de rol/login, compensar eliminando el usuario recién creado; no dejar cuenta parcial.
- `EmailConfirmed=true` solo para Google con claim verificado. Para Facebook o email aportado, publicar el evento con email no confirmado para reutilizar el envío actual.
- Consumir y borrar el ticket al éxito; la repetición resuelve idempotentemente por login.

Prueba roja: Google verificado, Facebook con/sin email, campos inválidos, rol/evento una vez, carrera de email y compensación de fallo.
Verificación:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~AuthExternaRegistroTests
```

Esperado: verde; `AspNetUserLogins` contiene la asociación y no hay usuario parcial.
Commit: `feat(auth): registra usuarios desde Google y Facebook`

### Tarea 6. Vincular sin apropiación por coincidencia de email

**Entradas:** ticket cuyo email ya pertenece a otra cuenta.
**Salida:** vinculación solo tras sesión válida del propietario.

Archivos:

- Ampliar `AuthExternaEndpoints.cs` y el servicio de registro/resolución.
- Crear `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthExternaVinculacionTests.cs`.

Mapear `POST /api/auth/external/link` con `RequireAuthorization`. Verificar que existe ticket pendiente, obtener `sub` del JWT mediante el helper seguro existente y confirmar que el usuario autenticado es exactamente el dueño del email detectado. Añadir login de forma idempotente. Si el usuario no coincide, devolver problema genérico y conservar o invalidar el ticket según el riesgo; preferencia: invalidarlo tras intento incorrecto.

La UI podrá hacer login tradicional u otro login externo; después invoca `link`. Si el usuario olvidó contraseña, usa `/olvide-password` existente y vuelve al flujo.

Prueba roja: email coincidente no enlaza ni duplica automáticamente; dueño autenticado vincula; usuario distinto falla; segundo proveedor queda en la misma cuenta; identidad ya vinculada a otra cuenta no se mueve.
Verificación:

```powershell
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter FullyQualifiedName~AuthExternaVinculacionTests
```

Esperado: verde.
Commit: `feat(auth): vincula cuentas externas de forma segura`

## Bloque 3 — PWA y contrato

### Tarea 7. UI de login, retorno y onboarding

**Entradas:** endpoints backend operativos.
**Salida:** experiencia completa en navegador y PWA instalada.

Archivos:

- Modificar `web/src/routes/LoginPage.tsx` y `LoginPage.test.tsx`.
- Crear `web/src/routes/AuthExternaCallbackPage.tsx` y test.
- Crear `web/src/routes/CompletarRegistroExternoPage.tsx` y test.
- Modificar `web/src/app/router.tsx`.
- Modificar `web/src/api/auth.ts`.
- Modificar `web/src/auth/AuthContext.tsx` solo para exponer una operación reutilizable de restaurar sesión/perfil.

Comportamiento:

- Consultar capacidades y mostrar Facebook, Google, separador “o” y formulario existente; si la consulta falla, conservar el formulario.
- Los botones hacen navegación completa a `start`, incluyendo únicamente el `from` local validado.
- Callback llama a una operación `restaurarSesion`: `refresh`, perfil y claims; luego navega al retorno local.
- Onboarding consulta `pending` y muestra solo email/nombre/ciudad requeridos.
- Si requiere vinculación, explicar de forma genérica y ofrecer login/recuperación; tras autenticar al dueño ejecutar `link` y restaurar sesión.
- Mostrar mensajes españoles genéricos para códigos opacos; accesibilidad por nombre, foco y teclado.

Prueba roja: orden de botones, proveedor deshabilitado oculto, navegación correcta, retorno malicioso descartado, bootstrap, onboarding dinámico, error sin PII y login tradicional intacto.
Verificación:

```powershell
npm run test -- --run src/routes/LoginPage.test.tsx src/routes/AuthExternaCallbackPage.test.tsx src/routes/CompletarRegistroExternoPage.test.tsx
npm run typecheck
```

Esperado: verde.
Commit: `feat(web): añade acceso con Facebook y Google`

### Tarea 8. OpenAPI, cliente generado y configuración operativa

**Entradas:** contrato final.
**Salida:** tipos sincronizados y despliegue documentado sin secretos.

Archivos:

- Añadir `.Produces`, `.Accepts` y nombres de operación a endpoints JSON; excluir/documentar endpoints de navegación cuando proceda.
- Regenerar `CaseritoApp/src/Host/CaseritoApp.Host/openapi.json` y `web/src/api/schema.d.ts` mediante comandos existentes.
- Actualizar `.env.example`, configuración de compose/despliegue o documentación operativa exacta localizada durante la tarea, sin valores reales.
- Crear `docs/ai/AUTH_PROVEEDORES.md` solo si no existe un documento operativo apropiado; incluir callbacks de dev/prod, alta en Google/Meta, secretos, política de privacidad, eliminación de datos y prueba manual.

No modificar el contrato de login/refresh tradicional. Confirmar que GET de navegación no es tratado como llamada JSON por el cliente.

Verificación:

```powershell
npm run generate:api
npm run typecheck
git diff --check
```

Esperado: generación reproducible y ningún secreto.
Commit: `docs(auth): documenta configuración de proveedores`

## Bloque 4 — Integración y cierre

### Tarea 9. Regresión completa y revisión contra el spec

Revisar primero `git diff --stat` desde el inicio de rama y después los diffs relevantes. Comprobar explícitamente:

- proveedores y retornos en lista cerrada;
- ninguna vinculación automática por email;
- cookies y expiraciones;
- ausencia de tokens/PII en URL, logs, errores y almacenamiento web;
- rol, permisos, KYC y email confirmado en JWT;
- compensación/idempotencia;
- continuidad de correo/contraseña, refresh, logout, confirmación y recuperación;
- requisitos productivos de Google/Meta documentados.

Verificación backend desde `CaseritoApp/`:

```powershell
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes
```

Verificación frontend desde `web/`:

```powershell
npm run typecheck
npm run lint
npm run test -- --run
npm run build
```

Requiere Docker para integración. Añadir una prueba manual con aplicaciones sandbox de Google/Meta cuando el usuario proporcione credenciales y callbacks; no afirmar esa parte verde hasta ejecutarla.

Esperado: todas las suites verdes, `git diff --check` limpio y sin cambios ajenos.
Commit previsto si aparecen ajustes exclusivos de integración: `test(auth): cubre flujo externo integral`

## Reparto recomendado entre sesiones

1. **Sesión backend base:** tareas 1–3; actualizar handoff.
2. **Sesión flujos externos:** tareas 4–6; actualizar handoff.
3. **Sesión PWA/contrato:** tareas 7–8; actualizar handoff.
4. **Sesión cierre:** tarea 9, revisión final y eliminar `docs/ai/HANDOFF.md` cuando todo quede cerrado.

Cada sesión comienza verificando Git y las afirmaciones del handoff. No mergear ni hacer push sin autorización explícita.
