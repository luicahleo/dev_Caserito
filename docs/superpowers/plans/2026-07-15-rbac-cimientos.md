# Cimientos de RBAC + fix `Jwt:Key` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establecer los cimientos de RBAC (roles ≠ permisos, seeding idempotente, claims de permiso en el JWT, policies) y corregir el bug diferido de `Jwt:Key` donde firma y validación divergen.

**Architecture:** El modelo puro (roles, permisos, mapa rol→permisos) vive en `Identity.Domain` (hoy vacío). La emisión de tokens agrega los permisos del usuario como claims `perm`; la autorización usa policies `RequireClaim("perm", …)`. Una única clave de firma compartida (singleton) elimina la divergencia firma/validación. El seeder de roles corre tras migrar en Development y en Testing.

**Tech Stack:** .NET 10, ASP.NET Core Identity, JWT Bearer (HS256), EF Core + SQL Server, xUnit, Testcontainers.MsSql.

## Global Constraints

- Namespaces file-scoped; `using` fuera del namespace, System primero. `PascalCase` tipos/miembros; `_camelCase` campos privados; `I` en interfaces.
- Nullable enable, warnings-as-errors, analizadores .NET + Roslynator + Sonar. Nada compila si viola las reglas.
- Versiones de paquetes SOLO en `CaseritoApp/Directory.Packages.props` (CPM). Nunca `Version=` en un `.csproj`.
- Reglas de capa (verificadas por `CaseritoApp.ArchitectureTests`): `Domain` no depende de nada hacia afuera; `Application` solo de `Domain`; dependencias siempre hacia adentro.
- Anti-PII: jamás loguear CI, imágenes de documento/selfie, tokens de sesión ni cadenas de QR/pago. Los claims `perm` NO son PII.
- Textos de UI y comentarios en **español**.
- Comandos desde `CaseritoApp/`: build `dotnet build CaseritoApp.sln`; test `dotnet test CaseritoApp.sln`; formato CI `dotnet format CaseritoApp.sln --verify-no-changes`.
- Los tests de integración requieren Docker (Testcontainers). Entorno `Testing`, fixture `CaseritoApiFactory`.

---

### Task 1: Modelo de dominio RBAC (roles, permisos, claims, mapa)

Define el modelo puro en `Identity.Domain` y un proyecto de tests unitarios para la lógica de agregación de permisos.

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion/RolesApp.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion/Permisos.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion/ClaimsApp.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion/MapaRolesPermisos.cs`
- Create: `CaseritoApp/tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj`
- Create: `CaseritoApp/tests/CaseritoApp.UnitTests/Autorizacion/MapaRolesPermisosTests.cs`
- Modify: `CaseritoApp/CaseritoApp.sln` (vía `dotnet sln add`)

**Interfaces:**
- Produces:
  - `RolesApp` — consts `Cliente`, `Moderador`, `AdminKyc`, `AdminPlataforma`, `Soporte`, `Sistema` (todos `string`); `IReadOnlyList<string> Todos`.
  - `Permisos` — consts `PublicacionesModerar`, `ChatModerar`, `KycRevisar`, `UsuariosGestionar`, `SoporteTickets` (todos `string`); `IReadOnlyList<string> Todos`.
  - `ClaimsApp` — const `string Permiso = "perm"`.
  - `MapaRolesPermisos.PermisosDe(IEnumerable<string> roles) → IReadOnlyCollection<string>` (unión distinct; ignora roles desconocidos).

- [ ] **Step 1: Crear el `.csproj` de tests unitarios**

Create `CaseritoApp/tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
    <!-- CA1707: se permiten guiones bajos en nombres de test (convención xUnit en español). -->
    <NoWarn>$(NoWarn);CA1707</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Identity\CaseritoApp.Identity.Domain\CaseritoApp.Identity.Domain.csproj" />
    <ProjectReference Include="..\..\src\Identity\CaseritoApp.Identity.Infrastructure\CaseritoApp.Identity.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

(La referencia a Infrastructure la usa la Task 2; se agrega ya para no re-editar el csproj.)

- [ ] **Step 2: Agregar el proyecto a la solución**

Run desde `CaseritoApp/`:
```bash
dotnet sln CaseritoApp.sln add tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj --solution-folder tests
```
Expected: `Project ... added to the solution.`

- [ ] **Step 3: Escribir el test que falla (agregación de permisos)**

Create `CaseritoApp/tests/CaseritoApp.UnitTests/Autorizacion/MapaRolesPermisosTests.cs`:

```csharp
using CaseritoApp.Identity.Domain.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class MapaRolesPermisosTests
{
    [Fact]
    public void Cliente_no_tiene_permisos_elevados()
    {
        var permisos = MapaRolesPermisos.PermisosDe([RolesApp.Cliente]);

        Assert.Empty(permisos);
    }

    [Fact]
    public void AdminPlataforma_tiene_todos_los_permisos()
    {
        var permisos = MapaRolesPermisos.PermisosDe([RolesApp.AdminPlataforma]);

        Assert.Equal(Permisos.Todos.Count, permisos.Count);
        Assert.All(Permisos.Todos, p => Assert.Contains(p, permisos));
    }

    [Fact]
    public void Usuario_multi_rol_une_permisos_sin_duplicados()
    {
        var permisos = MapaRolesPermisos.PermisosDe([RolesApp.Moderador, RolesApp.AdminKyc]);

        Assert.Contains(Permisos.PublicacionesModerar, permisos);
        Assert.Contains(Permisos.ChatModerar, permisos);
        Assert.Contains(Permisos.KycRevisar, permisos);
        Assert.Equal(3, permisos.Count);
    }

    [Fact]
    public void Rol_desconocido_se_ignora()
    {
        var permisos = MapaRolesPermisos.PermisosDe(["RolInexistente"]);

        Assert.Empty(permisos);
    }
}
```

- [ ] **Step 4: Verificar que el test falla (no compila)**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj
```
Expected: FAIL de compilación — `RolesApp`, `Permisos`, `MapaRolesPermisos` no existen.

- [ ] **Step 5: Implementar `RolesApp`**

Create `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion/RolesApp.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Autorizacion;

/// <summary>Nombres canónicos de los roles del MVP (brief §8.1). Roles ≠ permisos.</summary>
public static class RolesApp
{
    /// <summary>Rol por defecto de todo usuario registrado (comprador y vendedor). Sin permisos elevados.</summary>
    public const string Cliente = "Cliente";

    /// <summary>Modera publicaciones y chat reportados (ocultar/eliminar).</summary>
    public const string Moderador = "Moderador";

    /// <summary>Aprueba/rechaza la verificación de identidad (KYC).</summary>
    public const string AdminKyc = "AdminKyc";

    /// <summary>Acceso total / gestión de la plataforma.</summary>
    public const string AdminPlataforma = "AdminPlataforma";

    /// <summary>Atención al cliente / tickets. Definido pero no operativo en v1.</summary>
    public const string Soporte = "Soporte";

    /// <summary>Actor de servicio (machine user) para automatización futura. No operativo en v1.</summary>
    public const string Sistema = "Sistema";

    /// <summary>Todos los roles definidos, en orden de seeding.</summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        Cliente, Moderador, AdminKyc, AdminPlataforma, Soporte, Sistema,
    ];
}
```

- [ ] **Step 6: Implementar `Permisos`**

Create `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion/Permisos.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Autorizacion;

/// <summary>Permisos atómicos del MVP. El código de autorización chequea permisos, no roles.</summary>
public static class Permisos
{
    /// <summary>Ocultar/eliminar publicaciones reportadas.</summary>
    public const string PublicacionesModerar = "publicaciones.moderar";

    /// <summary>Moderar conversaciones de chat reportadas.</summary>
    public const string ChatModerar = "chat.moderar";

    /// <summary>Aprobar/rechazar verificaciones de identidad.</summary>
    public const string KycRevisar = "kyc.revisar";

    /// <summary>Gestionar usuarios (roles, suspensión).</summary>
    public const string UsuariosGestionar = "usuarios.gestionar";

    /// <summary>Atender tickets de soporte (futuro Disputes).</summary>
    public const string SoporteTickets = "soporte.tickets";

    /// <summary>Todos los permisos definidos.</summary>
    public static readonly IReadOnlyList<string> Todos =
    [
        PublicacionesModerar, ChatModerar, KycRevisar, UsuariosGestionar, SoporteTickets,
    ];
}
```

- [ ] **Step 7: Implementar `ClaimsApp`**

Create `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion/ClaimsApp.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Autorizacion;

/// <summary>Tipos de claim propios de la aplicación emitidos en el JWT.</summary>
public static class ClaimsApp
{
    /// <summary>Claim de permiso concedido (uno por permiso). No es PII.</summary>
    public const string Permiso = "perm";
}
```

- [ ] **Step 8: Implementar `MapaRolesPermisos`**

Create `CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion/MapaRolesPermisos.cs`:

```csharp
namespace CaseritoApp.Identity.Domain.Autorizacion;

/// <summary>
/// Mapa autoritativo rol → permisos. Es la única fuente de verdad usada en tiempo de ejecución
/// para agregar los permisos de un usuario a partir de sus roles. Cambiar los permisos de un rol
/// solo requiere editar este mapa (no reescribir lógica de autorización).
/// </summary>
public static class MapaRolesPermisos
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Mapa =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            [RolesApp.Cliente] = [],
            [RolesApp.Moderador] = [Permisos.PublicacionesModerar, Permisos.ChatModerar],
            [RolesApp.AdminKyc] = [Permisos.KycRevisar],
            [RolesApp.AdminPlataforma] = Permisos.Todos,
            [RolesApp.Soporte] = [Permisos.SoporteTickets],
            [RolesApp.Sistema] = [],
        };

    /// <summary>Permisos concedidos a un rol individual (vacío si el rol es desconocido).</summary>
    public static IReadOnlyList<string> PermisosDeRol(string rol) =>
        Mapa.TryGetValue(rol, out var permisos) ? permisos : [];

    /// <summary>Unión distinta de los permisos de todos los roles indicados.</summary>
    public static IReadOnlyCollection<string> PermisosDe(IEnumerable<string> roles) =>
        roles.SelectMany(PermisosDeRol).Distinct(StringComparer.Ordinal).ToArray();
}
```

- [ ] **Step 9: Verificar que los tests pasan**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj
```
Expected: PASS (4 tests).

- [ ] **Step 10: Verificar arquitectura y formato**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.ArchitectureTests/CaseritoApp.ArchitectureTests.csproj
dotnet format CaseritoApp.sln --verify-no-changes
```
Expected: PASS (Domain sin dependencias externas nuevas; formato limpio).

- [ ] **Step 11: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Domain/Autorizacion CaseritoApp/tests/CaseritoApp.UnitTests CaseritoApp/CaseritoApp.sln
git commit -m "feat(identity): modelo de dominio RBAC (roles, permisos, mapa)"
```

---

### Task 2: Fix `Jwt:Key` — clave de firma compartida

Elimina la divergencia firma/validación introduciendo una única clave de firma (singleton) usada por el generador y por la validación Bearer, y hace que la validación consuma `IOptions<OpcionesJwt>` en lugar de releer la config.

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ProveedorClaveFirma.cs`
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ConfigurarJwtBearer.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/GeneradorTokensAcceso.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs:63-129`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/ProveedorClaveFirmaTests.cs`

**Interfaces:**
- Consumes: `OpcionesJwt` (existente), `TimeProvider`.
- Produces:
  - `ProveedorClaveFirma` — ctor `(IOptions<OpcionesJwt>)`; propiedad `SymmetricSecurityKey Clave` (calculada una sola vez; efímera ≥ 32 bytes si `Key` está vacía).
  - `ConfigurarJwtBearer : IConfigureNamedOptions<JwtBearerOptions>` — ctor `(IOptions<OpcionesJwt>, ProveedorClaveFirma)`.
  - `GeneradorTokensAcceso` — ctor pasa a `(IOptions<OpcionesJwt>, ProveedorClaveFirma, TimeProvider)`; `Generar(ApplicationUser)` firma con `ProveedorClaveFirma.Clave` (la firma pública del método NO cambia en esta task).

- [ ] **Step 1: Escribir el test que falla (firma = validación con clave efímera)**

Create `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/ProveedorClaveFirmaTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class ProveedorClaveFirmaTests
{
    [Fact]
    public void Clave_efimera_tiene_al_menos_256_bits_cuando_no_hay_key()
    {
        var proveedor = new ProveedorClaveFirma(Options.Create(new OpcionesJwt { Key = string.Empty }));

        Assert.True(proveedor.Clave.KeySize >= 256);
    }

    [Fact]
    public void Clave_es_la_misma_instancia_en_lecturas_sucesivas()
    {
        var proveedor = new ProveedorClaveFirma(Options.Create(new OpcionesJwt { Key = string.Empty }));

        Assert.Same(proveedor.Clave, proveedor.Clave);
    }

    [Fact]
    public void Usa_la_key_configurada_cuando_esta_presente()
    {
        const string key = "clave-fija-para-tests-unitarios-de-32b+";
        var proveedor = new ProveedorClaveFirma(Options.Create(new OpcionesJwt { Key = key }));

        Assert.Equal(Encoding.UTF8.GetBytes(key), proveedor.Clave.Key);
    }

    [Fact]
    public void Token_firmado_con_clave_efimera_valida_con_la_misma_clave()
    {
        // Regresión del bug Jwt:Key: sin Key configurada, firma y validación deben usar la MISMA clave.
        var opciones = Options.Create(new OpcionesJwt { Key = string.Empty });
        var proveedor = new ProveedorClaveFirma(opciones);
        var generador = new GeneradorTokensAcceso(opciones, proveedor, TimeProvider.System);

        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.test", Nombre = "N" };
        var jwt = generador.Generar(usuario);

        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "CaseritoApp",
            ValidateAudience = true,
            ValidAudience = "CaseritoApp",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = proveedor.Clave,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(jwt, parametros, out _);

        Assert.NotNull(principal);
    }
}
```

- [ ] **Step 2: Verificar que falla (no compila)**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj
```
Expected: FAIL — `ProveedorClaveFirma` no existe y `GeneradorTokensAcceso` no acepta el nuevo parámetro.

- [ ] **Step 3: Implementar `ProveedorClaveFirma`**

Create `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ProveedorClaveFirma.cs`:

```csharp
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>
/// Fuente única de la clave de firma HS256. Registrado como singleton para que el generador de
/// tokens y la validación Bearer usen exactamente la misma clave. Si <c>Jwt:Key</c> está vacía
/// (solo permitido en Development/Testing, ver <see cref="DependencyInjection"/>), genera una clave
/// efímera de ≥ 256 bits una sola vez, evitando que firma y validación diverjan.
/// </summary>
public sealed class ProveedorClaveFirma
{
    /// <summary>Clave simétrica compartida para firmar y validar el access token.</summary>
    public SymmetricSecurityKey Clave { get; }

    public ProveedorClaveFirma(IOptions<OpcionesJwt> opciones)
    {
        var key = opciones.Value.Key;
        var texto = string.IsNullOrWhiteSpace(key)
            ? Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")
            : key;

        Clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(texto));
    }
}
```

- [ ] **Step 4: Actualizar `GeneradorTokensAcceso` para firmar con el proveedor**

Modify `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/GeneradorTokensAcceso.cs`. Reemplaza la clase de implementación (líneas 16-46) por:

```csharp
/// <inheritdoc cref="IGeneradorTokensAcceso"/>
public sealed class GeneradorTokensAcceso(
    IOptions<OpcionesJwt> opciones, ProveedorClaveFirma proveedorClave, TimeProvider tiempo)
    : IGeneradorTokensAcceso
{
    private readonly OpcionesJwt _o = opciones.Value;

    /// <inheritdoc/>
    public string Generar(ApplicationUser usuario)
    {
        var credenciales = new SigningCredentials(proveedorClave.Clave, SecurityAlgorithms.HmacSha256);
        var ahora = tiempo.GetUtcNow();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, usuario.Nombre),
        };

        var token = new JwtSecurityToken(
            issuer: _o.Issuer,
            audience: _o.Audience,
            claims: claims,
            notBefore: ahora.UtcDateTime,
            expires: ahora.AddMinutes(_o.MinutosAcceso).UtcDateTime,
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

(El `using System.Text;` de la línea 3 queda sin uso tras quitar `Encoding.UTF8.GetBytes(_o.Key)`; elimínalo para no violar el analizador de usings innecesarios.)

- [ ] **Step 5: Implementar `ConfigurarJwtBearer`**

Create `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/ConfigurarJwtBearer.cs`:

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>
/// Configura los <see cref="JwtBearerOptions"/> del esquema Bearer usando el <see cref="OpcionesJwt"/>
/// ya bindeado/validado y la clave compartida de <see cref="ProveedorClaveFirma"/> (misma clave que
/// usa el generador de tokens, de modo que firma y validación nunca divergen).
/// </summary>
public sealed class ConfigurarJwtBearer(IOptions<OpcionesJwt> opciones, ProveedorClaveFirma proveedorClave)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly OpcionesJwt _o = opciones.Value;

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
        {
            return;
        }

        // Ver nota en DependencyInjection: se desactiva el remapeo de "sub" para leer el userId
        // directamente en los endpoints.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _o.Issuer,
            ValidateAudience = true,
            ValidAudience = _o.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = proveedorClave.Clave,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}
```

- [ ] **Step 6: Reconfigurar el registro en `DependencyInjection`**

Modify `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`. Reemplaza el cuerpo del método `AgregarAutenticacionJwt` (líneas 65-128, desde `servicios.AddSingleton(TimeProvider.System);` hasta antes de `return servicios;`) por:

```csharp
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddSingleton<ProveedorClaveFirma>();
        servicios.AddSingleton<IGeneradorTokensAcceso, GeneradorTokensAcceso>();
        servicios.AddScoped<IServicioRefreshTokens, ServicioRefreshTokens>();

        // Solo en Development/Testing se admite una clave efímera de repuesto; fuera de esos
        // entornos la ausencia de "Jwt:Key" es un error de configuración crítico y debe fallar
        // rápido (fail-fast). ValidateOnStart corre al construir el host, así que un despliegue mal
        // configurado ni siquiera arranca.
        var permiteClaveEfimera = entorno.IsDevelopment() || entorno.IsEnvironment("Testing");

        servicios.AddOptions<OpcionesJwt>()
            .Bind(config.GetSection(OpcionesJwt.Seccion))
            .Validate(
                o => permiteClaveEfimera
                    || (!string.IsNullOrWhiteSpace(o.Key) && Encoding.UTF8.GetByteCount(o.Key) >= 32),
                "Jwt:Key es obligatorio y debe tener al menos 32 bytes fuera de Development/Testing")
            .ValidateOnStart();

        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // La configuración de JwtBearerOptions se hace vía IConfigureNamedOptions para inyectar el
        // OpcionesJwt bindeado y la clave compartida (ProveedorClaveFirma), en vez de releer la
        // config a mano. Así firma (GeneradorTokensAcceso) y validación usan la misma clave.
        servicios.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigurarJwtBearer>();

        servicios.AddAuthorization();
```

Elimina los `using` que queden sin uso tras el cambio (`Microsoft.IdentityModel.Tokens` ya no se usa aquí; `Microsoft.Extensions.Options` sí, por `IConfigureOptions`). Verifica con `dotnet format`.

- [ ] **Step 7: Verificar que los tests unitarios pasan**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj
```
Expected: PASS (incluye `Token_firmado_con_clave_efimera_valida_con_la_misma_clave`).

- [ ] **Step 8: Verificar regresión de integración (auth sigue funcionando en Testing)**

Run desde `CaseritoApp/` (requiere Docker):
```bash
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~AuthFlowTests|FullyQualifiedName~PerfilTests"
```
Expected: PASS — login/refresh/perfil siguen funcionando con la clave de `CaseritoApiFactory`.

- [ ] **Step 9: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure CaseritoApp/tests/CaseritoApp.UnitTests
git commit -m "fix(identity): clave de firma JWT compartida (firma=validacion), evita fallo diferido en dev"
```

---

### Task 3: Emitir claims de permiso en el JWT

Cambia el generador para incluir un claim `perm` por permiso, y hace que login/refresh resuelvan los roles del usuario y los mapeen a permisos.

**Files:**
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/GeneradorTokensAcceso.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs:60-124`
- Test: `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/GeneradorTokensAccesoTests.cs`

**Interfaces:**
- Consumes: `MapaRolesPermisos.PermisosDe` (Task 1), `ClaimsApp.Permiso` (Task 1), `ProveedorClaveFirma` (Task 2).
- Produces:
  - `IGeneradorTokensAcceso.Generar(ApplicationUser usuario, IReadOnlyCollection<string> permisos) → string`.
  - Login/refresh resuelven `UserManager.GetRolesAsync(usuario)` → `MapaRolesPermisos.PermisosDe(roles)` → pasan a `Generar`.

- [ ] **Step 1: Escribir el test que falla (el token incluye claims perm)**

Create `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/GeneradorTokensAccesoTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace CaseritoApp.UnitTests.Auth;

public sealed class GeneradorTokensAccesoTests
{
    private static GeneradorTokensAcceso CrearGenerador()
    {
        var opciones = Options.Create(new OpcionesJwt { Key = "clave-fija-para-tests-unitarios-de-32b+" });
        return new GeneradorTokensAcceso(opciones, new ProveedorClaveFirma(opciones), TimeProvider.System);
    }

    private static JwtSecurityToken Leer(string jwt) => new JwtSecurityTokenHandler().ReadJwtToken(jwt);

    [Fact]
    public void Emite_un_claim_perm_por_permiso()
    {
        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.test", Nombre = "N" };

        var jwt = CrearGenerador().Generar(usuario, [Permisos.KycRevisar, Permisos.UsuariosGestionar]);

        var permisos = Leer(jwt).Claims.Where(c => c.Type == ClaimsApp.Permiso).Select(c => c.Value).ToArray();
        Assert.Contains(Permisos.KycRevisar, permisos);
        Assert.Contains(Permisos.UsuariosGestionar, permisos);
        Assert.Equal(2, permisos.Length);
    }

    [Fact]
    public void Sin_permisos_no_emite_claims_perm()
    {
        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.test", Nombre = "N" };

        var jwt = CrearGenerador().Generar(usuario, []);

        Assert.DoesNotContain(Leer(jwt).Claims, c => c.Type == ClaimsApp.Permiso);
    }
}
```

- [ ] **Step 2: Actualizar el test de Task 2 a la nueva firma**

Modify `CaseritoApp/tests/CaseritoApp.UnitTests/Auth/ProveedorClaveFirmaTests.cs`: en `Token_firmado_con_clave_efimera_valida_con_la_misma_clave`, cambia la línea de generación a pasar una lista de permisos vacía:

```csharp
        var jwt = generador.Generar(usuario, []);
```

- [ ] **Step 3: Verificar que falla (no compila)**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj
```
Expected: FAIL — `Generar` no acepta el segundo parámetro.

- [ ] **Step 4: Actualizar la interfaz y la implementación del generador**

Modify `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/GeneradorTokensAcceso.cs`.

Cambia la firma de la interfaz (líneas 10-14):

```csharp
/// <summary>Genera JWT de acceso para un <see cref="ApplicationUser"/> autenticado.</summary>
public interface IGeneradorTokensAcceso
{
    /// <summary>
    /// Genera un JWT firmado (HS256) con los claims del usuario, sus permisos (claims <c>perm</c>)
    /// y expiración configurable.
    /// </summary>
    public string Generar(ApplicationUser usuario, IReadOnlyCollection<string> permisos);
}
```

Y en la implementación, cambia la firma del método y agrega los claims `perm` tras los tres claims base:

```csharp
    /// <inheritdoc/>
    public string Generar(ApplicationUser usuario, IReadOnlyCollection<string> permisos)
    {
        var credenciales = new SigningCredentials(proveedorClave.Clave, SecurityAlgorithms.HmacSha256);
        var ahora = tiempo.GetUtcNow();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, usuario.Nombre),
        };

        claims.AddRange(permisos.Select(p => new Claim(ClaimsApp.Permiso, p)));

        var token = new JwtSecurityToken(
            issuer: _o.Issuer,
            audience: _o.Audience,
            claims: claims,
            notBefore: ahora.UtcDateTime,
            expires: ahora.AddMinutes(_o.MinutosAcceso).UtcDateTime,
            signingCredentials: credenciales);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
```

Agrega el `using CaseritoApp.Identity.Domain.Autorizacion;` (System primero, luego el resto ordenado).

- [ ] **Step 5: Resolver permisos en login y refresh**

Modify `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`.

Agrega el using (tras los existentes, orden alfabético dentro del bloque no-System):
```csharp
using CaseritoApp.Identity.Domain.Autorizacion;
```

En `LoginAsync`, reemplaza la línea 82 (`var accessToken = generadorTokens.Generar(usuario);`) por:
```csharp
        var roles = await userManager.GetRolesAsync(usuario);
        var permisos = MapaRolesPermisos.PermisosDe(roles);
        var accessToken = generadorTokens.Generar(usuario, permisos);
```

En `RefreshAsync`, reemplaza la línea 120 (`var accessToken = generadorTokens.Generar(usuario);`) por:
```csharp
        var roles = await userManager.GetRolesAsync(usuario);
        var permisos = MapaRolesPermisos.PermisosDe(roles);
        var accessToken = generadorTokens.Generar(usuario, permisos);
```

- [ ] **Step 6: Verificar unitarios + build de la solución**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.UnitTests/CaseritoApp.UnitTests.csproj
dotnet build CaseritoApp.sln
```
Expected: PASS (unitarios) y build sin errores (login/refresh compilan con la nueva firma).

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure CaseritoApp/src/Host/CaseritoApp.Host CaseritoApp/tests/CaseritoApp.UnitTests
git commit -m "feat(identity): emitir claims 'perm' agregados de los roles del usuario en el JWT"
```

---

### Task 4: Seeder idempotente de roles + wiring

Crea el seeder que asegura los 6 roles y sincroniza sus RoleClaims `perm`, y lo engancha tras migrar en Development y en Testing. Verifica idempotencia end-to-end.

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/SeedRolesExtensions.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs:34-40`
- Modify: `CaseritoApp/tests/CaseritoApp.IntegrationTests/Infrastructure/CaseritoApiFactory.cs:40-46`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/SeedRolesTests.cs`

**Interfaces:**
- Consumes: `RolesApp.Todos`, `MapaRolesPermisos.PermisosDeRol`, `ClaimsApp.Permiso` (Task 1); `RoleManager<IdentityRole<Guid>>` (ya registrado por `AddRoles`).
- Produces: `IServiceProvider.SembrarRolesAsync(CancellationToken) → Task` (extension). Crea roles faltantes y agrega RoleClaims `perm` faltantes; idempotente.

- [ ] **Step 1: Escribir el test de integración que falla (idempotencia)**

Create `CaseritoApp/tests/CaseritoApp.IntegrationTests/SeedRolesTests.cs`:

```csharp
using System.Security.Claims;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Verifica que el seeder de roles deja los 6 roles del MVP con sus RoleClaims de permiso, y que
/// re-ejecutarlo es idempotente (no duplica roles ni claims).
/// </summary>
public sealed class SeedRolesTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Seeder_crea_los_roles_del_mvp_y_es_idempotente()
    {
        // La factory ya sembró una vez en InitializeAsync; ejecutar de nuevo no debe duplicar.
        await factory.Services.SembrarRolesAsync();

        using var scope = factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Assert.Equal(RolesApp.Todos.Count, roleManager.Roles.Count());

        var adminKyc = await roleManager.FindByNameAsync(RolesApp.AdminKyc);
        Assert.NotNull(adminKyc);

        var claims = await roleManager.GetClaimsAsync(adminKyc!);
        var permisos = claims.Where(c => c.Type == ClaimsApp.Permiso).Select(c => c.Value).ToArray();

        Assert.Equal([Permisos.KycRevisar], permisos);
    }
}
```

- [ ] **Step 2: Verificar que falla (no compila)**

Run desde `CaseritoApp/`:
```bash
dotnet build tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj
```
Expected: FAIL — `SembrarRolesAsync` no existe.

- [ ] **Step 3: Implementar el seeder**

Create `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/SeedRolesExtensions.cs`:

```csharp
using System.Security.Claims;
using CaseritoApp.Identity.Domain.Autorizacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Seeding idempotente de los roles del MVP y sus RoleClaims de permiso.</summary>
public static class SeedRolesExtensions
{
    /// <summary>
    /// Asegura que existan los 6 roles de <see cref="RolesApp.Todos"/> y que cada uno tenga los
    /// RoleClaims <c>perm</c> del mapa <see cref="MapaRolesPermisos"/>. Idempotente: re-ejecutar no
    /// crea duplicados. Debe llamarse DESPUÉS de aplicar las migraciones.
    /// </summary>
    public static async Task SembrarRolesAsync(this IServiceProvider proveedor, CancellationToken ct = default)
    {
        using var scope = proveedor.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var nombreRol in RolesApp.Todos)
        {
            var rol = await roleManager.FindByNameAsync(nombreRol);
            if (rol is null)
            {
                rol = new IdentityRole<Guid>(nombreRol);
                await roleManager.CreateAsync(rol);
            }

            var permisosActuales = (await roleManager.GetClaimsAsync(rol))
                .Where(c => c.Type == ClaimsApp.Permiso)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var permiso in MapaRolesPermisos.PermisosDeRol(nombreRol))
            {
                if (permisosActuales.Add(permiso))
                {
                    await roleManager.AddClaimAsync(rol, new Claim(ClaimsApp.Permiso, permiso));
                }
            }
        }
    }
}
```

- [ ] **Step 4: Enganchar el seeder en `Program.cs`**

Modify `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`. Reemplaza el bloque de migración (líneas 34-40) por:

```csharp
// Migración + seeding de roles automáticos solo en Development y solo si hay cadena de conexión.
if (app.Environment.IsDevelopment() && !string.IsNullOrWhiteSpace(cadenaConexion))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.MigrateAsync();
    }

    await app.Services.SembrarRolesAsync();
}
```

- [ ] **Step 5: Enganchar el seeder en `CaseritoApiFactory`**

Modify `CaseritoApp/tests/CaseritoApp.IntegrationTests/Infrastructure/CaseritoApiFactory.cs`. Reemplaza `InitializeAsync` (líneas 40-46) por:

```csharp
    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.MigrateAsync();
        }

        await Services.SembrarRolesAsync();
    }
```

- [ ] **Step 6: Verificar el test de integración (requiere Docker)**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~SeedRolesTests"
```
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure CaseritoApp/src/Host/CaseritoApp.Host/Program.cs CaseritoApp/tests/CaseritoApp.IntegrationTests
git commit -m "feat(identity): seeder idempotente de roles + RoleClaims de permiso"
```

---

### Task 5: Asignar rol `Cliente` en el registro

Todo usuario nuevo recibe el rol por defecto `Cliente` tras registrarse.

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs:35-58`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/RegistroAsignaRolTests.cs`

**Interfaces:**
- Consumes: `RolesApp.Cliente` (Task 1); `UserManager.AddToRoleAsync`. Roles ya sembrados (Task 4).
- Produces: tras `POST /api/auth/register` exitoso, el usuario tiene el rol `Cliente`.

- [ ] **Step 1: Escribir el test de integración que falla**

Create `CaseritoApp/tests/CaseritoApp.IntegrationTests/RegistroAsignaRolTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>Verifica que al registrarse, el usuario recibe el rol por defecto <c>Cliente</c>.</summary>
public sealed class RegistroAsignaRolTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    [Fact]
    public async Task Registro_asigna_rol_Cliente()
    {
        using var cliente = factory.CreateClient();
        var email = $"registro-rol-{Guid.NewGuid():N}@caserito.test";

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "Lima"));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        Assert.NotNull(usuario);

        var roles = await userManager.GetRolesAsync(usuario!);
        Assert.Contains(RolesApp.Cliente, roles);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~RegistroAsignaRolTests"
```
Expected: FAIL — el usuario no tiene rol `Cliente` asignado.

- [ ] **Step 3: Asignar el rol en `RegistrarAsync`**

Modify `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`. Reemplaza el cuerpo de `RegistrarAsync` desde la validación del resultado (líneas 49-57) por:

```csharp
        if (!resultado.Succeeded)
        {
            return Results.ValidationProblem(
                resultado.Errors.ToDictionary(
                    e => e.Code,
                    e => new[] { e.Description }));
        }

        // Todo usuario nuevo recibe el rol por defecto Cliente (sembrado en el arranque).
        await userManager.AddToRoleAsync(usuario, RolesApp.Cliente);

        return Results.Ok();
```

(El `using CaseritoApp.Identity.Domain.Autorizacion;` ya fue agregado en Task 3, Step 5.)

- [ ] **Step 4: Verificar que pasa**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~RegistroAsignaRolTests"
```
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs CaseritoApp/tests/CaseritoApp.IntegrationTests/RegistroAsignaRolTests.cs
git commit -m "feat(identity): asignar rol por defecto Cliente al registrar usuario"
```

---

### Task 6: Policies de permiso + endpoint de ejemplo protegido

Registra una policy por permiso y expone `GET /api/admin/ping` protegido por `usuarios.gestionar`, demostrando el pipeline RBAC end-to-end (200 con permiso, 403 sin él).

**Files:**
- Create: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/PoliticasAutorizacion.cs`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` (registro de `AddAuthorization`)
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs` (mapear el grupo admin)
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/AdminEndpointTests.cs`

**Interfaces:**
- Consumes: `Permisos.Todos`, `Permisos.UsuariosGestionar`, `ClaimsApp.Permiso` (Task 1); claims `perm` en el JWT (Task 3); rol `AdminPlataforma` (Task 1).
- Produces:
  - `PoliticasAutorizacion.Permiso(string) → string` (nombre de policy, `"perm:{permiso}"`).
  - `IEndpointRouteBuilder.MapAdminEndpoints()` — grupo `/api/admin` con `GET /ping` protegido por la policy `usuarios.gestionar`.

- [ ] **Step 1: Escribir el test de integración que falla (200/403)**

Create `CaseritoApp/tests/CaseritoApp.IntegrationTests/AdminEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Verifica el pipeline RBAC end-to-end sobre el endpoint de ejemplo <c>/api/admin/ping</c>:
/// un Cliente sin el permiso recibe 403; un usuario con rol AdminPlataforma recibe 200.
/// </summary>
public sealed class AdminEndpointTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
{
    private async Task<string> RegistrarYLoguearAsync(HttpClient cliente, string email, string? rolExtra)
    {
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        if (rolExtra is not null)
        {
            using var scope = factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var usuario = await userManager.FindByEmailAsync(email);
            await userManager.AddToRoleAsync(usuario!, rolExtra);
        }

        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Password123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<TokenAccesoResponse>();
        return body!.AccessToken;
    }

    [Fact]
    public async Task Cliente_sin_permiso_recibe_403()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, $"admin-403-{Guid.NewGuid():N}@caserito.test", rolExtra: null);

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/admin/ping");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task AdminPlataforma_con_permiso_recibe_200()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(
            cliente, $"admin-200-{Guid.NewGuid():N}@caserito.test", RolesApp.AdminPlataforma);

        using var solicitud = new HttpRequestMessage(HttpMethod.Get, "/api/admin/ping");
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_token_recibe_401()
    {
        using var cliente = factory.CreateClient();

        var respuesta = await cliente.GetAsync("/api/admin/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
```

- [ ] **Step 2: Verificar que falla**

Run desde `CaseritoApp/`:
```bash
dotnet build tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj
```
Expected: FAIL — no existe el endpoint `/api/admin/ping` ni `PoliticasAutorizacion`.

- [ ] **Step 3: Implementar `PoliticasAutorizacion`**

Create `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Auth/PoliticasAutorizacion.cs`:

```csharp
namespace CaseritoApp.Identity.Infrastructure.Auth;

/// <summary>Nombres de policies de autorización basadas en permiso.</summary>
public static class PoliticasAutorizacion
{
    /// <summary>Nombre de la policy que exige el claim <c>perm</c> con el permiso indicado.</summary>
    public static string Permiso(string permiso) => $"perm:{permiso}";
}
```

- [ ] **Step 4: Registrar una policy por permiso en `DependencyInjection`**

Modify `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`. Reemplaza la línea `servicios.AddAuthorization();` (dentro de `AgregarAutenticacionJwt`) por:

```csharp
        servicios.AddAuthorization(opciones =>
        {
            // Una policy por permiso: exige el claim "perm" con ese valor. La autorización chequea
            // permisos, no roles (los roles solo agregan permisos al emitir el token).
            foreach (var permiso in Permisos.Todos)
            {
                opciones.AddPolicy(
                    PoliticasAutorizacion.Permiso(permiso),
                    p => p.RequireClaim(ClaimsApp.Permiso, permiso));
            }
        });
```

Agrega el `using CaseritoApp.Identity.Domain.Autorizacion;` al archivo (orden: System primero, luego el resto ordenado alfabéticamente).

- [ ] **Step 5: Implementar `AdminEndpoints`**

Create `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs`:

```csharp
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;

namespace CaseritoApp.Host.Endpoints;

/// <summary>
/// Grupo minimal API <c>/api/admin</c>. Por ahora solo expone <c>GET /ping</c> como andamiaje
/// demostrativo del pipeline RBAC (policy de permiso). Los endpoints reales de administración
/// (moderación, revisión KYC) llegan en Fase 2.
/// </summary>
public static class AdminEndpoints
{
    /// <summary>Mapea el grupo <c>/api/admin</c> protegido por la policy del permiso <c>usuarios.gestionar</c>.</summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin")
            .RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.UsuariosGestionar));

        grupo.MapGet("/ping", () => Results.Ok(new { estado = "ok" }));

        return app;
    }
}
```

- [ ] **Step 6: Mapear el grupo admin en `Program.cs`**

Modify `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`. Tras la línea `app.MapPerfilEndpoints();` (línea 46), agrega:

```csharp
app.MapAdminEndpoints();
```

- [ ] **Step 7: Verificar los tests de integración (requiere Docker)**

Run desde `CaseritoApp/`:
```bash
dotnet test tests/CaseritoApp.IntegrationTests/CaseritoApp.IntegrationTests.csproj --filter "FullyQualifiedName~AdminEndpointTests"
```
Expected: PASS (403 sin permiso, 200 con AdminPlataforma, 401 sin token).

- [ ] **Step 8: Verificación final de la solución completa**

Run desde `CaseritoApp/`:
```bash
dotnet format CaseritoApp.sln --verify-no-changes
dotnet test CaseritoApp.sln
```
Expected: formato limpio; toda la suite (unit + integración + arquitectura) en verde.

- [ ] **Step 9: Commit**

```bash
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure CaseritoApp/src/Host/CaseritoApp.Host CaseritoApp/tests/CaseritoApp.IntegrationTests
git commit -m "feat(identity): policies por permiso + endpoint /api/admin/ping de ejemplo (RBAC end-to-end)"
```

---

## Notas de cierre

- Tras completar las 6 tasks, actualizar `MEMORY.md` / `caserito-roadmap.md` marcando "Cimientos de RBAC" hecho, y considerar una migración EF NO es necesaria (las tablas de roles ya existen desde `InicialIdentity`; el seeding es de datos, no de esquema).
- Fuera de alcance (bloques posteriores): asignación/gestión de roles por admin, endpoints reales de moderación/KYC (Fase 2), atributo "Vendedor verificado", activación operativa de Soporte y Sistema.
