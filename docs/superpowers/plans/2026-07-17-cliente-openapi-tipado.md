# Cliente OpenAPI tipado — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reemplazar la capa de API a mano de `web/src/api/` por un cliente TypeScript tipado generado del OpenAPI de la Web API .NET, con check de deriva en CI.

**Architecture:** El host .NET expone OpenAPI vía `Microsoft.AspNetCore.OpenApi` y emite `openapi.json` en `dotnet build` (sin levantar servidor). Cada minimal API se anota con metadata (`.Produces<T>()`, `.Accepts<T>()`) sin reescribir su handler. El frontend genera tipos con `openapi-typescript` y consume la API con `openapi-fetch` sobre un transporte custom que conserva el refresh-on-401 y el `HttpError` sin PII. CI regenera y falla si hay diff.

**Tech Stack:** .NET 10 (minimal APIs, `Microsoft.AspNetCore.OpenApi`, `Microsoft.Extensions.ApiDescription.Server`), React 19 + TypeScript, Vite, Vitest, `openapi-typescript`, `openapi-fetch`.

## Global Constraints

- **CPM:** versiones de paquetes .NET solo en `CaseritoApp/Directory.Packages.props`. Nunca `Version=` en un `.csproj`.
- **Backend rigor:** nullable enable, warnings-as-errors, analizadores (Roslynator + Sonar). Nada compila si viola las reglas. Namespaces file-scoped; `using` fuera del namespace, System primero.
- **Frontend rigor:** TypeScript estricto, ESLint + Prettier, Vitest. `npm run lint`, `npm run typecheck`, `npm run test`, `npm run build` deben quedar verdes.
- **Idioma:** textos de UI y comentarios en **español** (con acentos correctos).
- **Anti-PII (NO negociable):** jamás loguear ni exponer CI, imágenes de documento/selfie, tokens de sesión ni cadenas de QR/pago. El mapeo de errores solo expone status + `code`/`title` de ProblemDetails.
- **Ramas:** trabajo en `feat/cliente-openapi-tipado`. Commits del implementer: solo `git add <archivos>` + `git commit`. Prohibido `reset`/`rebase`/`checkout`/`amend`/`push`.
- **Node 22 / .NET 10** (según `global.json` y CI).
- **Comandos backend** desde `CaseritoApp/`; **comandos frontend** desde `web/`.

---

## Estructura de archivos

**Backend (crear/modificar):**
- Modify: `CaseritoApp/Directory.Packages.props` — versiones de los 2 paquetes OpenAPI.
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj` — referencias + emisión en build.
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs` — `AddOpenApi` + `MapOpenApi`.
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/OpenApi/SecuritySchemeTransformer.cs` — Bearer JWT.
- Modify: `Endpoints/AuthEndpoints.cs`, `PerfilEndpoints.cs`, `KycEndpoints.cs`, `AdminEndpoints.cs` — metadata `.Produces/.Accepts`.
- Create (emitido, committeado): `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`.

**Frontend (crear/modificar/borrar):**
- Modify: `web/package.json` — devdeps + script `generate:api`.
- Create (generado, committeado): `web/src/api/schema.d.ts`.
- Create: `web/src/api/http.ts` — transporte + cliente `openapi-fetch` + `HttpError` + `desempaquetar`.
- Create: `web/src/api/http.test.ts` — tests del transporte (portados de `client.test.ts`).
- Modify: `web/src/api/auth.ts`, `perfil.ts`, `kyc.ts` — sobre el cliente tipado.
- Modify: `web/src/api/kyc.test.ts` — mock del nuevo módulo.
- Delete: `web/src/api/client.ts`, `web/src/api/client.test.ts`.

**CI:**
- Modify: `.github/workflows/ci.yml` — job `contract`.

---

## Task 1: Backend — habilitar OpenAPI y emisión en build

**Files:**
- Modify: `CaseritoApp/Directory.Packages.props`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs:1-32`
- Create: `CaseritoApp/src/Host/CaseritoApp.Host/OpenApi/SecuritySchemeTransformer.cs`
- Create (emitido): `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`

**Interfaces:**
- Produces: documento OpenAPI emitido en `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` con `securitySchemes.Bearer` (http/bearer/JWT). El nombre del archivo emitido depende de la tooling; **se verifica en el Paso 5** y se usa ese nombre real en todo lo posterior (script npm, CI).

- [ ] **Step 1: Añadir versiones de paquetes (CPM)**

En `CaseritoApp/Directory.Packages.props`, dentro del primer `<ItemGroup>` de orquestación/persistencia, agregar:

```xml
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.ApiDescription.Server" Version="10.0.0" />
```

- [ ] **Step 2: Referenciar los paquetes y activar la emisión en build**

En `CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj`, agregar al `<ItemGroup>` de `PackageReference`:

```xml
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
    <PackageReference Include="Microsoft.Extensions.ApiDescription.Server">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
```

Y agregar un `<PropertyGroup>` nuevo que fije la emisión y el directorio de salida:

```xml
  <PropertyGroup>
    <!-- Emite el documento OpenAPI en cada build del host, sin levantar el servidor.
         El JSON emitido se versiona como contrato para generar el cliente TS. -->
    <OpenApiGenerateDocumentsOnBuild>true</OpenApiGenerateDocumentsOnBuild>
    <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)/../../../artifacts/openapi</OpenApiDocumentsDirectory>
  </PropertyGroup>
```

- [ ] **Step 3: Crear el document transformer del Bearer JWT**

Crear `CaseritoApp/src/Host/CaseritoApp.Host/OpenApi/SecuritySchemeTransformer.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

namespace CaseritoApp.Host.OpenApi;

/// <summary>
/// Registra el esquema de seguridad Bearer JWT en el documento OpenAPI y lo aplica como
/// requisito global, para que el contrato refleje los endpoints <c>[Authorize]</c>.
/// </summary>
internal sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var esquema = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme },
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = esquema;
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement { [esquema] = [] });

        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4: Registrar OpenAPI en `Program.cs`**

En `CaseritoApp/src/Host/CaseritoApp.Host/Program.cs`, añadir el `using` (con los demás, orden alfabético del bloque `CaseritoApp.*`):

```csharp
using CaseritoApp.Host.OpenApi;
```

Después de `builder.Services.AgregarAutenticacionJwt(...)` (línea ~30), antes de `var app = builder.Build();`:

```csharp
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<SecuritySchemeTransformer>());
```

Después de `var app = builder.Build();` (antes de `app.UseAuthentication()`):

```csharp
// Expone el documento en dev (/openapi/v1.json). El contrato de generación sale del build, no de aquí.
app.MapOpenApi();
```

- [ ] **Step 5: Build y verificar la emisión del documento**

Run (desde `CaseritoApp/`): `dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj`
Expected: build correcto (0 warnings/errores). Se crea un archivo JSON bajo `CaseritoApp/artifacts/openapi/`.

Verificar el nombre real emitido:
Run (desde `CaseritoApp/`): `ls artifacts/openapi/`
Expected: un archivo `.json` (típicamente `CaseritoApp.Host.json`). **Anotar el nombre exacto** — se usa en Task 3 (script npm) y Task 8 (CI). Si el nombre no fuera `CaseritoApp.Host.json`, usar el nombre real en todas las referencias posteriores.

Verificar que el contrato tiene el security scheme:
Run (desde `CaseritoApp/`): `cat artifacts/openapi/CaseritoApp.Host.json | grep -i "Bearer"`
Expected: aparece `"Bearer"` en `securitySchemes`.

- [ ] **Step 6: Ejecutar la suite backend (sin regresiones)**

Run (desde `CaseritoApp/`): `dotnet test CaseritoApp.sln`
Expected: todos los tests verdes (requiere Docker para los de integración; si Docker no está disponible en el entorno del subagente, correr al menos `dotnet build CaseritoApp.sln --configuration Release` y anotarlo).

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/Directory.Packages.props CaseritoApp/src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj CaseritoApp/src/Host/CaseritoApp.Host/Program.cs CaseritoApp/src/Host/CaseritoApp.Host/OpenApi/SecuritySchemeTransformer.cs CaseritoApp/artifacts/openapi/CaseritoApp.Host.json
git commit -m "feat(api): exponer OpenAPI del host y emitirlo en build (Bearer JWT)"
```

---

## Task 2: Backend — anotar endpoints con metadata OpenAPI

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs:25-35`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PerfilEndpoints.cs:17-25`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs:21-38`
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs:22-34`
- Modify (regenerado): `CaseritoApp/artifacts/openapi/CaseritoApp.Host.json`

**Interfaces:**
- Consumes: DTOs ya existentes — `RegistroRequest`, `LoginRequest`, `TokenAccesoResponse` (`AuthEndpoints.cs`); `ActualizarPerfilRequest` (`PerfilEndpoints.cs`), `PerfilDto` (`CaseritoApp.Identity.Application.Perfil`); `RechazarKycRequest` (`KycEndpoints.cs`), `EstadoKycDto`, `SolicitudKycResumenDto` (`CaseritoApp.Identity.Application.Kyc`), `ResultadoPaginado<T>` (BuildingBlocks); `AsignarRolRequest`, `RolDto`, `UsuarioConRolesDto` (`CaseritoApp.Identity.Application.Autorizacion`).
- Produces: contrato OpenAPI con request/response/status por endpoint. **Nota:** los cuerpos de los handlers NO se tocan — solo se encadena metadata a los `Map*`.

- [ ] **Step 1: Anotar `AuthEndpoints`**

En `MapAuthEndpoints` (`AuthEndpoints.cs`), reemplazar el bloque de mapeos por:

```csharp
        grupo.MapPost("/register", RegistrarAsync)
            .Accepts<RegistroRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        grupo.MapPost("/login", LoginAsync)
            .Accepts<LoginRequest>("application/json")
            .Produces<TokenAccesoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/refresh", RefreshAsync)
            .Produces<TokenAccesoResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPost("/logout", LogoutAsync)
            .Produces(StatusCodes.Status204NoContent);
```

- [ ] **Step 2: Anotar `PerfilEndpoints`**

En `MapPerfilEndpoints` (`PerfilEndpoints.cs`), reemplazar los dos mapeos por (nótese que `ActualizarAsync` devuelve `Results.Ok()` → **200 vacío**, no 204):

```csharp
        grupo.MapGet("/", ObtenerAsync)
            .Produces<PerfilDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        grupo.MapPut("/", ActualizarAsync)
            .Accepts<ActualizarPerfilRequest>("application/json")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();
```

Añadir el `using` si falta (ya está `CaseritoApp.Identity.Application.Perfil`; confirmar que `PerfilDto` resuelve).

- [ ] **Step 3: Anotar `KycEndpoints`**

En `MapKycEndpoints` (`KycEndpoints.cs`), reemplazar los mapeos por:

```csharp
        var usuario = app.MapGroup("/api/kyc").RequireAuthorization();
        usuario.MapPost("/", EnviarAsync).DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();
        usuario.MapGet("/estado", EstadoAsync)
            .Produces<EstadoKycDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        var admin = app.MapGroup("/api/admin/kyc")
            .RequireAuthorization(PoliticasAutorizacion.Permiso(Permisos.KycRevisar));
        admin.MapGet("/", ListarAsync)
            .Produces<ResultadoPaginado<SolicitudKycResumenDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();
        admin.MapGet("/{solicitudId:guid}/documento", (Guid solicitudId, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => BlobAsync(solicitudId, TipoBlobKyc.Documento, u, s, ct))
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);
        admin.MapGet("/{solicitudId:guid}/selfie", (Guid solicitudId, ClaimsPrincipal u, ISender s, CancellationToken ct)
            => BlobAsync(solicitudId, TipoBlobKyc.Selfie, u, s, ct))
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .ProducesProblem(StatusCodes.Status404NotFound);
        admin.MapPost("/{solicitudId:guid}/aprobar", AprobarAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        admin.MapPost("/{solicitudId:guid}/rechazar", RechazarAsync)
            .Accepts<RechazarKycRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();
```

Añadir el `using` de `ResultadoPaginado<T>` si el compilador lo pide (namespace de BuildingBlocks Application; el símbolo ya se usa transitivamente vía la query).

- [ ] **Step 4: Anotar `AdminEndpoints` (roles/usuarios) para un contrato completo**

En `MapAdminEndpoints` (`AdminEndpoints.cs`), reemplazar los mapeos por (el frontend no los consume en este bloque, pero se anotan para que `schema.d.ts` no genere tipos inline vacíos):

```csharp
        grupo.MapGet("/ping", () => Results.Ok(new { estado = "ok" }))
            .Produces(StatusCodes.Status200OK);
        grupo.MapGet("/roles", ListarRolesAsync)
            .Produces<IReadOnlyList<RolDto>>(StatusCodes.Status200OK);
        grupo.MapGet("/usuarios", BuscarUsuariosAsync)
            .Produces<ResultadoPaginado<UsuarioConRolesDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();
        grupo.MapPost("/usuarios/{id:guid}/roles", AsignarRolAsync)
            .Accepts<AsignarRolRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();
        grupo.MapDelete("/usuarios/{id:guid}/roles/{rol}", QuitarRolAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesValidationProblem();
```

Añadir los `using` que pida el compilador (`CaseritoApp.Identity.Application.Autorizacion` para `RolDto`/`UsuarioConRolesDto`; el de `ResultadoPaginado<T>`). Verificar la firma real de `ListarRolesQuery` (si devuelve `IReadOnlyList<RolDto>` u otra colección) y ajustar el genérico de `.Produces<...>` para que coincida exactamente con el tipo retornado.

- [ ] **Step 5: Regenerar y verificar el contrato**

Run (desde `CaseritoApp/`): `dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj`
Expected: build correcto; `artifacts/openapi/CaseritoApp.Host.json` regenerado.

Run (desde `CaseritoApp/`): `cat artifacts/openapi/CaseritoApp.Host.json | grep -i "TokenAccesoResponse"`
Expected: aparece `TokenAccesoResponse` en `components.schemas` (evidencia de que las respuestas se tiparon).

- [ ] **Step 6: Suite backend**

Run (desde `CaseritoApp/`): `dotnet test CaseritoApp.sln`
Expected: verde (o `dotnet build --configuration Release` si no hay Docker; anotarlo).

- [ ] **Step 7: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AuthEndpoints.cs CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PerfilEndpoints.cs CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/KycEndpoints.cs CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/AdminEndpoints.cs CaseritoApp/artifacts/openapi/CaseritoApp.Host.json
git commit -m "feat(api): anotar endpoints con metadata OpenAPI (request/response/status)"
```

---

## Task 3: Frontend — dependencias, script de generación y `schema.d.ts`

**Files:**
- Modify: `web/package.json:5-15,31-54`
- Create (generado): `web/src/api/schema.d.ts`

**Interfaces:**
- Produces: `web/src/api/schema.d.ts` exportando `paths` y `components` tipados. Los **path keys** (p. ej. `"/api/perfil/"`, `"/api/kyc/"`) reflejan las rutas reales del contrato (posible barra final por los `MapGroup(...).Map*("/")`). Tasks 4-6 deben usar los keys **exactamente** como aparezcan en este archivo.

- [ ] **Step 1: Añadir devdeps y script**

En `web/package.json`, agregar a `devDependencies`:

```json
    "openapi-typescript": "^7.9.1",
    "openapi-fetch": "^0.14.0",
```

(Nota: `openapi-fetch` es runtime, pero se instala como dependencia; ubicarlo en `dependencies` es igualmente válido. Colocarlo en `dependencies` junto a los demás runtime deps.)

En `scripts`, agregar:

```json
    "generate:api": "openapi-typescript ../CaseritoApp/artifacts/openapi/CaseritoApp.Host.json -o src/api/schema.d.ts",
```

(Usar el nombre de archivo verificado en Task 1 Step 5.)

- [ ] **Step 2: Instalar**

Run (desde `web/`): `npm install`
Expected: instala sin errores; `package-lock.json` actualizado.

- [ ] **Step 3: Generar los tipos**

Run (desde `web/`): `npm run generate:api`
Expected: se crea `web/src/api/schema.d.ts`. 

Run (desde `web/`): `grep -c "PerfilDto\|EstadoKycDto\|TokenAccesoResponse" src/api/schema.d.ts`
Expected: > 0 (los esquemas nombrados están presentes).

**Anotar los path keys reales** presentes en `schema.d.ts` (buscar `interface paths`) para usarlos literalmente en Tasks 4-6.

- [ ] **Step 4: Typecheck y lint**

Run (desde `web/`): `npm run typecheck`
Expected: sin errores (el `schema.d.ts` generado compila).

Run (desde `web/`): `npm run lint`
Expected: sin errores. Si ESLint marca el archivo generado, añadir `src/api/schema.d.ts` a los ignores de ESLint (en `eslint.config.js`, patrón `ignores`).

- [ ] **Step 5: Commit**

```bash
git add web/package.json web/package-lock.json web/src/api/schema.d.ts
git commit -m "chore(web): generar tipos del OpenAPI (openapi-typescript) y script generate:api"
```

---

## Task 4: Frontend — transporte `http.ts` (refresh-on-401 + HttpError) con TDD

**Files:**
- Create: `web/src/api/http.ts`
- Create: `web/src/api/http.test.ts`

**Interfaces:**
- Consumes: `web/src/api/schema.d.ts` (`paths`); `../auth/session` (`getAccessToken`, `setAccessToken`, `clearAccessToken`).
- Produces:
  - `class HttpError extends Error { status: number; code: string | null; }`
  - `const api` — cliente `openapi-fetch` tipado con `paths`, configurado con el transporte custom.
  - `desempaquetar<T>(resultado: { data?: T; error?: unknown; response: Response }): T` — devuelve `data` si ok; si no, lanza `HttpError(status, code, mensaje)` con `code` leído de `title` de ProblemDetails (sin PII). Para respuestas sin cuerpo (204/200 vacío) devuelve `undefined as T`.

- [ ] **Step 1: Escribir los tests del transporte (fallan)**

Crear `web/src/api/http.test.ts` (portados de `client.test.ts`, apuntando al nuevo módulo):

```typescript
import { describe, it, expect, vi, afterEach } from 'vitest';
import { api, desempaquetar, HttpError } from './http';
import { setAccessToken, getAccessToken, clearAccessToken } from '../auth/session';

afterEach(() => {
  vi.restoreAllMocks();
  clearAccessToken();
});

function respuesta(status: number, cuerpo?: unknown) {
  return new Response(cuerpo === undefined ? '' : JSON.stringify(cuerpo), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

describe('http refresh-on-401', () => {
  it('ante 401 refresca una vez y reintenta con el nuevo token', async () => {
    setAccessToken('viejo');
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(respuesta(401)) // GET original
      .mockResolvedValueOnce(respuesta(200, { accessToken: 'nuevo' })) // refresh
      .mockResolvedValueOnce(respuesta(200, { id: '1', email: 'a@b.c', nombre: 'A', ciudad: 'LP' })); // reintento

    const r = await api.GET('/api/perfil');
    const data = desempaquetar(r);
    expect((data as { email: string }).email).toBe('a@b.c');
    expect(getAccessToken()).toBe('nuevo');
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it('si el refresh falla, limpia la sesión', async () => {
    setAccessToken('viejo');
    vi.spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(respuesta(401))
      .mockResolvedValueOnce(respuesta(401)); // refresh falla
    await api.GET('/api/perfil');
    expect(getAccessToken()).toBeNull();
  });

  it('no intenta refrescar en rutas /api/auth/*', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(respuesta(401));
    await api.POST('/api/auth/login', { body: { email: 'x', password: 'y' } });
    expect(fetchMock).toHaveBeenCalledTimes(1); // sin reintento
  });
});

describe('desempaquetar / HttpError', () => {
  it('lanza HttpError con status y code de ProblemDetails ante un 409', () => {
    const r = {
      error: { title: 'Kyc.YaVerificado' },
      response: new Response(null, { status: 409 }),
    };
    try {
      desempaquetar(r as never);
      expect.unreachable();
    } catch (e) {
      expect(e).toBeInstanceOf(HttpError);
      expect((e as HttpError).status).toBe(409);
      expect((e as HttpError).code).toBe('Kyc.YaVerificado');
    }
  });

  it('inyecta Authorization: Bearer desde la sesión', async () => {
    setAccessToken('tok');
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(respuesta(200, {}));
    await api.GET('/api/perfil');
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.headers.get('Authorization')).toBe('Bearer tok');
  });
});
```

- [ ] **Step 2: Correr los tests (fallan)**

Run (desde `web/`): `npm run test -- src/api/http.test.ts`
Expected: FAIL — `./http` no existe.

- [ ] **Step 3: Implementar `http.ts`**

Crear `web/src/api/http.ts`. **Usar los path keys reales** de `schema.d.ts` si difieren (p. ej. `/api/auth/refresh`):

```typescript
// Cliente HTTP tipado generado del OpenAPI de la Web API. openapi-fetch resuelve URL,
// query y serialización a partir de los tipos de `schema.d.ts`; este módulo aporta el
// transporte transversal: inyección del Bearer, refresh-on-401 con reintento único y
// el mapeo de errores a HttpError sin PII.
import createClient from 'openapi-fetch';
import type { paths } from './schema';
import { clearAccessToken, getAccessToken, setAccessToken } from '../auth/session';

// Error tipado para ramificar por status (p. ej. 409). Solo status + code no-PII de ProblemDetails.
export class HttpError extends Error {
  readonly status: number;
  readonly code: string | null;

  constructor(status: number, code: string | null, mensaje: string) {
    super(mensaje);
    this.name = 'HttpError';
    this.status = status;
    this.code = code;
  }
}

async function refrescarToken(): Promise<boolean> {
  const r = await fetch('/api/auth/refresh', { method: 'POST', credentials: 'include' });
  if (!r.ok) return false;
  try {
    const data = (await r.json()) as { accessToken: string };
    setAccessToken(data.accessToken);
    return true;
  } catch {
    return false;
  }
}

// fetch custom: añade Bearer + credenciales, y ante 401 (fuera de /api/auth/*) refresca
// una vez y reintenta la petición original. openapi-fetch delega toda su E/S aquí.
async function transporte(input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
  const ejecutar = async (reintentar: boolean): Promise<Response> => {
    const headers = new Headers(init?.headers);
    const token = getAccessToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);

    const respuesta = await fetch(input, { ...init, headers, credentials: 'include' });

    const url = typeof input === 'string' ? input : input instanceof URL ? input.pathname : input.url;
    const esRutaAuth = url.includes('/api/auth/');
    if (respuesta.status === 401 && reintentar && !esRutaAuth) {
      if (await refrescarToken()) return ejecutar(false);
      clearAccessToken();
    }
    return respuesta;
  };
  return ejecutar(true);
}

export const api = createClient<paths>({ fetch: transporte });

// Lee el código no-PII (title de ProblemDetails) de un error de openapi-fetch.
function leerCodigo(error: unknown): string | null {
  if (error && typeof error === 'object' && 'title' in error) {
    const title = (error as { title?: unknown }).title;
    return typeof title === 'string' ? title : null;
  }
  return null;
}

// Desempaqueta un resultado de openapi-fetch: devuelve data si ok; si no, lanza HttpError.
export function desempaquetar<T>(resultado: {
  data?: T;
  error?: unknown;
  response: Response;
}): T {
  if (resultado.error !== undefined || !resultado.response.ok) {
    const code = leerCodigo(resultado.error);
    throw new HttpError(
      resultado.response.status,
      code,
      `Petición fallida (${resultado.response.status})`,
    );
  }
  return resultado.data as T;
}
```

- [ ] **Step 4: Correr los tests (pasan)**

Run (desde `web/`): `npm run test -- src/api/http.test.ts`
Expected: PASS. Si el test de `/api/auth/login` falla por el path key exacto, ajustar el argumento de `api.POST` al key real de `schema.d.ts`.

- [ ] **Step 5: Typecheck**

Run (desde `web/`): `npm run typecheck`
Expected: sin errores.

- [ ] **Step 6: Commit**

```bash
git add web/src/api/http.ts web/src/api/http.test.ts
git commit -m "feat(web): transporte openapi-fetch con refresh-on-401 y HttpError sin PII"
```

---

## Task 5: Frontend — reescribir `auth.ts` y `perfil.ts` sobre el cliente tipado

**Files:**
- Modify: `web/src/api/auth.ts`
- Modify: `web/src/api/perfil.ts`

**Interfaces:**
- Consumes: `api`, `desempaquetar` de `./http`; `paths`/`components` de `./schema`; `setAccessToken`, `clearAccessToken` de `../auth/session`.
- Produces (firmas públicas sin cambios respecto de hoy):
  - `auth.ts`: `registrar(datos: RegistroDatos): Promise<void>`, `iniciarSesion(cred: Credenciales): Promise<void>`, `refrescar(): Promise<boolean>`, `cerrarSesion(): Promise<void>`.
  - `perfil.ts`: `obtenerPerfil(): Promise<Perfil>`, `actualizarPerfil(datos: { nombre: string; ciudad: string }): Promise<void>`.

- [ ] **Step 1: Reescribir `auth.ts`**

Sustituir `web/src/api/auth.ts` (mantener las interfaces `RegistroDatos`/`Credenciales` como shape del formulario; **usar los path keys reales** de `schema.d.ts`):

```typescript
import { clearAccessToken, setAccessToken } from '../auth/session';
import { api, desempaquetar } from './http';

export interface RegistroDatos {
  email: string;
  password: string;
  nombre: string;
  ciudad: string;
}

export interface Credenciales {
  email: string;
  password: string;
}

export async function registrar(datos: RegistroDatos): Promise<void> {
  desempaquetar(await api.POST('/api/auth/register', { body: datos }));
}

export async function iniciarSesion(cred: Credenciales): Promise<void> {
  const data = desempaquetar(await api.POST('/api/auth/login', { body: cred }));
  setAccessToken((data as { accessToken: string }).accessToken);
}

export async function refrescar(): Promise<boolean> {
  const r = await api.POST('/api/auth/refresh');
  if (r.error !== undefined || !r.response.ok) return false;
  setAccessToken((r.data as { accessToken: string }).accessToken);
  return true;
}

export async function cerrarSesion(): Promise<void> {
  try {
    await api.POST('/api/auth/logout');
  } finally {
    clearAccessToken();
  }
}
```

- [ ] **Step 2: Reescribir `perfil.ts`**

Sustituir `web/src/api/perfil.ts` (derivar `Perfil` del esquema generado; el nombre del schema puede variar, verificar en `schema.d.ts`):

```typescript
import type { components } from './schema';
import { api, desempaquetar } from './http';

export type Perfil = components['schemas']['PerfilDto'];

export async function obtenerPerfil(): Promise<Perfil> {
  return desempaquetar(await api.GET('/api/perfil'));
}

export async function actualizarPerfil(datos: { nombre: string; ciudad: string }): Promise<void> {
  desempaquetar(
    await api.PUT('/api/perfil', { body: { nombre: datos.nombre, ciudad: datos.ciudad } }),
  );
}
```

Si el esquema del perfil expone `id/email/nombre/ciudad`, `Perfil` mantiene compatibilidad con los consumidores actuales. Verificar el nombre real del schema en `schema.d.ts` y ajustar el índice de `components['schemas'][...]`.

- [ ] **Step 3: Typecheck y localizar rupturas de consumidores**

Run (desde `web/`): `npm run typecheck`
Expected: si algún componente consumía un campo con otro nombre, TypeScript lo marca. Ajustar el componente al tipo generado (mínimo). Anotar cualquier ajuste fuera de `src/api/`.

- [ ] **Step 4: Suite completa**

Run (desde `web/`): `npm run test`
Expected: verde (los tests existentes de auth/perfil, si mockeaban el módulo, siguen mockeando estas mismas funciones públicas).

- [ ] **Step 5: Lint**

Run (desde `web/`): `npm run lint`
Expected: sin errores.

- [ ] **Step 6: Commit**

```bash
git add web/src/api/auth.ts web/src/api/perfil.ts
git commit -m "feat(web): auth y perfil sobre el cliente OpenAPI tipado"
```

---

## Task 6: Frontend — reescribir `kyc.ts` (multipart, blobs, admin) y sus tests

**Files:**
- Modify: `web/src/api/kyc.ts`
- Modify: `web/src/api/kyc.test.ts`

**Interfaces:**
- Consumes: `api`, `desempaquetar`, `HttpError` de `./http`; `components` de `./schema`.
- Produces (firmas públicas sin cambios):
  - `type EstadoKyc = 'NoIniciado' | 'Pendiente' | 'Aprobada' | 'Rechazada'`
  - `obtenerEstadoKyc(): Promise<EstadoKycDto>`
  - `enviarKyc(documento: File, selfie: File): Promise<void>`
  - `listarSolicitudesKyc(estado?, pagina?, tamano?): Promise<PaginaSolicitudes>`
  - `obtenerImagenKyc(solicitudId: string, tipo: 'documento' | 'selfie'): Promise<string>`
  - `aprobarKyc(solicitudId: string): Promise<void>`
  - `rechazarKyc(solicitudId: string, motivo: string): Promise<void>`

- [ ] **Step 1: Reescribir `kyc.ts`**

Sustituir `web/src/api/kyc.ts` (multipart con `FormData` + `bodySerializer`; blob con `parseAs: 'blob'`; **path keys reales** de `schema.d.ts`):

```typescript
import type { components } from './schema';
import { api, desempaquetar, HttpError } from './http';

// El estado se expone como string en el contrato; el union preserva el uso en UI.
export type EstadoKyc = 'NoIniciado' | 'Pendiente' | 'Aprobada' | 'Rechazada';

export type EstadoKycDto = components['schemas']['EstadoKycDto'];
export type SolicitudKycResumen = components['schemas']['SolicitudKycResumenDto'];
export type PaginaSolicitudes = components['schemas']['ResultadoPaginadoDeSolicitudKycResumenDto'];

export async function obtenerEstadoKyc(): Promise<EstadoKycDto> {
  return desempaquetar(await api.GET('/api/kyc/estado'));
}

export async function enviarKyc(documento: File, selfie: File): Promise<void> {
  const form = new FormData();
  form.append('documento', documento);
  form.append('selfie', selfie);
  const r = await api.POST('/api/kyc', {
    body: form as unknown as never,
    bodySerializer: (b: unknown) => b as FormData,
  });
  if (r.error !== undefined || !r.response.ok) {
    const code =
      r.error && typeof r.error === 'object' && 'title' in r.error
        ? ((r.error as { title?: string }).title ?? null)
        : null;
    throw new HttpError(r.response.status, code, `Petición fallida (${r.response.status})`);
  }
}

export function listarSolicitudesKyc(
  estado?: EstadoKyc,
  pagina = 1,
  tamano = 20,
): Promise<PaginaSolicitudes> {
  return api
    .GET('/api/admin/kyc', { params: { query: { estado, pagina, tamano } } })
    .then((r) => desempaquetar(r));
}

export async function obtenerImagenKyc(
  solicitudId: string,
  tipo: 'documento' | 'selfie',
): Promise<string> {
  const ruta = tipo === 'documento'
    ? '/api/admin/kyc/{solicitudId}/documento'
    : '/api/admin/kyc/{solicitudId}/selfie';
  const r = await api.GET(ruta, {
    params: { path: { solicitudId } },
    parseAs: 'blob',
  });
  if (r.error !== undefined || !r.response.ok) {
    throw new HttpError(r.response.status, null, `Petición fallida (${r.response.status})`);
  }
  return URL.createObjectURL(r.data as Blob);
}

export async function aprobarKyc(solicitudId: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/kyc/{solicitudId}/aprobar', {
      params: { path: { solicitudId } },
    }),
  );
}

export async function rechazarKyc(solicitudId: string, motivo: string): Promise<void> {
  desempaquetar(
    await api.POST('/api/admin/kyc/{solicitudId}/rechazar', {
      params: { path: { solicitudId } },
      body: { motivo },
    }),
  );
}
```

Verificar en `schema.d.ts` el nombre real del schema paginado (openapi-typescript genera nombres del estilo `ResultadoPaginadoDeSolicitudKycResumenDto` o similar) y ajustar el índice de `components['schemas'][...]` y los path keys (barra final incluida).

- [ ] **Step 2: Reescribir `kyc.test.ts`**

Sustituir `web/src/api/kyc.test.ts` para mockear `fetch` (no el módulo `client`, que desaparece):

```typescript
import { describe, it, expect, vi, afterEach } from 'vitest';
import { enviarKyc, listarSolicitudesKyc, obtenerImagenKyc, rechazarKyc } from './kyc';
import { clearAccessToken } from '../auth/session';

afterEach(() => {
  vi.restoreAllMocks();
  clearAccessToken();
});

describe('api/kyc', () => {
  it('enviarKyc hace POST multipart a /api/kyc con documento y selfie', async () => {
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    const doc = new File(['a'], 'doc.png', { type: 'image/png' });
    const selfie = new File(['b'], 'selfie.jpg', { type: 'image/jpeg' });
    await enviarKyc(doc, selfie);
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.url).toContain('/api/kyc');
    // El body es FormData: no debe fijarse Content-Type manual (lo pone el navegador con boundary).
  });

  it('listarSolicitudesKyc arma la query con filtro y paginación', async () => {
    const fetchMock = vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(JSON.stringify({ items: [], pagina: 2, tamano: 10, total: 0 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );
    await listarSolicitudesKyc('Pendiente', 2, 10);
    const req = fetchMock.mock.calls[0][0] as Request;
    expect(req.url).toContain('estado=Pendiente');
    expect(req.url).toContain('pagina=2');
    expect(req.url).toContain('tamano=10');
  });

  it('obtenerImagenKyc convierte el Blob en objectURL', async () => {
    vi.spyOn(globalThis, 'fetch').mockResolvedValueOnce(
      new Response(new Blob(['x'], { type: 'image/png' }), {
        status: 200,
        headers: { 'Content-Type': 'image/png' },
      }),
    );
    const crear = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:fake');
    const url = await obtenerImagenKyc('abc', 'selfie');
    expect(crear).toHaveBeenCalled();
    expect(url).toBe('blob:fake');
  });

  it('rechazarKyc envía el motivo en el cuerpo JSON', async () => {
    const fetchMock = vi
      .spyOn(globalThis, 'fetch')
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    await rechazarKyc('abc', 'documento ilegible');
    const req = fetchMock.mock.calls[0][0] as Request;
    const body = await req.text();
    expect(body).toContain('documento ilegible');
  });
});
```

- [ ] **Step 3: Correr los tests de kyc**

Run (desde `web/`): `npm run test -- src/api/kyc.test.ts`
Expected: PASS. Ajustar path keys / nombres de schema si algún assert falla por el key exacto.

- [ ] **Step 4: Typecheck y lint**

Run (desde `web/`): `npm run typecheck`
Run (desde `web/`): `npm run lint`
Expected: sin errores.

- [ ] **Step 5: Commit**

```bash
git add web/src/api/kyc.ts web/src/api/kyc.test.ts
git commit -m "feat(web): kyc (multipart, blobs, admin) sobre el cliente OpenAPI tipado"
```

---

## Task 7: Frontend — borrar `client.ts` y cerrar la migración

**Files:**
- Delete: `web/src/api/client.ts`
- Delete: `web/src/api/client.test.ts`

**Interfaces:**
- Consumes: nada nuevo. Verifica que ningún módulo importa ya `./client`.

- [ ] **Step 1: Confirmar que no quedan referencias a `client`**

Run (desde `web/`): `grep -rn "from './client'\|from '../api/client'\|api/client" src/`
Expected: **sin resultados** (todas las importaciones ya apuntan a `./http`). Si aparece alguna, migrarla a `./http` antes de borrar.

- [ ] **Step 2: Borrar los archivos**

```bash
git rm web/src/api/client.ts web/src/api/client.test.ts
```

- [ ] **Step 3: Suite completa + build**

Run (desde `web/`): `npm run test`
Run (desde `web/`): `npm run typecheck`
Run (desde `web/`): `npm run lint`
Run (desde `web/`): `npm run build`
Expected: los cuatro verdes.

- [ ] **Step 4: Commit**

```bash
git add -A web/src/api/
git commit -m "refactor(web): eliminar la capa de API a mano (client.ts)"
```

---

## Task 8: CI — job de detección de deriva de contrato

**Files:**
- Modify: `.github/workflows/ci.yml`

**Interfaces:**
- Consumes: script `generate:api` (Task 3), emisión en build (Task 1).
- Produces: job `contract` que falla si el backend y `schema.d.ts` divergen del commit.

- [ ] **Step 1: Añadir el job `contract`**

En `.github/workflows/ci.yml`, agregar al final de `jobs:` (mismo nivel que `build` y `frontend`, sin `defaults.working-directory`):

```yaml
  contract:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          global-json-file: CaseritoApp/global.json

      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: npm
          cache-dependency-path: web/package-lock.json

      - name: Emitir OpenAPI (build del host)
        run: dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj
        working-directory: CaseritoApp

      - name: Instalar deps web
        run: npm ci
        working-directory: web

      - name: Regenerar tipos del cliente
        run: npm run generate:api
        working-directory: web

      - name: Verificar que no hay deriva de contrato
        run: git diff --exit-code -- CaseritoApp/artifacts/openapi/CaseritoApp.Host.json web/src/api/schema.d.ts
```

(Usar el nombre de archivo verificado en Task 1.)

- [ ] **Step 2: Validar el YAML localmente**

Run (desde la raíz): `git diff --exit-code -- CaseritoApp/artifacts/openapi/CaseritoApp.Host.json web/src/api/schema.d.ts`
Expected: exit 0 (el árbol de trabajo coincide con lo committeado; confirma que el comando de deriva pasa en verde con el estado actual).

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: job contract que detecta deriva backend↔schema.d.ts del cliente"
```

---

## Self-Review (cobertura del spec)

- **Exponer OpenAPI + Bearer scheme** → Task 1. ✔
- **Emisión en build sin levantar servidor** → Task 1 (Steps 2, 5). ✔
- **Anotación por endpoint (opción A, sin reescribir handlers)** → Task 2. ✔
- **DTOs con nombre estable** → ya existen como records; `.Produces<T>()` apunta a ellos (Task 2). ✔
- **openapi-typescript + schema.d.ts committeado** → Task 3. ✔
- **openapi-fetch con middleware/transporte: refresh-on-401, HttpError sin PII** → Task 4. ✔
- **Multipart + blobs preservados** → Task 6. ✔
- **Reescritura de auth/perfil/kyc + eliminación de interfaces a mano** → Tasks 5-6. ✔
- **Borrar client.ts** → Task 7. ✔
- **CI: check de deriva** → Task 8. ✔
- **Verde continuo por commit** → cada task cierra con suite + commit. ✔

**Notas de decisión (dependientes de la generación, resueltas en ejecución):**
1. **Nombre del JSON emitido** — verificado en Task 1 Step 5; propagado a Tasks 3 y 8.
2. **Path keys exactos** (barra final por `Map*("/")`) — leídos de `schema.d.ts` en Task 3 Step 3; usados literalmente en Tasks 4-6.
3. **Nombres de schemas generados** (p. ej. el paginado genérico) — verificados en `schema.d.ts` y ajustados en Tasks 5-6.
4. **`EstadoKyc`** se conserva como union en el frontend (el contrato lo expone como `string`).
