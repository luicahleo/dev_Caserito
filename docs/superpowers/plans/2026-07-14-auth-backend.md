# Auth backend (Fase 1 — Bloque B1) — Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Backend de identidad y sesión: ASP.NET Core Identity (`ApplicationUser`), `RefreshToken`, primera migración, access JWT + refresh en cookie httpOnly con rotación, endpoints register/login/refresh/logout, perfil vía CQRS, y tests de integración con Testcontainers.

**Architecture:** Contexto Identity del monolito modular. Identity vive en Infrastructure (acoplado a EF/Identity). Access token = JWT corto con claims; refresh = token opaco hasheado en BD, rotado, entregado en cookie httpOnly SameSite=Strict (Secure según entorno). Endpoints de auth como minimal API (fuera de MediatR); perfil vía CQRS (MediatR + Result). Migración aplicada por migrate-on-dev (Bloque A) y por la fixture de Testcontainers en tests.

**Tech Stack:** .NET 10, ASP.NET Core Identity (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`), JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`), EF Core + SQL Server, MediatR + FluentValidation, xUnit + Testcontainers.MsSql, `dotnet-ef`.

## Global Constraints

- CPM: versiones en `CaseritoApp/Directory.Packages.props`; sin `Version=` en csproj. Rigor estricto (build 0 warnings; supresiones solo locales/justificadas).
- **Sin secretos versionados**: clave JWT y afines por user-secrets/env; el refresh se guarda **hasheado** (SHA-256), nunca en claro.
- Español en nombres/comentarios (tests con `NoWarn CA1707`).
- Migración solo en Development al arrancar (Bloque A); en tests la aplica la fixture Testcontainers. Docker disponible.
- Cookie de refresh: `HttpOnly`, `SameSite=Strict`, `Path=/api/auth`, `Secure` = true solo en Production/https (false en Development/Testing sobre http).
- Access JWT ~15 min; claims `sub` (id), `email`, `name`. HS256.
- Endpoints auth bajo `/api/auth`; perfil bajo `/api/perfil` con `[Authorize]`.
- SDK .NET 10.0.301.

## Fuera de alcance (bloques posteriores)

Frontend (B2); RBAC operativo (claims de permisos/policies/seed); KYC+PII; confirmación email/2FA/reset password.

---

### Task 1: Paquetes + modelo Identity + DbContext + RefreshToken — [sin Docker]

**Files:**
- Modify: `CaseritoApp/Directory.Packages.props`
- Modify: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/CaseritoApp.Identity.Infrastructure.csproj`
- Create: `.../Identity/CaseritoApp.Identity.Infrastructure/ApplicationUser.cs`
- Create: `.../Identity/CaseritoApp.Identity.Infrastructure/RefreshToken.cs`
- Modify: `.../Identity/CaseritoApp.Identity.Infrastructure/IdentityDbContext.cs`

**Interfaces:**
- Produces: `ApplicationUser : IdentityUser<Guid>` (con `Nombre`, `Ciudad`); `RefreshToken` (Id, UserId, TokenHash, ExpiraEn, CreadoEn, RevocadoEn?, ReemplazadoPorHash?, `EsActivo`); `IdentityDbContext` hereda del de Identity de Microsoft con `DbSet<RefreshToken>` y schema `identity`.

- [ ] **Step 1: Paquetes CPM + csproj**

Añade a `Directory.Packages.props`:
```xml
    <PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.0" />
    <PackageVersion Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
```
Añade a `CaseritoApp.Identity.Infrastructure.csproj` (sin `Version=`; ya tiene EF Core + SqlServer):
```xml
    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
```

- [ ] **Step 2: `ApplicationUser`**

`ApplicationUser.cs`:
```csharp
using Microsoft.AspNetCore.Identity;

namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Usuario de la aplicación (ASP.NET Core Identity) con datos de perfil.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string Nombre { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
}
```

- [ ] **Step 3: `RefreshToken`**

`RefreshToken.cs`:
```csharp
namespace CaseritoApp.Identity.Infrastructure;

/// <summary>Refresh token persistido hasheado (nunca en claro). Se rota en cada uso.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset ExpiraEn { get; set; }
    public DateTimeOffset? RevocadoEn { get; set; }
    public string? ReemplazadoPorHash { get; set; }

    public bool EsActivo(DateTimeOffset ahora) => RevocadoEn is null && ExpiraEn > ahora;
}
```

- [ ] **Step 4: `IdentityDbContext` hereda del de Identity**

Reemplaza `IdentityDbContext.cs` (usa un alias para evitar ambigüedad con el nombre propio):
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using IdentityDbContextBase = Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<
    CaseritoApp.Identity.Infrastructure.ApplicationUser,
    Microsoft.AspNetCore.Identity.IdentityRole<System.Guid>,
    System.Guid>;

namespace CaseritoApp.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContextBase(options)
{
    public const string Schema = "identity";

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
        });
    }
}
```

- [ ] **Step 5: Verificar (sin Docker)**

Run (desde `CaseritoApp/`):
```bash
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
```
Expected: build 0/0; tests verdes. Nota: `SchemaPorContextoTests` sigue construyendo `IdentityDbContext` con `UseSqlServer` dummy y `GetDefaultSchema()` == "identity" (no cambia). `dotnet format --verify-no-changes` limpio.

- [ ] **Step 6: Commit**

```bash
cd ..
git add CaseritoApp/Directory.Packages.props CaseritoApp/src/Identity
git commit -m "feat(identity): ApplicationUser + RefreshToken + IdentityDbContext sobre ASP.NET Core Identity"
```

---

### Task 2: Primera migración de Identity + registro de Identity en el host — [Docker]

**Files:**
- Modify: `.config/dotnet-tools.json` (raíz — añadir dotnet-ef)
- Create: `.../Identity/CaseritoApp.Identity.Infrastructure/DesignTimeIdentityDbContextFactory.cs`
- Create: migración en `.../Identity/CaseritoApp.Identity.Infrastructure/Migrations/`
- Create: `.../Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`

**Interfaces:**
- Produces: `AgregarIdentity(IServiceCollection, IConfiguration)` (extensión) que registra el DbContext + Identity core + stores; migración `InicialIdentity`; factory de diseño para `dotnet ef`.

- [ ] **Step 1: Herramienta dotnet-ef + factory de diseño**

Run (desde la raíz):
```bash
dotnet tool install dotnet-ef
```
`DesignTimeIdentityDbContextFactory.cs` (permite a `dotnet ef` crear el contexto sin arrancar el host):
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CaseritoApp.Identity.Infrastructure;

public sealed class DesignTimeIdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=localhost,1433;Database=CaseritoDb;User Id=sa;Password=noop;TrustServerCertificate=True")
            .Options;
        return new IdentityDbContext(opciones);
    }
}
```

- [ ] **Step 2: Extensión de registro de Identity**

`DependencyInjection.cs` (en Identity.Infrastructure):
```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AgregarIdentity(this IServiceCollection servicios, IConfiguration config)
    {
        var cadena = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cadena))
        {
            servicios.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(cadena));
        }

        servicios
            .AddIdentityCore<ApplicationUser>(opciones =>
            {
                opciones.Password.RequiredLength = 8;
                opciones.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return servicios;
    }
}
```
En `Program.cs`, sustituye el registro directo del DbContext (Bloque A) por `builder.Services.AgregarIdentity(builder.Configuration);` (mantén el bloque de `MigrateAsync` en Development). Quita el `AddDbContext<IdentityDbContext>` suelto que introdujo el Bloque A (ahora lo hace `AgregarIdentity`).

- [ ] **Step 3: Generar la migración [Docker no requerido para generar]**

Run (desde `CaseritoApp/`):
```bash
dotnet ef migrations add InicialIdentity \
  --project src/Identity/CaseritoApp.Identity.Infrastructure \
  --startup-project src/Host/CaseritoApp.Host \
  --output-dir Migrations
```
Expected: se crean los archivos de migración (tablas de Identity + `RefreshToken`) bajo `Migrations/`.

- [ ] **Step 4: Verificar aplicando la migración [Docker]**

Run (desde `CaseritoApp/`):
```bash
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
```
Expected: build 0/0; el test de Testcontainers (`PersistenciaSmokeTests`, que migra en la fixture) ahora aplica la migración real contra SQL efímero y pasa. Verde.

- [ ] **Step 5: Commit**

```bash
cd ..
git add .config/dotnet-tools.json CaseritoApp/src/Identity CaseritoApp/src/Host/CaseritoApp.Host/Program.cs
git commit -m "feat(identity): registro de Identity + primera migración (InicialIdentity)"
```

---

### Task 3: Generador de access JWT + configuración JwtBearer — [sin Docker para el unit test]

**Files:**
- Create: `.../Identity/CaseritoApp.Identity.Infrastructure/Auth/OpcionesJwt.cs`
- Create: `.../Identity/CaseritoApp.Identity.Infrastructure/Auth/GeneradorTokensAcceso.cs`
- Modify: `.../Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` (registrar + JwtBearer)
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs` (UseAuthentication/UseAuthorization)
- Test: `CaseritoApp/tests/CaseritoApp.ArchitectureTests/Identity/GeneradorTokensAccesoTests.cs` (o un proyecto de unit tests de Identity)

**Interfaces:**
- Produces: `IGeneradorTokensAcceso.Generar(ApplicationUser) : string` (JWT con claims `sub`, `email`, `name`, expiración configurable); `OpcionesJwt { Key, Issuer, Audience, MinutosAcceso }`; JwtBearer configurado en el host.

- [ ] **Step 1: Opciones + generador**

`Auth/OpcionesJwt.cs`:
```csharp
namespace CaseritoApp.Identity.Infrastructure.Auth;

public sealed class OpcionesJwt
{
    public const string Seccion = "Jwt";
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "CaseritoApp";
    public string Audience { get; set; } = "CaseritoApp";
    public int MinutosAcceso { get; set; } = 15;
}
```

`Auth/GeneradorTokensAcceso.cs`:
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CaseritoApp.Identity.Infrastructure.Auth;

public interface IGeneradorTokensAcceso
{
    string Generar(ApplicationUser usuario);
}

public sealed class GeneradorTokensAcceso(IOptions<OpcionesJwt> opciones, TimeProvider tiempo)
    : IGeneradorTokensAcceso
{
    private readonly OpcionesJwt _o = opciones.Value;

    public string Generar(ApplicationUser usuario)
    {
        var clave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key));
        var credenciales = new SigningCredentials(clave, SecurityAlgorithms.HmacSha256);
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

- [ ] **Step 2: Registro + JwtBearer**

En `DependencyInjection.cs`, añade una extensión `AgregarAutenticacionJwt(this IServiceCollection, IConfiguration)` que: bindea `OpcionesJwt` desde la sección `Jwt`, registra `TimeProvider.System` y `IGeneradorTokensAcceso`, y configura:
```csharp
servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        var o = config.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>()!;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = o.Issuer,
            ValidateAudience = true, ValidAudience = o.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Key)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
servicios.AddAuthorization();
```
En `Program.cs`: llama `builder.Services.AgregarAutenticacionJwt(builder.Configuration);` y añade `app.UseAuthentication(); app.UseAuthorization();` antes de mapear endpoints. Añade la clave de dev por user-secrets (no versionada); documenta: `dotnet user-secrets set "Jwt:Key" "<clave-larga-aleatoria>"` en el csproj del host (inicializa user-secrets si hace falta).

- [ ] **Step 3: Unit test del generador**

Crea el test (usa un `TimeProvider` fake y `OpcionesJwt` de prueba) que verifica: el JWT generado es válido, contiene los claims `sub`/`email`/`name` correctos, y expira a los `MinutosAcceso`. Ejemplo:
```csharp
using System.IdentityModel.Tokens.Jwt;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.Identity.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CaseritoApp.ArchitectureTests.Identity;

public sealed class GeneradorTokensAccesoTests
{
    [Fact]
    public void Genera_jwt_con_claims_del_usuario()
    {
        var opciones = Options.Create(new OpcionesJwt { Key = new string('k', 40), Issuer = "CaseritoApp", Audience = "CaseritoApp", MinutosAcceso = 15 });
        var tiempo = new FakeTimeProvider();
        var gen = new GeneradorTokensAcceso(opciones, tiempo);
        var usuario = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.com", Nombre = "Ana" };

        var jwt = gen.Generar(usuario);
        var leido = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        Assert.Equal(usuario.Id.ToString(), leido.Subject);
        Assert.Contains(leido.Claims, c => c.Type == "email" && c.Value == "a@b.com");
        Assert.Contains(leido.Claims, c => c.Type == "name" && c.Value == "Ana");
    }
}
```
Añade `Microsoft.Extensions.TimeProvider.Testing` (`FakeTimeProvider`) a CPM + al csproj de tests, y referencia `Identity.Infrastructure` desde el proyecto de tests si falta.

- [ ] **Step 4: Verificar**

Run (desde `CaseritoApp/`): `dotnet build && dotnet test CaseritoApp.sln`
Expected: build 0/0; el unit test del generador pasa; el resto verde.

- [ ] **Step 5: Commit**

```bash
cd ..
git add CaseritoApp/Directory.Packages.props CaseritoApp/src/Identity CaseritoApp/src/Host/CaseritoApp.Host/Program.cs CaseritoApp/tests
git commit -m "feat(identity): generador de access JWT + JwtBearer"
```

---

### Task 4: Servicio de refresh tokens (rotación + reuso) — [Docker para el test de store]

**Files:**
- Create: `.../Identity/CaseritoApp.Identity.Infrastructure/Auth/ServicioRefreshTokens.cs`
- Modify: `.../Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/RefreshTokensTests.cs`

**Interfaces:**
- Produces: `IServicioRefreshTokens` con `Task<string> EmitirAsync(Guid userId, CancellationToken)` (devuelve el token plano), `Task<(string tokenPlano, Guid userId)?> RotarAsync(string tokenPlano, CancellationToken)` (null si inválido/reuso), `Task RevocarAsync(string tokenPlano, CancellationToken)`. El token plano se genera aleatorio (32 bytes) y se guarda hasheado SHA-256; la rotación revoca el anterior, devuelve el `userId` (para emitir el nuevo access JWT) y detecta reuso (si el token ya está revocado, revoca todos los activos del usuario).

- [ ] **Step 1: Implementación**

`Auth/ServicioRefreshTokens.cs`:
```csharp
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Auth;

public interface IServicioRefreshTokens
{
    Task<string> EmitirAsync(Guid userId, CancellationToken ct);
    Task<(string tokenPlano, Guid userId)?> RotarAsync(string tokenPlano, CancellationToken ct);
    Task RevocarAsync(string tokenPlano, CancellationToken ct);
}

public sealed class ServicioRefreshTokens(IdentityDbContext db, TimeProvider tiempo) : IServicioRefreshTokens
{
    private const int DiasVida = 7;

    public async Task<string> EmitirAsync(Guid userId, CancellationToken ct)
    {
        var (plano, hash) = GenerarToken();
        var ahora = tiempo.GetUtcNow();
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = userId, TokenHash = hash,
            CreadoEn = ahora, ExpiraEn = ahora.AddDays(DiasVida),
        });
        await db.SaveChangesAsync(ct);
        return plano;
    }

    public async Task<(string tokenPlano, Guid userId)?> RotarAsync(string tokenPlano, CancellationToken ct)
    {
        var hash = Hash(tokenPlano);
        var actual = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        var ahora = tiempo.GetUtcNow();

        if (actual is null) return null;
        if (!actual.EsActivo(ahora))
        {
            // Reuso de un token revocado/expirado: revoca todos los activos del usuario (defensa).
            await db.RefreshTokens
                .Where(t => t.UserId == actual.UserId && t.RevocadoEn == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevocadoEn, ahora), ct);
            return null;
        }

        var (nuevoPlano, nuevoHash) = GenerarToken();
        actual.RevocadoEn = ahora;
        actual.ReemplazadoPorHash = nuevoHash;
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = actual.UserId, TokenHash = nuevoHash,
            CreadoEn = ahora, ExpiraEn = ahora.AddDays(DiasVida),
        });
        await db.SaveChangesAsync(ct);
        return (nuevoPlano, actual.UserId);
    }

    public async Task RevocarAsync(string tokenPlano, CancellationToken ct)
    {
        var hash = Hash(tokenPlano);
        var actual = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (actual is { RevocadoEn: null })
        {
            actual.RevocadoEn = tiempo.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }
    }

    private static (string plano, string hash) GenerarToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plano = Convert.ToBase64String(bytes);
        return (plano, Hash(plano));
    }

    private static string Hash(string plano) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(plano)));
}
```
Registra `IServicioRefreshTokens` en `DependencyInjection.cs` (scoped).

- [ ] **Step 2: Test de integración (Testcontainers) del servicio**

`RefreshTokensTests.cs` usa `CaseritoApiFactory` para obtener un `IdentityDbContext` real (scope de servicios) y verifica: emitir crea un token activo; rotar devuelve uno nuevo e invalida el anterior; rotar con el anterior (ya revocado) devuelve null y revoca los activos del usuario (detección de reuso); revocar invalida. (Crea un `ApplicationUser` de prueba con `UserManager` o inserta uno; usa un `userId` fijo.)

- [ ] **Step 3: Verificar [Docker]**

Run (desde `CaseritoApp/`): `dotnet test CaseritoApp.sln`
Expected: verde, incluyendo `RefreshTokensTests` (Testcontainers).

- [ ] **Step 4: Commit**

```bash
cd ..
git add CaseritoApp/src/Identity CaseritoApp/tests/CaseritoApp.IntegrationTests
git commit -m "feat(identity): servicio de refresh tokens con rotación y detección de reuso"
```

---

### Task 5: Endpoints de auth (register/login/refresh/logout) — [Docker]

**Files:**
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/AuthFlowTests.cs`

**Interfaces:**
- Consumes: `UserManager<ApplicationUser>`, `SignInManager<ApplicationUser>`, `IGeneradorTokensAcceso`, `IServicioRefreshTokens`.
- Produces: grupo `/api/auth` con `register`, `login`, `refresh`, `logout`. `login`/`refresh` devuelven `{ accessToken }` y setean/rotan la cookie `refreshToken` (httpOnly, SameSite=Strict, Secure según entorno, Path=/api/auth).

- [ ] **Step 1: Endpoints**

`Endpoints/AuthEndpoints.cs` — grupo minimal API. Nombre de cookie `refreshToken`. Helper para setear la cookie con `Secure = !env.IsDevelopment() && !env.IsEnvironment("Testing")`. Contratos: `RegistroRequest(Email, Password, Nombre, Ciudad)`, `LoginRequest(Email, Password)`. Lógica:
- `register`: `UserManager.CreateAsync(new ApplicationUser{ UserName=email, Email=email, Nombre, Ciudad }, password)`; si falla → 400 con errores; si ok → 200.
- `login`: busca por email; `SignInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure:true)`; si falla → 401; si ok → genera access JWT + `EmitirAsync` refresh, setea cookie, devuelve `{ accessToken }`.
- `refresh`: lee cookie `refreshToken`; `RotarAsync` (devuelve `(tokenPlano, userId)?`); si null → 401 (borra cookie); si ok → carga el `ApplicationUser` por `userId` (`UserManager.FindByIdAsync`) para generar el nuevo access JWT con sus claims, setea la nueva cookie con el `tokenPlano` rotado, devuelve `{ accessToken }`.
- `logout`: lee cookie, `RevocarAsync`, borra cookie, 204.

En `Program.cs`: `app.MapAuthEndpoints();` (tras UseAuthentication/UseAuthorization).

- [ ] **Step 2: Test de flujo (Testcontainers)**

`AuthFlowTests.cs` (usa `CaseritoApiFactory`): 
- register (200) → login (200, devuelve accessToken + set-cookie refreshToken) → refresh usando la cookie (200, nuevo accessToken, nueva cookie) → logout (204) → refresh tras logout (401).
- login con password incorrecta → 401.
- Usa un `HttpClient` con `CookieContainer` (o `HttpClientHandler { UseCookies = true }`) para que la cookie fluya; `WebApplicationFactory` lo soporta con `CreateDefaultClient` + handler, o inspecciona `Set-Cookie` manualmente.

- [ ] **Step 3: Verificar [Docker]**

Run (desde `CaseritoApp/`): `dotnet test CaseritoApp.sln`
Expected: verde, incluyendo `AuthFlowTests`.

- [ ] **Step 4: Commit**

```bash
cd ..
git add CaseritoApp/src/Host/CaseritoApp.Host CaseritoApp/tests/CaseritoApp.IntegrationTests
git commit -m "feat(auth): endpoints register/login/refresh/logout con cookie de refresh"
```

---

### Task 6: Perfil vía CQRS — [Docker]

**Files:**
- Create: `.../Identity/CaseritoApp.Identity.Application/Perfil/ObtenerPerfilQuery.cs` (+ handler)
- Create: `.../Identity/CaseritoApp.Identity.Application/Perfil/ActualizarPerfilCommand.cs` (+ handler + validator)
- Create: abstracción para leer/actualizar el usuario (p. ej. `IRepositorioPerfil` en Application, implementada en Infrastructure con `UserManager`)
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PerfilEndpoints.cs`
- Modify: `Program.cs` (MapPerfilEndpoints; registrar Application de Identity en MediatR)
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/PerfilTests.cs`

**Interfaces:**
- Produces: `GET /api/perfil` (`[Authorize]`) → `{ id, email, nombre, ciudad }` del usuario del claim `sub`; `PUT /api/perfil` (`[Authorize]`) con `{ nombre, ciudad }` → actualiza; validación FluentValidation; `Result` traducido a 200/400.

- [ ] **Step 1: Abstracción de perfil (Application) + impl (Infrastructure)**

En `Identity.Application`: `IRepositorioPerfil { Task<PerfilDto?> ObtenerAsync(Guid userId, CancellationToken); Task<Result> ActualizarAsync(Guid userId, string nombre, string ciudad, CancellationToken); }` con `PerfilDto(Guid Id, string Email, string Nombre, string Ciudad)`. En `Identity.Infrastructure`: implementación con `UserManager<ApplicationUser>`. Registra la impl en `DependencyInjection`.

- [ ] **Step 2: Query + Command (CQRS)**

`ObtenerPerfilQuery(Guid UserId) : IQuery<Result<PerfilDto>>` + handler que llama `IRepositorioPerfil.ObtenerAsync` (404/`Result.Fallo` si no existe).
`ActualizarPerfilCommand(Guid UserId, string Nombre, string Ciudad) : ICommand` + handler + `AbstractValidator` (Nombre no vacío, longitudes razonables). Usa los tipos de `BuildingBlocks.Application.Messaging` y `Result`.
Registra el ensamblado de `Identity.Application` en MediatR (en `Program.cs`, amplía `AddMediatR(...RegisterServicesFromAssemblies(...))` para incluir Identity.Application; o registra por tipo marcador).

- [ ] **Step 3: Endpoints `[Authorize]`**

`PerfilEndpoints.cs`: `GET /api/perfil` y `PUT /api/perfil`, ambos `.RequireAuthorization()`. Obtienen el `userId` del claim `sub` (`ClaimsPrincipal`), despachan por MediatR, y traducen `Result` a 200 / 400 ProblemDetails.

- [ ] **Step 4: Test (Testcontainers)**

`PerfilTests.cs`: registra+login (obtiene accessToken), llama `GET /api/perfil` con `Authorization: Bearer` (200, datos correctos), `PUT /api/perfil` (200, cambia nombre/ciudad), vuelve a `GET` (refleja el cambio); `GET /api/perfil` sin token → 401.

- [ ] **Step 5: Verificar [Docker]**

Run (desde `CaseritoApp/`): `dotnet build && dotnet test CaseritoApp.sln`
Expected: build 0/0; verde incluyendo `PerfilTests`.

- [ ] **Step 6: Commit**

```bash
cd ..
git add CaseritoApp/src/Identity CaseritoApp/src/Host/CaseritoApp.Host CaseritoApp/tests
git commit -m "feat(perfil): ver/editar perfil vía CQRS con endpoints [Authorize]"
```

---

### Task 7: Documentación de auth en CLAUDE.md — [sin Docker]

**Files:**
- Modify: `CLAUDE.md` (raíz)

- [ ] **Step 1: Añadir sección `## Auth (Identity + JWT)`**

Documenta: ASP.NET Core Identity (`ApplicationUser`), access JWT corto (claims sub/email/name) + refresh en cookie httpOnly SameSite=Strict (Secure según entorno) con rotación y detección de reuso; endpoints `/api/auth/{register,login,refresh,logout}` y perfil `/api/perfil` (`[Authorize]`, CQRS); clave JWT en user-secrets/env (`Jwt:Key`), nunca versionada; refresh guardado hasheado. Comando de migración: `dotnet ef migrations add <Nombre> --project src/Identity/CaseritoApp.Identity.Infrastructure --startup-project src/Host/CaseritoApp.Host --output-dir Migrations`.

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: auth (Identity + JWT + refresh) en CLAUDE.md"
```

---

## Verificación end-to-end (al terminar)

Desde `CaseritoApp/`:
1. `dotnet build` 0/0; `dotnet format --verify-no-changes` limpio.
2. `dotnet test CaseritoApp.sln` verde: unit del generador JWT + integración (Testcontainers) de refresh, flujo de auth (register→login→refresh→logout, 401s), y perfil (get/put autenticado, 401 sin token).
3. Con user-secrets `Jwt:Key` y el stack dev (`rebuild.ps1`): `POST /api/auth/register` + `login` devuelven accessToken y setean cookie de refresh; `GET /api/perfil` con bearer → 200; `refresh` rota; `logout` invalida.
4. La migración `InicialIdentity` existe y crea tablas de Identity + `RefreshToken` bajo schema `identity`.
5. Sin secretos versionados; refresh persistido hasheado.
