# RBAC operativo — gestión de roles por admin — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permitir que un administrador (permiso `usuarios.gestionar`) liste roles, busque usuarios y asigne/quite roles vía API, con guardrails que eviten auto-bloqueo y escaladas.

**Architecture:** Backend puro. Espeja el patrón de Perfil: puerto `IRepositorioRolesUsuario` en `Identity.Application`, adaptador sobre `UserManager<ApplicationUser>` en `Identity.Infrastructure`, casos de uso CQRS-lite (MediatR + `Result` + FluentValidation), endpoints minimal API en el Host bajo el grupo `/api/admin` ya protegido por policy de permiso. Guardrails estáticos en validadores; guardrails con estado en handlers. Autorización stateless: los cambios de rol se propagan al siguiente refresh del access token (≤ 15 min).

**Tech Stack:** .NET 10, ASP.NET Core Identity, MediatR, FluentValidation, EF Core (SQL Server), xUnit, Testcontainers.MsSql.

## Global Constraints

- Nullable enable, warnings-as-errors, analizadores .NET + Roslynator + Sonar. Nada compila si viola las reglas.
- Namespaces file-scoped; `using` fuera del namespace, System primero.
- `PascalCase` tipos/miembros; `camelCase` locales; `_camelCase` campos privados; `I` en interfaces.
- Versiones de paquetes solo en `CaseritoApp/Directory.Packages.props` (CPM). Nunca `Version=` en un `.csproj`. **Este bloque no añade paquetes.**
- **Logging obligatorio con `[LoggerMessage]`** (delegados source-generated); prohibido `LogInformation(...)` con interpolación (CA1848). La clase que loguea debe ser `partial`.
- **Anti-PII (no negociable):** jamás loguear CI, imágenes de documento/selfie, tokens de sesión, cadenas de QR/pago, **ni email**. El email solo aparece en respuestas HTTP, nunca en logs.
- Textos de UI/comentarios en español.
- Comandos desde `CaseritoApp/`: `dotnet build CaseritoApp.sln`, `dotnet test CaseritoApp.sln`. Los tests de integración requieren Docker (Testcontainers).
- Convención de commits: `feat(identity): ...`, terminados con la línea `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

## File Structure

**Application** (`src/Identity/CaseritoApp.Identity.Application/Autorizacion/`):
- `RolDto.cs`, `UsuarioConRolesDto.cs`, `ResultadoPaginado.cs` — DTOs.
- `CodigosErrorRoles.cs` — códigos de error compartidos handler↔endpoint.
- `IRepositorioRolesUsuario.cs` — puerto.
- `ListarRolesQuery.cs`, `BuscarUsuariosQuery.cs`, `AsignarRolCommand.cs`, `QuitarRolCommand.cs` — casos de uso (record + handler + validator en un archivo, como Perfil).

**Infrastructure** (`src/Identity/CaseritoApp.Identity.Infrastructure/Autorizacion/`):
- `RepositorioRolesUsuarioUserManager.cs` — adaptador.
- Registro DI en `DependencyInjection.cs` (modificar).

**Host** (`src/Host/CaseritoApp.Host/Endpoints/`):
- `AdminEndpoints.cs` (modificar) — nuevos endpoints + mapeo `Result`→HTTP + helper de `AdminId`.

**Tests:**
- `tests/CaseritoApp.UnitTests/Autorizacion/` — validadores y `ListarRolesQuery`.
- `tests/CaseritoApp.IntegrationTests/` — `GestionRolesTests.cs`.

**Notas de contratos comunes** (mismo assembly `Identity.Application` que Perfil → MediatR y FluentValidation los descubren automáticamente; **no** hace falta tocar `Program.cs` para registrarlos):
- `ICommand` (sin retorno) : `IRequest<Result>`; `ICommandHandler<T> : IRequestHandler<T, Result>`.
- `IQuery<TResp> : IRequest<TResp>`; `IQueryHandler<TQ,TResp>`.
- `Result.Exito()`, `Result.Exito<T>(v)`, `Result.Fallo(Error)`, `Result.Fallo<T>(Error)`, `r.EsExito`, `r.Error`, `r.Valor`.
- `Error(string Code, string Message)`.
- El `ValidationBehavior` lanza `FluentValidation.ValidationException` cuando un validador falla; el endpoint la captura y devuelve `400 ValidationProblem` (patrón ya usado en `PerfilEndpoints.ActualizarAsync`).

---

### Task 1: Catálogo de roles (`GET /api/admin/roles`)

Slice vertical completo: query sin BD + endpoint + tests.

**Files:**
- Create: `src/Identity/CaseritoApp.Identity.Application/Autorizacion/RolDto.cs`
- Create: `src/Identity/CaseritoApp.Identity.Application/Autorizacion/ListarRolesQuery.cs`
- Modify: `src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs`
- Test: `tests/CaseritoApp.UnitTests/Autorizacion/ListarRolesQueryTests.cs`
- Test: `tests/CaseritoApp.IntegrationTests/GestionRolesTests.cs`

**Interfaces:**
- Consumes: `RolesApp.Todos`, `MapaRolesPermisos.PermisosDeRol(string)` (Domain, ya existentes).
- Produces: `RolDto(string Rol, IReadOnlyList<string> Permisos)`; `ListarRolesQuery : IQuery<IReadOnlyList<RolDto>>`.

- [ ] **Step 1: Escribir el test unit que falla**

`tests/CaseritoApp.UnitTests/Autorizacion/ListarRolesQueryTests.cs`:

```csharp
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class ListarRolesQueryTests
{
    [Fact]
    public async Task Devuelve_los_seis_roles_con_sus_permisos()
    {
        var handler = new ListarRolesQueryHandler();

        var roles = await handler.Handle(new ListarRolesQuery(), CancellationToken.None);

        Assert.Equal(RolesApp.Todos.Count, roles.Count);

        var adminPlataforma = roles.Single(r => r.Rol == RolesApp.AdminPlataforma);
        Assert.Equal(Permisos.Todos.Count, adminPlataforma.Permisos.Count);

        var cliente = roles.Single(r => r.Rol == RolesApp.Cliente);
        Assert.Empty(cliente.Permisos);
    }
}
```

- [ ] **Step 2: Ejecutar el test para verificar que falla**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~ListarRolesQueryTests`
Expected: FAIL de compilación (`ListarRolesQuery`/`RolDto` no existen).

- [ ] **Step 3: Crear `RolDto`**

`src/Identity/CaseritoApp.Identity.Application/Autorizacion/RolDto.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Un rol del MVP y los permisos que agrega (derivado de <c>MapaRolesPermisos</c>).</summary>
public sealed record RolDto(string Rol, IReadOnlyList<string> Permisos);
```

- [ ] **Step 4: Crear `ListarRolesQuery` + handler**

`src/Identity/CaseritoApp.Identity.Application/Autorizacion/ListarRolesQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.Identity.Domain.Autorizacion;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Consulta el catálogo de roles del MVP con los permisos que agrega cada uno.</summary>
public sealed record ListarRolesQuery : IQuery<IReadOnlyList<RolDto>>;

/// <summary>Handler de <see cref="ListarRolesQuery"/>: proyecta <c>RolesApp.Todos</c> sobre <c>MapaRolesPermisos</c>.</summary>
public sealed class ListarRolesQueryHandler : IQueryHandler<ListarRolesQuery, IReadOnlyList<RolDto>>
{
    public Task<IReadOnlyList<RolDto>> Handle(ListarRolesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<RolDto> roles = RolesApp.Todos
            .Select(rol => new RolDto(rol, MapaRolesPermisos.PermisosDeRol(rol)))
            .ToArray();

        return Task.FromResult(roles);
    }
}
```

- [ ] **Step 5: Ejecutar el test unit — debe pasar**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~ListarRolesQueryTests`
Expected: PASS.

- [ ] **Step 6: Añadir el endpoint `GET /api/admin/roles`**

Modificar `src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs`. Añadir `using`s y el mapeo. El archivo queda:

```csharp
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;
using MediatR;

namespace CaseritoApp.Host.Endpoints;

/// <summary>
/// Grupo minimal API <c>/api/admin</c>, protegido por la policy del permiso <c>usuarios.gestionar</c>:
/// ping de ejemplo, catálogo de roles, búsqueda de usuarios y gestión de roles por usuario.
/// </summary>
public static class AdminEndpoints
{
    /// <summary>Mapea el grupo <c>/api/admin</c> protegido por la policy del permiso <c>usuarios.gestionar</c>.</summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/admin")
            .RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.UsuariosGestionar));

        grupo.MapGet("/ping", () => Results.Ok(new { estado = "ok" }));
        grupo.MapGet("/roles", ListarRolesAsync);

        return app;
    }

    private static async Task<IResult> ListarRolesAsync(ISender sender, CancellationToken ct)
    {
        var roles = await sender.Send(new ListarRolesQuery(), ct);
        return Results.Ok(roles);
    }
}
```

- [ ] **Step 7: Escribir el test de integración del catálogo**

Crear `tests/CaseritoApp.IntegrationTests/GestionRolesTests.cs` con el helper de login (copiado del patrón de `AdminEndpointTests`) y el primer test:

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

/// <summary>Verifica los endpoints de gestión de roles bajo <c>/api/admin</c>.</summary>
public sealed class GestionRolesTests(CaseritoApiFactory factory) : IClassFixture<CaseritoApiFactory>
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

    private static HttpRequestMessage Autorizada(HttpMethod metodo, string url, string token)
    {
        var solicitud = new HttpRequestMessage(metodo, url);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return solicitud;
    }

    private static string Email(string prefijo) => $"{prefijo}-{Guid.NewGuid():N}@caserito.test";

    [Fact]
    public async Task Listar_roles_devuelve_200_con_admin()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("roles-list"), RolesApp.AdminPlataforma);

        using var solicitud = Autorizada(HttpMethod.Get, "/api/admin/roles", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Listar_roles_sin_permiso_devuelve_403()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("roles-403"), rolExtra: null);

        using var solicitud = Autorizada(HttpMethod.Get, "/api/admin/roles", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
```

- [ ] **Step 8: Ejecutar los tests de integración del catálogo (requiere Docker)**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~GestionRolesTests`
Expected: PASS (2 tests).

- [ ] **Step 9: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Application/Autorizacion/RolDto.cs \
        src/Identity/CaseritoApp.Identity.Application/Autorizacion/ListarRolesQuery.cs \
        src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs \
        tests/CaseritoApp.UnitTests/Autorizacion/ListarRolesQueryTests.cs \
        tests/CaseritoApp.IntegrationTests/GestionRolesTests.cs
git commit -m "feat(identity): catálogo de roles GET /api/admin/roles"
```

---

### Task 2: Puerto + adaptador + búsqueda de usuarios (`GET /api/admin/usuarios`)

**Files:**
- Create: `src/Identity/CaseritoApp.Identity.Application/Autorizacion/UsuarioConRolesDto.cs`
- Create: `src/Identity/CaseritoApp.Identity.Application/Autorizacion/ResultadoPaginado.cs`
- Create: `src/Identity/CaseritoApp.Identity.Application/Autorizacion/IRepositorioRolesUsuario.cs`
- Create: `src/Identity/CaseritoApp.Identity.Application/Autorizacion/BuscarUsuariosQuery.cs`
- Create: `src/Identity/CaseritoApp.Identity.Infrastructure/Autorizacion/RepositorioRolesUsuarioUserManager.cs`
- Modify: `src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs` (registro DI)
- Modify: `src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs`
- Test: `tests/CaseritoApp.UnitTests/Autorizacion/BuscarUsuariosQueryValidatorTests.cs`
- Test: `tests/CaseritoApp.IntegrationTests/GestionRolesTests.cs` (añadir tests)

**Interfaces:**
- Consumes: `UserManager<ApplicationUser>`; `ApplicationUser` (propiedades `Id`, `Email`, `Nombre`, `Ciudad`).
- Produces:
  - `UsuarioConRolesDto(Guid Id, string Email, string Nombre, string Ciudad, IReadOnlyList<string> Roles)`
  - `ResultadoPaginado<T>(IReadOnlyList<T> Items, int Pagina, int Tamano, int Total)`
  - `IRepositorioRolesUsuario` con: `BuscarUsuariosAsync`, `ExisteUsuarioAsync`, `ContarEnRolAsync`, `TieneRolAsync`, `AgregarRolAsync`, `QuitarRolAsync` (firmas exactas abajo).
  - `BuscarUsuariosQuery(string? Query, int Pagina, int Tamano) : IQuery<ResultadoPaginado<UsuarioConRolesDto>>`.

- [ ] **Step 1: Crear los DTOs**

`src/Identity/CaseritoApp.Identity.Application/Autorizacion/UsuarioConRolesDto.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Usuario con sus roles asignados, para la vista de administración. El email no se loguea.</summary>
public sealed record UsuarioConRolesDto(Guid Id, string Email, string Nombre, string Ciudad, IReadOnlyList<string> Roles);
```

`src/Identity/CaseritoApp.Identity.Application/Autorizacion/ResultadoPaginado.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Página de resultados con metadatos de paginación.</summary>
public sealed record ResultadoPaginado<T>(IReadOnlyList<T> Items, int Pagina, int Tamano, int Total);
```

- [ ] **Step 2: Crear el puerto `IRepositorioRolesUsuario`**

`src/Identity/CaseritoApp.Identity.Application/Autorizacion/IRepositorioRolesUsuario.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>
/// Puerto para consultar y mutar los roles de los usuarios sin depender de ASP.NET Core Identity.
/// El adaptador (sobre <c>UserManager</c>) vive en Infrastructure. Las operaciones de mutación son
/// idempotentes: agregar un rol ya presente o quitar uno ausente no es error.
/// </summary>
public interface IRepositorioRolesUsuario
{
    /// <summary>Página de usuarios (filtrada por email/nombre si <paramref name="query"/> no es vacío) con sus roles.</summary>
    public Task<ResultadoPaginado<UsuarioConRolesDto>> BuscarUsuariosAsync(
        string? query, int pagina, int tamano, CancellationToken cancellationToken);

    /// <summary>Indica si existe un usuario con ese id.</summary>
    public Task<bool> ExisteUsuarioAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Cantidad de usuarios que tienen el rol indicado.</summary>
    public Task<int> ContarEnRolAsync(string rol, CancellationToken cancellationToken);

    /// <summary>Indica si el usuario tiene el rol indicado.</summary>
    public Task<bool> TieneRolAsync(Guid userId, string rol, CancellationToken cancellationToken);

    /// <summary>Asigna el rol al usuario (idempotente si ya lo tiene).</summary>
    public Task<Result> AgregarRolAsync(Guid userId, string rol, CancellationToken cancellationToken);

    /// <summary>Quita el rol al usuario (idempotente si no lo tiene).</summary>
    public Task<Result> QuitarRolAsync(Guid userId, string rol, CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Escribir el test unit del validador de búsqueda (falla)**

`tests/CaseritoApp.UnitTests/Autorizacion/BuscarUsuariosQueryValidatorTests.cs`:

```csharp
using CaseritoApp.Identity.Application.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class BuscarUsuariosQueryValidatorTests
{
    private readonly BuscarUsuariosQueryValidator _validator = new();

    [Theory]
    [InlineData(0, 20)]   // pagina < 1
    [InlineData(1, 0)]    // tamano < 1
    [InlineData(1, 101)]  // tamano > 100
    public void Rechaza_paginacion_invalida(int pagina, int tamano)
    {
        var resultado = _validator.Validate(new BuscarUsuariosQuery(null, pagina, tamano));

        Assert.False(resultado.IsValid);
    }

    [Fact]
    public void Acepta_paginacion_valida()
    {
        var resultado = _validator.Validate(new BuscarUsuariosQuery("ana", 1, 20));

        Assert.True(resultado.IsValid);
    }
}
```

- [ ] **Step 4: Crear `BuscarUsuariosQuery` + handler + validator**

`src/Identity/CaseritoApp.Identity.Application/Autorizacion/BuscarUsuariosQuery.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using FluentValidation;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Busca usuarios (por email/nombre) paginados, con sus roles.</summary>
public sealed record BuscarUsuariosQuery(string? Query, int Pagina, int Tamano)
    : IQuery<ResultadoPaginado<UsuarioConRolesDto>>;

/// <summary>Handler de <see cref="BuscarUsuariosQuery"/>: delega en <see cref="IRepositorioRolesUsuario"/>.</summary>
public sealed class BuscarUsuariosQueryHandler(IRepositorioRolesUsuario repositorio)
    : IQueryHandler<BuscarUsuariosQuery, ResultadoPaginado<UsuarioConRolesDto>>
{
    public Task<ResultadoPaginado<UsuarioConRolesDto>> Handle(
        BuscarUsuariosQuery request, CancellationToken cancellationToken) =>
        repositorio.BuscarUsuariosAsync(request.Query, request.Pagina, request.Tamano, cancellationToken);
}

/// <summary>Valida la paginación: página ≥ 1 y tamaño en 1..100.</summary>
public sealed class BuscarUsuariosQueryValidator : AbstractValidator<BuscarUsuariosQuery>
{
    public BuscarUsuariosQueryValidator()
    {
        RuleFor(q => q.Pagina)
            .GreaterThanOrEqualTo(1).WithMessage("La página debe ser mayor o igual a 1.");

        RuleFor(q => q.Tamano)
            .InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
```

- [ ] **Step 5: Ejecutar el test unit del validador — debe pasar**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~BuscarUsuariosQueryValidatorTests`
Expected: PASS (4 casos).

- [ ] **Step 6: Crear el adaptador `RepositorioRolesUsuarioUserManager`**

`src/Identity/CaseritoApp.Identity.Infrastructure/Autorizacion/RepositorioRolesUsuarioUserManager.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Autorizacion;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CaseritoApp.Identity.Infrastructure.Autorizacion;

/// <summary>
/// Implementación de <see cref="IRepositorioRolesUsuario"/> sobre <see cref="UserManager{TUser}"/>.
/// La búsqueda proyecta roles por usuario con <c>GetRolesAsync</c> (N+1 acotado por el tamaño de
/// página; optimizable con join a AspNetUserRoles más adelante).
/// </summary>
public sealed class RepositorioRolesUsuarioUserManager(UserManager<ApplicationUser> userManager)
    : IRepositorioRolesUsuario
{
    public async Task<ResultadoPaginado<UsuarioConRolesDto>> BuscarUsuariosAsync(
        string? query, int pagina, int tamano, CancellationToken cancellationToken)
    {
        var consulta = userManager.Users;

        if (!string.IsNullOrWhiteSpace(query))
        {
            var patron = query.Trim();
            consulta = consulta.Where(u =>
                (u.Email != null && u.Email.Contains(patron)) || u.Nombre.Contains(patron));
        }

        var total = await consulta.CountAsync(cancellationToken);

        var usuarios = await consulta
            .OrderBy(u => u.Email)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(cancellationToken);

        var items = new List<UsuarioConRolesDto>(usuarios.Count);
        foreach (var usuario in usuarios)
        {
            var roles = await userManager.GetRolesAsync(usuario);
            items.Add(new UsuarioConRolesDto(
                usuario.Id, usuario.Email ?? string.Empty, usuario.Nombre, usuario.Ciudad, roles.ToArray()));
        }

        return new ResultadoPaginado<UsuarioConRolesDto>(items, pagina, tamano, total);
    }

    public async Task<bool> ExisteUsuarioAsync(Guid userId, CancellationToken cancellationToken) =>
        await userManager.FindByIdAsync(userId.ToString()) is not null;

    public async Task<int> ContarEnRolAsync(string rol, CancellationToken cancellationToken) =>
        (await userManager.GetUsersInRoleAsync(rol)).Count;

    public async Task<bool> TieneRolAsync(Guid userId, string rol, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        return usuario is not null && await userManager.IsInRoleAsync(usuario, rol);
    }

    public async Task<Result> AgregarRolAsync(Guid userId, string rol, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return Result.Fallo(new Error(CodigosErrorRoles.UsuarioNoEncontrado, "El usuario no existe."));
        }

        if (await userManager.IsInRoleAsync(usuario, rol))
        {
            return Result.Exito();
        }

        var resultado = await userManager.AddToRoleAsync(usuario, rol);
        return resultado.Succeeded
            ? Result.Exito()
            : Result.Fallo(new Error(
                "Roles.AsignacionFallida", string.Join("; ", resultado.Errors.Select(e => e.Description))));
    }

    public async Task<Result> QuitarRolAsync(Guid userId, string rol, CancellationToken cancellationToken)
    {
        var usuario = await userManager.FindByIdAsync(userId.ToString());
        if (usuario is null)
        {
            return Result.Fallo(new Error(CodigosErrorRoles.UsuarioNoEncontrado, "El usuario no existe."));
        }

        if (!await userManager.IsInRoleAsync(usuario, rol))
        {
            return Result.Exito();
        }

        var resultado = await userManager.RemoveFromRoleAsync(usuario, rol);
        return resultado.Succeeded
            ? Result.Exito()
            : Result.Fallo(new Error(
                "Roles.RetiroFallido", string.Join("; ", resultado.Errors.Select(e => e.Description))));
    }
}
```

- [ ] **Step 7: Crear `CodigosErrorRoles` (usado por el adaptador y por handlers/endpoint)**

`src/Identity/CaseritoApp.Identity.Application/Autorizacion/CodigosErrorRoles.cs`:

```csharp
namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Códigos de error de la gestión de roles, compartidos entre handlers y el mapeo HTTP del endpoint.</summary>
public static class CodigosErrorRoles
{
    /// <summary>El usuario objetivo no existe → HTTP 404.</summary>
    public const string UsuarioNoEncontrado = "Roles.UsuarioNoEncontrado";

    /// <summary>Se intentó quitar AdminPlataforma al último que lo tiene → HTTP 409.</summary>
    public const string UltimoAdminPlataforma = "Roles.UltimoAdminPlataforma";
}
```

- [ ] **Step 8: Registrar el adaptador en el DI de Identity**

Modificar `src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs`. Añadir el `using` y el registro junto al de `IRepositorioPerfil` (línea ~44):

Añadir al bloque de `using` del inicio:

```csharp
using CaseritoApp.Identity.Infrastructure.Autorizacion;
```

Y tras `servicios.AddScoped<IRepositorioPerfil, RepositorioPerfilUserManager>();`:

```csharp
servicios.AddScoped<IRepositorioRolesUsuario, RepositorioRolesUsuarioUserManager>();
```

(El `using CaseritoApp.Identity.Application.Autorizacion;` puede hacer falta para `IRepositorioRolesUsuario`; añadirlo si el compilador lo pide.)

- [ ] **Step 9: Añadir el endpoint `GET /api/admin/usuarios`**

En `AdminEndpoints.cs`, dentro de `MapAdminEndpoints`, tras `/roles`:

```csharp
        grupo.MapGet("/usuarios", BuscarUsuariosAsync);
```

Y el método (junto a `ListarRolesAsync`):

```csharp
    private static async Task<IResult> BuscarUsuariosAsync(
        ISender sender, CancellationToken ct, string? query = null, int pagina = 1, int tamano = 20)
    {
        try
        {
            var resultado = await sender.Send(new BuscarUsuariosQuery(query, pagina, tamano), ct);
            return Results.Ok(resultado);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errores = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.ValidationProblem(errores);
        }
    }
```

- [ ] **Step 10: Añadir tests de integración de búsqueda**

En `GestionRolesTests.cs`, añadir un record para deserializar y dos tests:

```csharp
    private sealed record UsuarioConRolesResponse(Guid Id, string Email, string Nombre, string Ciudad, string[] Roles);
    private sealed record PaginaUsuariosResponse(UsuarioConRolesResponse[] Items, int Pagina, int Tamano, int Total);

    [Fact]
    public async Task Buscar_usuarios_devuelve_al_usuario_por_email()
    {
        using var cliente = factory.CreateClient();
        var email = Email("busqueda");
        var token = await RegistrarYLoguearAsync(cliente, email, RolesApp.AdminPlataforma);

        using var solicitud = Autorizada(HttpMethod.Get, $"/api/admin/usuarios?query={Uri.EscapeDataString(email)}", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<PaginaUsuariosResponse>();
        Assert.Contains(pagina!.Items, u => u.Email == email);
        Assert.Contains(pagina.Items, u => u.Roles.Contains(RolesApp.AdminPlataforma));
    }

    [Fact]
    public async Task Buscar_usuarios_con_paginacion_invalida_devuelve_400()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("busqueda-400"), RolesApp.AdminPlataforma);

        using var solicitud = Autorizada(HttpMethod.Get, "/api/admin/usuarios?tamano=0", token);
        var respuesta = await cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
```

- [ ] **Step 11: Ejecutar toda la suite (requiere Docker)**

Run: `dotnet test CaseritoApp.sln`
Expected: PASS (incluye los nuevos unit + integración; ninguno previo se rompe).

- [ ] **Step 12: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Application/Autorizacion/ \
        src/Identity/CaseritoApp.Identity.Infrastructure/Autorizacion/ \
        src/Identity/CaseritoApp.Identity.Infrastructure/DependencyInjection.cs \
        src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs \
        tests/CaseritoApp.UnitTests/Autorizacion/BuscarUsuariosQueryValidatorTests.cs \
        tests/CaseritoApp.IntegrationTests/GestionRolesTests.cs
git commit -m "feat(identity): puerto de roles + búsqueda de usuarios GET /api/admin/usuarios"
```

---

### Task 3: Asignar rol (`POST /api/admin/usuarios/{id}/roles`)

**Files:**
- Create: `src/Identity/CaseritoApp.Identity.Application/Autorizacion/AsignarRolCommand.cs`
- Modify: `src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs`
- Test: `tests/CaseritoApp.UnitTests/Autorizacion/AsignarRolCommandValidatorTests.cs`
- Test: `tests/CaseritoApp.IntegrationTests/GestionRolesTests.cs` (añadir tests)

**Interfaces:**
- Consumes: `IRepositorioRolesUsuario` (Task 2), `RolesApp`, `CodigosErrorRoles` (Task 2).
- Produces:
  - `AsignarRolCommand(Guid AdminId, Guid TargetUserId, string Rol) : ICommand`.
  - En `AdminEndpoints`: `AsignarRolRequest(string Rol)`; helper `DesdeResult(Result)` (mapeo error→HTTP, reutilizado en Task 4); helper `TryObtenerAdminId(ClaimsPrincipal, out Guid)`.

- [ ] **Step 1: Escribir el test unit del validador (falla)**

`tests/CaseritoApp.UnitTests/Autorizacion/AsignarRolCommandValidatorTests.cs`:

```csharp
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class AsignarRolCommandValidatorTests
{
    private readonly AsignarRolCommandValidator _validator = new();

    [Fact]
    public void Acepta_rol_conocido()
    {
        var cmd = new AsignarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.Moderador);

        Assert.True(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_rol_desconocido()
    {
        var cmd = new AsignarRolCommand(Guid.NewGuid(), Guid.NewGuid(), "RolFalso");

        Assert.False(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_rol_Sistema()
    {
        var cmd = new AsignarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.Sistema);

        Assert.False(_validator.Validate(cmd).IsValid);
    }
}
```

- [ ] **Step 2: Ejecutar el test — debe fallar (no compila)**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~AsignarRolCommandValidatorTests`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `AsignarRolCommand` + handler + validator**

`src/Identity/CaseritoApp.Identity.Application/Autorizacion/AsignarRolCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Autorizacion;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Asigna un rol a un usuario. <paramref name="AdminId"/> es el administrador que ejecuta la acción (auditoría).</summary>
public sealed record AsignarRolCommand(Guid AdminId, Guid TargetUserId, string Rol) : ICommand;

/// <summary>Handler de <see cref="AsignarRolCommand"/>: valida existencia, asigna (idempotente) y audita sin PII.</summary>
public sealed partial class AsignarRolCommandHandler(
    IRepositorioRolesUsuario repositorio,
    ILogger<AsignarRolCommandHandler> logger)
    : ICommandHandler<AsignarRolCommand>
{
    public async Task<Result> Handle(AsignarRolCommand request, CancellationToken cancellationToken)
    {
        if (!await repositorio.ExisteUsuarioAsync(request.TargetUserId, cancellationToken))
        {
            RegistrarCambioRol(logger, "asignar", request.AdminId, request.TargetUserId, request.Rol, CodigosErrorRoles.UsuarioNoEncontrado);
            return Result.Fallo(new Error(CodigosErrorRoles.UsuarioNoEncontrado, "El usuario no existe."));
        }

        var resultado = await repositorio.AgregarRolAsync(request.TargetUserId, request.Rol, cancellationToken);

        RegistrarCambioRol(
            logger, "asignar", request.AdminId, request.TargetUserId, request.Rol,
            resultado.EsExito ? "ok" : resultado.Error.Code);

        return resultado;
    }

    // Auditoría sin PII: solo ids y rol; nunca email ni datos sensibles.
    [LoggerMessage(Level = LogLevel.Information, Message = "Cambio de rol {Accion}: admin={AdminId} target={TargetUserId} rol={Rol} resultado={Resultado}")]
    private static partial void RegistrarCambioRol(
        ILogger logger, string accion, Guid adminId, Guid targetUserId, string rol, string resultado);
}

/// <summary>Guardrails estáticos: solo roles conocidos y <c>Sistema</c> no asignable vía API.</summary>
public sealed class AsignarRolCommandValidator : AbstractValidator<AsignarRolCommand>
{
    public AsignarRolCommandValidator()
    {
        RuleFor(c => c.Rol)
            .Must(rol => RolesApp.Todos.Contains(rol))
            .WithMessage("El rol indicado no existe.")
            .Must(rol => rol != RolesApp.Sistema)
            .WithMessage("El rol Sistema no puede asignarse vía API.");
    }
}
```

- [ ] **Step 4: Ejecutar el test unit — debe pasar**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~AsignarRolCommandValidatorTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Añadir el endpoint POST, el helper de mapeo y el helper de AdminId**

En `AdminEndpoints.cs`:

1. Añadir `using`s que falten al inicio:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CaseritoApp.BuildingBlocks.Domain;
```

2. En `MapAdminEndpoints`, tras `/usuarios`:

```csharp
        grupo.MapPost("/usuarios/{id:guid}/roles", AsignarRolAsync);
```

3. Añadir el record de request (junto a la clase, nivel namespace):

```csharp
/// <summary>Cuerpo para asignar un rol a un usuario.</summary>
public sealed record AsignarRolRequest(string Rol);
```

4. Añadir los métodos:

```csharp
    private static async Task<IResult> AsignarRolAsync(
        Guid id, AsignarRolRequest request, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerAdminId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new AsignarRolCommand(adminId, id, request.Rol), ct);
            return DesdeResult(resultado);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errores = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.ValidationProblem(errores);
        }
    }

    private static bool TryObtenerAdminId(ClaimsPrincipal usuario, out Guid userId)
    {
        var valor = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(valor, out userId);
    }

    // Mapea el Result de los comandos de gestión de roles a códigos HTTP.
    private static IResult DesdeResult(Result resultado)
    {
        if (resultado.EsExito)
        {
            return Results.NoContent();
        }

        return resultado.Error.Code switch
        {
            CodigosErrorRoles.UsuarioNoEncontrado =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status404NotFound),
            CodigosErrorRoles.UltimoAdminPlataforma =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status409Conflict),
            _ =>
                Results.Problem(title: resultado.Error.Code, detail: resultado.Error.Message, statusCode: StatusCodes.Status400BadRequest),
        };
    }
```

- [ ] **Step 6: Añadir tests de integración de asignación (incluida propagación)**

En `GestionRolesTests.cs`. Se necesita el UserId del target; se obtiene vía `UserManager` en un scope. Añadir un helper y los tests:

```csharp
    private async Task<(string email, Guid id)> RegistrarClienteAsync(HttpClient cliente)
    {
        var email = Email("target");
        var registro = await cliente.PostAsJsonAsync(
            "/api/auth/register",
            new RegistroRequest(email, "Password123!", "Usuario", "Lima"));
        Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var usuario = await userManager.FindByEmailAsync(email);
        return (email, usuario!.Id);
    }

    [Fact]
    public async Task Asignar_rol_lo_refleja_en_la_busqueda_y_se_propaga_al_relogin()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-asignar"), RolesApp.AdminPlataforma);
        var (targetEmail, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Moderador));
        var respuesta = await cliente.SendAsync(asignar);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        // Se refleja en la búsqueda.
        using var buscar = Autorizada(HttpMethod.Get, $"/api/admin/usuarios?query={Uri.EscapeDataString(targetEmail)}", adminToken);
        var paginaResp = await cliente.SendAsync(buscar);
        var pagina = await paginaResp.Content.ReadFromJsonAsync<PaginaUsuariosResponse>();
        Assert.Contains(pagina!.Items, u => u.Id == targetId && u.Roles.Contains(RolesApp.Moderador));

        // Propagación: al (re)loguear, el JWT del target trae los permisos de Moderador.
        var login = await cliente.PostAsJsonAsync("/api/auth/login", new LoginRequest(targetEmail, "Password123!"));
        var tokenTarget = (await login.Content.ReadFromJsonAsync<TokenAccesoResponse>())!.AccessToken;
        var permisos = PermisosDelToken(tokenTarget);
        Assert.Contains(Permisos.PublicacionesModerar, permisos);
        Assert.Contains(Permisos.ChatModerar, permisos);
    }

    [Fact]
    public async Task Asignar_rol_desconocido_devuelve_400()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-rol-falso"), RolesApp.AdminPlataforma);
        var (_, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest("RolFalso"));
        var respuesta = await cliente.SendAsync(asignar);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Asignar_rol_Sistema_devuelve_400()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-sistema"), RolesApp.AdminPlataforma);
        var (_, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Sistema));
        var respuesta = await cliente.SendAsync(asignar);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Asignar_rol_a_usuario_inexistente_devuelve_404()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-404"), RolesApp.AdminPlataforma);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{Guid.NewGuid()}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Moderador));
        var respuesta = await cliente.SendAsync(asignar);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Asignar_rol_sin_permiso_devuelve_403()
    {
        using var cliente = factory.CreateClient();
        var token = await RegistrarYLoguearAsync(cliente, Email("no-admin"), rolExtra: null);
        var (_, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", token);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Moderador));
        var respuesta = await cliente.SendAsync(asignar);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
```

Añadir también el helper de decodificación de permisos del JWT y los `using` necesarios al inicio del archivo de test:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
```

```csharp
    private static IReadOnlyList<string> PermisosDelToken(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return jwt.Claims.Where(c => c.Type == "perm").Select(c => c.Value).ToArray();
    }
```

- [ ] **Step 7: Ejecutar la suite (requiere Docker)**

Run: `dotnet test CaseritoApp.sln`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Application/Autorizacion/AsignarRolCommand.cs \
        src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs \
        tests/CaseritoApp.UnitTests/Autorizacion/AsignarRolCommandValidatorTests.cs \
        tests/CaseritoApp.IntegrationTests/GestionRolesTests.cs
git commit -m "feat(identity): asignar rol POST /api/admin/usuarios/{id}/roles con auditoría sin PII"
```

---

### Task 4: Quitar rol (`DELETE /api/admin/usuarios/{id}/roles/{rol}`)

**Files:**
- Create: `src/Identity/CaseritoApp.Identity.Application/Autorizacion/QuitarRolCommand.cs`
- Modify: `src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs`
- Test: `tests/CaseritoApp.UnitTests/Autorizacion/QuitarRolCommandValidatorTests.cs`
- Test: `tests/CaseritoApp.IntegrationTests/GestionRolesTests.cs` (añadir tests)

**Interfaces:**
- Consumes: `IRepositorioRolesUsuario`, `RolesApp`, `CodigosErrorRoles`, `DesdeResult`, `TryObtenerAdminId` (Tasks 2-3).
- Produces: `QuitarRolCommand(Guid AdminId, Guid TargetUserId, string Rol) : ICommand`.

- [ ] **Step 1: Escribir el test unit del validador (falla)**

`tests/CaseritoApp.UnitTests/Autorizacion/QuitarRolCommandValidatorTests.cs`:

```csharp
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class QuitarRolCommandValidatorTests
{
    private readonly QuitarRolCommandValidator _validator = new();

    [Fact]
    public void Acepta_quitar_rol_conocido_a_otro_usuario()
    {
        var cmd = new QuitarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.Moderador);

        Assert.True(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_rol_desconocido()
    {
        var cmd = new QuitarRolCommand(Guid.NewGuid(), Guid.NewGuid(), "RolFalso");

        Assert.False(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_rol_Sistema()
    {
        var cmd = new QuitarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.Sistema);

        Assert.False(_validator.Validate(cmd).IsValid);
    }

    [Fact]
    public void Rechaza_auto_retiro_de_AdminPlataforma()
    {
        var mismoId = Guid.NewGuid();
        var cmd = new QuitarRolCommand(mismoId, mismoId, RolesApp.AdminPlataforma);

        Assert.False(_validator.Validate(cmd).IsValid);
    }
}
```

- [ ] **Step 2: Ejecutar el test — debe fallar (no compila)**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~QuitarRolCommandValidatorTests`
Expected: FAIL de compilación.

- [ ] **Step 3: Crear `QuitarRolCommand` + handler + validator**

`src/Identity/CaseritoApp.Identity.Application/Autorizacion/QuitarRolCommand.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Application.Messaging;
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Domain.Autorizacion;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CaseritoApp.Identity.Application.Autorizacion;

/// <summary>Quita un rol a un usuario. <paramref name="AdminId"/> es el administrador que ejecuta la acción (auditoría).</summary>
public sealed record QuitarRolCommand(Guid AdminId, Guid TargetUserId, string Rol) : ICommand;

/// <summary>
/// Handler de <see cref="QuitarRolCommand"/>: valida existencia, protege al último AdminPlataforma,
/// quita (idempotente) y audita sin PII.
/// </summary>
public sealed partial class QuitarRolCommandHandler(
    IRepositorioRolesUsuario repositorio,
    ILogger<QuitarRolCommandHandler> logger)
    : ICommandHandler<QuitarRolCommand>
{
    public async Task<Result> Handle(QuitarRolCommand request, CancellationToken cancellationToken)
    {
        if (!await repositorio.ExisteUsuarioAsync(request.TargetUserId, cancellationToken))
        {
            RegistrarCambioRol(logger, "quitar", request.AdminId, request.TargetUserId, request.Rol, CodigosErrorRoles.UsuarioNoEncontrado);
            return Result.Fallo(new Error(CodigosErrorRoles.UsuarioNoEncontrado, "El usuario no existe."));
        }

        // Guardrail: no dejar la plataforma sin ningún AdminPlataforma.
        if (request.Rol == RolesApp.AdminPlataforma
            && await repositorio.TieneRolAsync(request.TargetUserId, RolesApp.AdminPlataforma, cancellationToken)
            && await repositorio.ContarEnRolAsync(RolesApp.AdminPlataforma, cancellationToken) <= 1)
        {
            RegistrarCambioRol(logger, "quitar", request.AdminId, request.TargetUserId, request.Rol, CodigosErrorRoles.UltimoAdminPlataforma);
            return Result.Fallo(new Error(
                CodigosErrorRoles.UltimoAdminPlataforma,
                "No se puede quitar el rol al último administrador de plataforma."));
        }

        var resultado = await repositorio.QuitarRolAsync(request.TargetUserId, request.Rol, cancellationToken);

        RegistrarCambioRol(
            logger, "quitar", request.AdminId, request.TargetUserId, request.Rol,
            resultado.EsExito ? "ok" : resultado.Error.Code);

        return resultado;
    }

    // Auditoría sin PII: solo ids y rol; nunca email ni datos sensibles.
    [LoggerMessage(Level = LogLevel.Information, Message = "Cambio de rol {Accion}: admin={AdminId} target={TargetUserId} rol={Rol} resultado={Resultado}")]
    private static partial void RegistrarCambioRol(
        ILogger logger, string accion, Guid adminId, Guid targetUserId, string rol, string resultado);
}

/// <summary>
/// Guardrails estáticos: solo roles conocidos, <c>Sistema</c> no gestionable vía API, y un admin no
/// puede quitarse a sí mismo <c>AdminPlataforma</c>.
/// </summary>
public sealed class QuitarRolCommandValidator : AbstractValidator<QuitarRolCommand>
{
    public QuitarRolCommandValidator()
    {
        RuleFor(c => c.Rol)
            .Must(rol => RolesApp.Todos.Contains(rol))
            .WithMessage("El rol indicado no existe.")
            .Must(rol => rol != RolesApp.Sistema)
            .WithMessage("El rol Sistema no puede gestionarse vía API.");

        RuleFor(c => c)
            .Must(c => !(c.AdminId == c.TargetUserId && c.Rol == RolesApp.AdminPlataforma))
            .WithMessage("Un administrador no puede quitarse a sí mismo el rol AdminPlataforma.")
            .OverridePropertyName(nameof(QuitarRolCommand.Rol));
    }
}
```

- [ ] **Step 4: Ejecutar el test unit — debe pasar**

Run: `dotnet test CaseritoApp.sln --filter FullyQualifiedName~QuitarRolCommandValidatorTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Añadir el endpoint DELETE**

En `AdminEndpoints.cs`, en `MapAdminEndpoints`, tras el POST:

```csharp
        grupo.MapDelete("/usuarios/{id:guid}/roles/{rol}", QuitarRolAsync);
```

Y el método:

```csharp
    private static async Task<IResult> QuitarRolAsync(
        Guid id, string rol, ClaimsPrincipal admin, ISender sender, CancellationToken ct)
    {
        if (!TryObtenerAdminId(admin, out var adminId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var resultado = await sender.Send(new QuitarRolCommand(adminId, id, rol), ct);
            return DesdeResult(resultado);
        }
        catch (FluentValidation.ValidationException ex)
        {
            var errores = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            return Results.ValidationProblem(errores);
        }
    }
```

- [ ] **Step 6: Añadir tests de integración de retiro**

En `GestionRolesTests.cs`:

```csharp
    [Fact]
    public async Task Quitar_rol_lo_elimina_de_la_busqueda()
    {
        using var cliente = factory.CreateClient();
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-quitar"), RolesApp.AdminPlataforma);
        var (targetEmail, targetId) = await RegistrarClienteAsync(cliente);

        using var asignar = Autorizada(HttpMethod.Post, $"/api/admin/usuarios/{targetId}/roles", adminToken);
        asignar.Content = JsonContent.Create(new AsignarRolRequest(RolesApp.Moderador));
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(asignar)).StatusCode);

        using var quitar = Autorizada(HttpMethod.Delete, $"/api/admin/usuarios/{targetId}/roles/{RolesApp.Moderador}", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.SendAsync(quitar)).StatusCode);

        using var buscar = Autorizada(HttpMethod.Get, $"/api/admin/usuarios?query={Uri.EscapeDataString(targetEmail)}", adminToken);
        var pagina = await (await cliente.SendAsync(buscar)).Content.ReadFromJsonAsync<PaginaUsuariosResponse>();
        Assert.DoesNotContain(pagina!.Items, u => u.Id == targetId && u.Roles.Contains(RolesApp.Moderador));
    }

    // Nota: el 409 "último AdminPlataforma" (conteo global = 1) se cubre en unit (Task 4, Step 6b),
    // porque la BD de integración es compartida y suele tener varios AdminPlataforma. Aquí se verifica
    // el caso complementario: con otro admin presente, quitar AdminPlataforma a un tercero es 204.
    [Fact]
    public async Task Quitar_AdminPlataforma_con_otro_admin_presente_devuelve_204()
    {
        using var cliente = factory.CreateClient();
        // El ejecutor es AdminPlataforma; garantiza que exista >1 admin en BD.
        var adminToken = await RegistrarYLoguearAsync(cliente, Email("admin-ejecutor"), RolesApp.AdminPlataforma);

        // El objetivo también es AdminPlataforma (distinto del ejecutor): quitarle el rol no lo deja sin admins.
        var (_, targetId) = await RegistrarClienteAsync(cliente);
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var target = await userManager.FindByIdAsync(targetId.ToString());
            await userManager.AddToRoleAsync(target!, RolesApp.AdminPlataforma);
        }

        using var quitar = Autorizada(HttpMethod.Delete, $"/api/admin/usuarios/{targetId}/roles/{RolesApp.AdminPlataforma}", adminToken);
        var respuesta = await cliente.SendAsync(quitar);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task Auto_retiro_de_AdminPlataforma_devuelve_400()
    {
        using var cliente = factory.CreateClient();
        var email = Email("admin-auto");
        var adminToken = await RegistrarYLoguearAsync(cliente, email, RolesApp.AdminPlataforma);

        Guid adminId;
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            adminId = (await userManager.FindByEmailAsync(email))!.Id;
        }

        using var quitar = Autorizada(HttpMethod.Delete, $"/api/admin/usuarios/{adminId}/roles/{RolesApp.AdminPlataforma}", adminToken);
        var respuesta = await cliente.SendAsync(quitar);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
```

- [ ] **Step 6b: Test unit del guardrail "último AdminPlataforma" en el handler (repositorio en memoria)**

El 409 con conteo=1 se cubre en unit para no depender del estado global de la BD compartida entre tests. Añadir a `tests/CaseritoApp.UnitTests/Autorizacion/QuitarRolCommandHandlerTests.cs`:

```csharp
using CaseritoApp.BuildingBlocks.Domain;
using CaseritoApp.Identity.Application.Autorizacion;
using CaseritoApp.Identity.Domain.Autorizacion;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaseritoApp.UnitTests.Autorizacion;

public sealed class QuitarRolCommandHandlerTests
{
    // Repositorio en memoria: el usuario es AdminPlataforma y es el único (conteo=1).
    private sealed class RepoUltimoAdmin : IRepositorioRolesUsuario
    {
        public Task<ResultadoPaginado<UsuarioConRolesDto>> BuscarUsuariosAsync(string? query, int pagina, int tamano, CancellationToken ct) =>
            Task.FromResult(new ResultadoPaginado<UsuarioConRolesDto>([], pagina, tamano, 0));
        public Task<bool> ExisteUsuarioAsync(Guid userId, CancellationToken ct) => Task.FromResult(true);
        public Task<int> ContarEnRolAsync(string rol, CancellationToken ct) => Task.FromResult(1);
        public Task<bool> TieneRolAsync(Guid userId, string rol, CancellationToken ct) => Task.FromResult(true);
        public Task<Result> AgregarRolAsync(Guid userId, string rol, CancellationToken ct) => Task.FromResult(Result.Exito());
        public Task<Result> QuitarRolAsync(Guid userId, string rol, CancellationToken ct) => Task.FromResult(Result.Exito());
    }

    [Fact]
    public async Task Quitar_ultimo_AdminPlataforma_falla_con_codigo_UltimoAdminPlataforma()
    {
        var handler = new QuitarRolCommandHandler(new RepoUltimoAdmin(), NullLogger<QuitarRolCommandHandler>.Instance);
        var cmd = new QuitarRolCommand(Guid.NewGuid(), Guid.NewGuid(), RolesApp.AdminPlataforma);

        var resultado = await handler.Handle(cmd, CancellationToken.None);

        Assert.False(resultado.EsExito);
        Assert.Equal(CodigosErrorRoles.UltimoAdminPlataforma, resultado.Error.Code);
    }
}
```

- [ ] **Step 7: Ejecutar la suite completa (requiere Docker)**

Run: `dotnet test CaseritoApp.sln`
Expected: PASS (toda la suite, incluidos los previos).

- [ ] **Step 8: Verificar formato como el CI**

Run: `dotnet format CaseritoApp.sln --verify-no-changes`
Expected: sin cambios pendientes (exit 0).

- [ ] **Step 9: Commit**

```bash
git add src/Identity/CaseritoApp.Identity.Application/Autorizacion/QuitarRolCommand.cs \
        src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs \
        tests/CaseritoApp.UnitTests/Autorizacion/QuitarRolCommandValidatorTests.cs \
        tests/CaseritoApp.UnitTests/Autorizacion/QuitarRolCommandHandlerTests.cs \
        tests/CaseritoApp.IntegrationTests/GestionRolesTests.cs
git commit -m "feat(identity): quitar rol DELETE /api/admin/usuarios/{id}/roles/{rol} con guardrails"
```

---

## Notas de decisión

- **Guardrail "último AdminPlataforma" (409):** se prueba de forma fiable en **unit** (Step 6b del Task 4) con un repositorio en memoria de conteo=1, porque los tests de integración comparten una BD y crean varios AdminPlataforma, lo que hace frágil forzar "conteo global = 1" desde HTTP. El test de integración correspondiente documenta ese matiz y verifica que con 2+ admins la operación es NoContent.
- **Propagación de permisos:** validada decodificando los claims `perm` del access token tras un nuevo login del usuario objetivo (Task 3, Step 6). Es el comportamiento esperado del diseño stateless (≤ 15 min).
- **Anti-PII:** el email nunca entra en logs; la auditoría solo registra `adminId`, `targetUserId`, `rol`, `accion`, `resultado` vía `[LoggerMessage]`.

## Self-Review (cobertura del spec)

- Sección 1 (puerto/adaptador) → Task 2 (puerto + adaptador + DI). ✔
- Sección 2 (casos de uso + DTOs) → `ListarRolesQuery` (Task 1), `BuscarUsuariosQuery` (Task 2), `AsignarRolCommand` (Task 3), `QuitarRolCommand` (Task 4); DTOs en Tasks 1-2. ✔
- Sección 3 (guardrails) → estáticos en validadores (Tasks 3-4); con estado (404/409/idempotencia) en handlers y adaptador (Tasks 2-4). ✔
- Sección 4 (endpoints + mapeo HTTP) → Tasks 1-4; helper `DesdeResult` (Task 3). ✔
- Sección 5 (auditoría + anti-PII) → `[LoggerMessage]` en handlers (Tasks 3-4). ✔
- Sección 6 (tests) → unit (validadores + `ListarRolesQuery` + guardrail último admin) y integración (asignar/quitar/propagación/403/404/400/paginación). ✔
