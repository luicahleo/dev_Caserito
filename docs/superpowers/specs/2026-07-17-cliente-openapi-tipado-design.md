# Diseño — Cliente OpenAPI tipado (reemplazo de la capa de API a mano)

- **Fecha:** 2026-07-17
- **Estado:** aprobado (brainstorming)
- **Contexto:** Fase 1 completa; "próximo trabajo sugerido" #1 del roadmap. Diferido
  desde el frontend inicial.

## Objetivo

Reemplazar la capa de API escrita a mano en `web/src/api/` por un cliente TypeScript
tipado generado del documento OpenAPI de la Web API .NET, para **eliminar la deriva de
contratos backend↔frontend** y garantizarla por CI en vez de por disciplina.

## Estado actual (punto de partida)

- **Backend host (`CaseritoApp/src/Host/CaseritoApp.Host`)**: minimal APIs. `Program.cs`
  **no** expone OpenAPI (no hay `AddOpenApi`/Swagger). Los handlers devuelven
  `IResult`/`Task<IResult>` **sin metadata tipada** (`.Produces<T>()`, `TypedResults`,
  uniones `Results<...>`). Sin metadata, el generador de ASP.NET Core produce rutas y
  parámetros pero deja los cuerpos de respuesta vacíos.
- **Endpoints existentes** (acotados):
  - `auth` (`AuthEndpoints.cs`): `POST /api/auth/register|login|refresh|logout`.
  - `perfil` (`PerfilEndpoints.cs`): `GET|PUT /api/perfil`.
  - `kyc usuario` (`KycEndpoints.cs`): `POST /api/kyc` (multipart), `GET /api/kyc/estado`.
  - `kyc admin` (`KycEndpoints.cs`): `GET /api/admin/kyc`, `GET .../{id}/documento|selfie`
    (blobs), `POST .../{id}/aprobar|rechazar`.
- **Frontend (`web/`)**: `client.ts` es puro transporte (refresh-on-401 con reintento
  único, `HttpError` con `code` de ProblemDetails sin PII, `postForm` multipart,
  `getBlob`). Capas finas `auth.ts`, `perfil.ts`, `kyc.ts` con interfaces TS a mano.
  Ya hay **zod v4**, **TanStack Query**, ESLint/Prettier, Vitest.
- **CI (`.github/workflows/ci.yml`)**: job `build` (.NET) + job `frontend` (Node:
  lint, typecheck, test, build). Sin job de contrato.

## Decisiones del brainstorming

1. **Nivel de anotación backend → Opción A:** añadir metadata OpenAPI vía
   `.Produces<T>()`/`.Accepts<T>()`/`.ProducesValidationProblem()`/`.ProducesProblem()`
   encadenada en cada `Map*`, **sin reescribir los cuerpos de los handlers**. (Se
   descartó la opción B, refactor a `TypedResults`, por churn/riesgo sobre código ya
   endurecido; y la C, esquema mínimo + tipos a mano, por no cumplir el objetivo.)
2. **Generador → `openapi-typescript` (solo tipos, cero runtime) + `openapi-fetch`**
   (cliente ~6 kB con middleware). Se descartó Orval (genera demasiado código e impone
   hooks/fetcher) y openapi-generator (requiere JVM, clases verbosas).
3. **Alcance → big-bang** de los 4 grupos en una rama `feat/*`, con commits que dejan
   la suite verde en cada paso. Se borra `client.ts` al final.
4. **Generación → OpenAPI emitido en el build del backend** (sin levantar servidor) +
   `schema.d.ts` **committeado** + **check de deriva en CI** (`git diff --exit-code`).

## Arquitectura de la solución

### 1. Backend: exponer OpenAPI

- **Paquetes** (versiones solo en `CaseritoApp/Directory.Packages.props`, CPM):
  `Microsoft.AspNetCore.OpenApi` y `Microsoft.Extensions.ApiDescription.Server`.
- **`Program.cs`**:
  - `builder.Services.AddOpenApi(...)` con un **document transformer** que registra el
    **security scheme Bearer JWT** y lo aplica a los endpoints `[Authorize]`.
  - `app.MapOpenApi()` (disponible en dev; el contrato de generación sale del build).
- **Emisión en build:** `Microsoft.Extensions.ApiDescription.Server` escribe
  `openapi.json` durante `dotnet build` del host, **sin abrir puertos ni BD**. Ruta
  versionada del contrato: `CaseritoApp/artifacts/openapi/v1.json` (committeado).
  Se fija `OpenApiGenerateDocumentsOnBuild=true` y el nombre/ruta del documento en el
  `.csproj` del host.
- **Anotación por endpoint** (encadenada en los `Map*`, sin tocar handlers):
  - `auth`: register `.Accepts<RegistroRequest>().Produces(200).ProducesValidationProblem()`;
    login `.Accepts<LoginRequest>().Produces<TokenAccesoResponse>(200).Produces(401)`;
    refresh `.Produces<TokenAccesoResponse>(200).Produces(401)`; logout `.Produces(204)`.
  - `perfil`: GET `.Produces<PerfilDto>(200)`; PUT `.Accepts<ActualizarPerfilRequest>()
    .Produces(204).ProducesValidationProblem()`.
  - `kyc usuario`: envío `.Accepts<...>("multipart/form-data").Produces(204)
    .ProducesProblem(409).ProducesValidationProblem()`; estado `.Produces<EstadoKycDto>(200)`.
  - `kyc admin`: listar `.Produces<PaginaSolicitudes>(200)`; blobs
    `.Produces(200,"application/octet-stream").ProducesProblem(404)`; aprobar/rechazar
    `.Produces(204).ProducesProblem(404).ProducesProblem(409).ProducesValidationProblem()`.
- **DTOs con nombre estable:** los cuerpos que hoy salen como objetos anónimos o vía
  `resultado.Valor` (perfil, `EstadoKycDto`, `PaginaSolicitudes`) se exponen con un tipo
  nombrado para que el esquema tenga nombre estable y `openapi-typescript` no genere
  tipos inline. Cambio mínimo, sin tocar lógica.

### 2. Frontend: cliente generado

- **Devdeps nuevas** (`web/package.json`): `openapi-typescript`, `openapi-fetch`.
- **`web/src/api/schema.d.ts`**: generado, committeado (cero runtime).
- **`web/src/api/http.ts`** (reemplaza `client.ts`): construye el cliente `openapi-fetch`
  con `baseUrl` relativo y registra **middleware** que reubica el transporte actual:
  - `onRequest`: inyecta `Authorization: Bearer` desde `../auth/session`.
  - `onResponse`: en `401` fuera de `/api/auth/*` → refresh único + reintento de la
    petición original; mapea respuestas no-ok a `HttpError` (status + `code` de
    ProblemDetails, **sin PII**). Se mantiene la clase `HttpError` exportada.
- **Casos especiales preservados** con helpers finos sobre el mismo cliente:
  - **multipart** (`enviarKyc`): `body: FormData` con `bodySerializer` que devuelve el
    `FormData` tal cual.
  - **blobs** (`obtenerImagenKyc`): `parseAs: "blob"` + `URL.createObjectURL`.
- **`auth.ts`, `perfil.ts`, `kyc.ts`**: reescritos sobre rutas tipadas del cliente
  (`GET("/api/perfil")`, etc.). Se **eliminan las interfaces TS a mano** duplicadas; los
  tipos derivan de `schema.d.ts` (`components["schemas"][...]`). TanStack Query se sigue
  usando **a mano** sobre estas funciones (sin hooks auto-generados).
- Se **borra `client.ts`**; se migran `client.test.ts`/`kyc.test.ts` al nuevo módulo.

### 3. Generación y CI

- **Script npm** (`web/package.json`):
  `"generate:api": "openapi-typescript ../CaseritoApp/artifacts/openapi/v1.json -o src/api/schema.d.ts"`.
- Artefactos **committeados**: `v1.json` (contrato) y `schema.d.ts` (tipos).
  `vite build` **no** regenera (usa el `.d.ts` versionado).
- **CI — nuevo job `contract`** (setup .NET + Node):
  1. `dotnet build` del host → emite `v1.json`.
  2. `npm run generate:api` → regenera `schema.d.ts`.
  3. `git diff --exit-code -- CaseritoApp/artifacts/openapi/v1.json web/src/api/schema.d.ts`.
  - Diff ⇒ **falla = deriva detectada**. Los jobs `build`/`frontend` quedan sin cambios.

## Flujo verde continuo

Rama `feat/cliente-openapi-tipado`. Orden sugerido (cada paso deja verde
`dotnet test` y `npm run test|typecheck|lint`):

1. Backend: paquetes + `AddOpenApi` + security scheme + emisión en build + `v1.json`.
2. Backend: anotación por endpoint + DTOs nombrados; regenerar `v1.json`.
3. Frontend: devdeps + `generate:api` + `schema.d.ts` committeado.
4. Frontend: `http.ts` (middleware refresh/HttpError) con tests (TDD) — multipart y
   401-refresh cubiertos **antes** de borrar `client.ts`.
5. Frontend: reescribir `auth.ts`/`perfil.ts`/`kyc.ts` sobre tipos; migrar tests.
6. Borrar `client.ts`; CI: job `contract`.

## Alcance

**Incluido:** exponer OpenAPI del host, anotar los 4 grupos, generar tipos, cliente
`openapi-fetch` con middleware, reescritura de las 4 capas, check de deriva en CI.

**Fuera de alcance (YAGNI):** hooks auto-generados de TanStack Query; esquemas zod
runtime desde OpenAPI; versionado de API (`/v2`); refactor de handlers a `TypedResults`
(opción B descartada); cliente contra el server en ejecución.

## Riesgos / atención

- **Emisor `ApiDescription.Server`**: puede requerir `OpenApiGenerateDocumentsOnBuild=true`
  y fijar nombre/ruta del documento. Validar temprano (paso 1).
- **Multipart y 401-refresh**: los dos puntos donde `openapi-fetch` difiere del `fetch`
  a mano. Cubrir con tests antes de borrar `client.ts`.
- **Nombres de esquema**: DTOs anónimos ⇒ tipos inline feos en `schema.d.ts`. Mitigado
  nombrando los DTOs de respuesta.
- **Anti-PII (no negociable)**: el mapeo de errores en `http.ts` solo expone
  status/ruta/`code` de ProblemDetails, nunca contenido sensible (igual que hoy).

## Criterios de éxito

- `dotnet build` emite `v1.json` determinista sin levantar el servidor.
- `web/src/api/` no contiene interfaces de request/response a mano; todo deriva de
  `schema.d.ts`.
- `client.ts` eliminado; suite web y .NET verdes.
- Job `contract` en CI falla si backend y `schema.d.ts` divergen.
