# UI web de catálogo (2E) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Que el marketplace se use de punta a punta desde el navegador: explorar/ver avisos sin login y publicar/gestionar avisos autenticado.

**Architecture:** SPA React (Vite) que consume la API de catálogo (2A dueño + 2B público) vía el cliente `openapi-fetch` tipado. Capa de API en `web/src/api/` (wrappers finos), páginas en `web/src/routes/`, layout compartido con `AppBar`, estado de servidor con TanStack Query. Un ajuste backend mínimo abre `/api/catalogo/*` a anónimo para poblar los filtros públicos.

**Tech Stack:** React 18 + TypeScript estricto, MUI, React Router (`createBrowserRouter`), TanStack Query, `openapi-fetch`; tests con Vitest + React Testing Library. Backend: ASP.NET Core Minimal API (`Host`), tests de integración con Testcontainers.MsSql.

## Global Constraints

- Textos de UI y comentarios en **español**; ortografía con acentos correcta.
- TypeScript estricto, ESLint + Prettier; nada de `any` implícito.
- Nunca exponer PII en logs del cliente (no volcar tokens ni cuerpos de error a consola). Los mensajes al usuario son genéricos, derivados de `HttpError` (solo status + code).
- La capa de API son wrappers sobre `api` (openapi-fetch) + `desempaquetar` de `web/src/api/http.ts`; los tipos se re-exportan del schema generado (`web/src/api/schema.d.ts`). Nunca tipar a mano lo que el schema ya define.
- `CrearAvisoRequest` / `EditarAvisoRequest` **no** llevan `moneda` (el backend asume BOB). El monto viaja como `number`.
- Frontend: comandos desde `web/`. Backend: comandos desde `CaseritoApp/`.
- Commits en la rama `feat/catalogo-2e-ui-web` (ya creada).

---

### Task 1: Backend — abrir `/api/catalogo/*` a anónimo

Los DTOs públicos de 2B resuelven nombres pero el listado público filtra por `categoriaId`/`ciudadId`; los dropdowns de filtro anónimos necesitan las listas de catálogo, hoy `[Authorize]`. Se abren a `AllowAnonymous` (catálogo sembrado, sin PII).

**Files:**
- Modify: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/CatalogoEndpoints.cs`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/AvisosFlujoTests.cs`
- Modify (docs): `CLAUDE.md`

**Interfaces:**
- Consumes: nada nuevo.
- Produces: `GET /api/catalogo/categorias` y `GET /api/catalogo/ciudades` responden `200` **sin** Bearer.

- [ ] **Step 1: Escribir el test de integración que falla (acceso anónimo)**

En `AvisosFlujoTests.cs`, añadir un test que pide las categorías **sin token**. Usar el `HttpClient` de la factory (`cliente`) directamente con `GetAsync` (sin el helper `Con(...)` que agrega el token):

```csharp
[Fact]
public async Task Catalogo_de_referencia_es_accesible_anonimo()
{
    var cliente = _factory.CreateClient();

    var respCats = await cliente.GetAsync("/api/catalogo/categorias");
    Assert.Equal(HttpStatusCode.OK, respCats.StatusCode);
    var categorias = await respCats.Content.ReadFromJsonAsync<CategoriaRef[]>();
    Assert.Contains(categorias!, c => c.Id == _categoria);

    var respCiudades = await cliente.GetAsync("/api/catalogo/ciudades");
    Assert.Equal(HttpStatusCode.OK, respCiudades.StatusCode);
    var ciudades = await respCiudades.Content.ReadFromJsonAsync<CiudadRef[]>();
    Assert.Contains(ciudades!, c => c.Id == _ciudad);
}
```

> Verificar al inicio del archivo que existan los `using System.Net;` (para `HttpStatusCode`) y `System.Net.Http.Json;` (para `ReadFromJsonAsync`), y que `_factory`, `_categoria`, `_ciudad`, `CategoriaRef`, `CiudadRef` ya existan en la clase (se usan en los tests vecinos). Si el nombre del campo factory difiere, usar el mismo que emplean los demás tests del archivo.

- [ ] **Step 2: Correr el test para verlo fallar**

Run: `dotnet test CaseritoApp/CaseritoApp.sln --filter "FullyQualifiedName~Catalogo_de_referencia_es_accesible_anonimo"`
Expected: FAIL — la respuesta es `401 Unauthorized` en vez de `200 OK`.

- [ ] **Step 3: Abrir el grupo de endpoints a anónimo**

En `CatalogoEndpoints.cs`, quitar `.RequireAuthorization()` del grupo y actualizar el doc-comment y los `.Produces(401)`:

```csharp
/// <summary>Endpoints de catálogos de referencia bajo <c>/api/catalogo</c> (anónimos: alimentan filtros públicos).</summary>
public static class CatalogoEndpoints
{
    /// <summary>Mapea los endpoints de referencia (anónimos; catálogo sembrado, sin PII).</summary>
    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/catalogo");

        grupo.MapGet("/categorias", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListarCategoriasQuery(), ct)))
            .Produces<IReadOnlyList<CategoriaDto>>(StatusCodes.Status200OK);

        grupo.MapGet("/ciudades", async (ISender sender, CancellationToken ct) =>
                Results.Ok(await sender.Send(new ListarCiudadesQuery(), ct)))
            .Produces<IReadOnlyList<CiudadDto>>(StatusCodes.Status200OK);

        return app;
    }
}
```

- [ ] **Step 4: Correr los tests de catálogo para verlos pasar**

Run: `dotnet test CaseritoApp/CaseritoApp.sln --filter "FullyQualifiedName~Catalogo"`
Expected: PASS (el nuevo test anónimo y el existente con token siguen verdes).

- [ ] **Step 5: Actualizar CLAUDE.md**

En la sección **Auth → Endpoints y Autorización** de `CLAUDE.md`, donde documente `/api/catalogo/*`, cambiar la nota de `[Authorize]` a anónimo. Buscar la referencia (p. ej. "GET /api/catalogo/categorias ... [Authorize]") y dejar claro que ahora son `AllowAnonymous` porque alimentan los filtros públicos de descubrimiento. Si no hubiera una línea específica, no inventar una sección: basta con que quede coherente con el cambio.

- [ ] **Step 6: Commit**

```bash
git add CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/CatalogoEndpoints.cs CaseritoApp/tests/CaseritoApp.IntegrationTests/AvisosFlujoTests.cs CLAUDE.md
git commit -m "feat(catalog): abre /api/catalogo/* a anonimo para filtros publicos"
```

---

### Task 2: Capa de API — `catalogo.ts` y `avisos.ts`

**Files:**
- Create: `web/src/api/catalogo.ts`
- Create: `web/src/api/avisos.ts`
- Test: `web/src/api/avisos.test.ts`

**Interfaces:**
- Consumes: `api`, `desempaquetar` de `web/src/api/http.ts`; `components`/`paths` de `web/src/api/schema.d.ts`.
- Produces:
  - `catalogo.ts`: `type Categoria`, `type Ciudad`; `listarCategorias(): Promise<Categoria[]>`, `listarCiudades(): Promise<Ciudad[]>`.
  - `avisos.ts`: tipos `AvisoPublicoResumen`, `AvisoPublico`, `AvisoResumen`, `Aviso`, `CrearAvisoRequest`, `EditarAvisoRequest`, `PaginaAvisosPublicos`, `PaginaMisAvisos`, `FiltroBusqueda`; funciones `buscarAvisos(filtro: FiltroBusqueda, pagina: number, tamano: number): Promise<PaginaAvisosPublicos>`, `obtenerAvisoPublico(id: string): Promise<AvisoPublico>`, `listarMisAvisos(pagina: number, tamano: number): Promise<PaginaMisAvisos>`, `obtenerMiAviso(id: string): Promise<Aviso>`, `crearAviso(req: CrearAvisoRequest): Promise<{ id: string }>`, `editarAviso(id: string, req: EditarAvisoRequest): Promise<void>`, `pausarAviso(id: string): Promise<void>`, `reactivarAviso(id: string): Promise<void>`, `eliminarAviso(id: string): Promise<void>`.

- [ ] **Step 1: Crear `catalogo.ts`**

```ts
import type { components } from './schema';
import { api, desempaquetar } from './http';

export type Categoria = components['schemas']['CategoriaDto'];
export type Ciudad = components['schemas']['CiudadDto'];

export async function listarCategorias(): Promise<Categoria[]> {
  return desempaquetar(await api.GET('/api/catalogo/categorias'));
}

export async function listarCiudades(): Promise<Ciudad[]> {
  return desempaquetar(await api.GET('/api/catalogo/ciudades'));
}
```

- [ ] **Step 2: Crear `avisos.ts`**

```ts
import type { components } from './schema';
import { api, desempaquetar } from './http';

export type AvisoPublicoResumen = components['schemas']['AvisoPublicoResumenDto'];
export type AvisoPublico = components['schemas']['AvisoPublicoDto'];
export type AvisoResumen = components['schemas']['AvisoResumenDto'];
export type Aviso = components['schemas']['AvisoDto'];
export type CrearAvisoRequest = components['schemas']['CrearAvisoRequest'];
export type EditarAvisoRequest = components['schemas']['EditarAvisoRequest'];
export type PaginaAvisosPublicos =
  components['schemas']['ResultadoPaginadoOfAvisoPublicoResumenDto'];
export type PaginaMisAvisos = components['schemas']['ResultadoPaginadoOfAvisoResumenDto'];

// Criterios de búsqueda pública; todos opcionales. Se pasan como query a /api/publico/avisos.
export interface FiltroBusqueda {
  q?: string;
  categoriaId?: string;
  ciudadId?: string;
  precioMin?: number;
  precioMax?: number;
  condicion?: string;
}

export async function buscarAvisos(
  filtro: FiltroBusqueda,
  pagina: number,
  tamano: number,
): Promise<PaginaAvisosPublicos> {
  return desempaquetar(
    await api.GET('/api/publico/avisos', {
      params: { query: { ...filtro, pagina, tamano } },
    }),
  );
}

export async function obtenerAvisoPublico(id: string): Promise<AvisoPublico> {
  return desempaquetar(
    await api.GET('/api/publico/avisos/{id}', { params: { path: { id } } }),
  );
}

export async function listarMisAvisos(pagina: number, tamano: number): Promise<PaginaMisAvisos> {
  return desempaquetar(
    await api.GET('/api/avisos/mios', { params: { query: { pagina, tamano } } }),
  );
}

export async function obtenerMiAviso(id: string): Promise<Aviso> {
  return desempaquetar(
    await api.GET('/api/avisos/mios/{id}', { params: { path: { id } } }),
  );
}

export async function crearAviso(req: CrearAvisoRequest): Promise<{ id: string }> {
  return desempaquetar(await api.POST('/api/avisos', { body: req }));
}

export async function editarAviso(id: string, req: EditarAvisoRequest): Promise<void> {
  desempaquetar(await api.PUT('/api/avisos/{id}', { params: { path: { id } }, body: req }));
}

export async function pausarAviso(id: string): Promise<void> {
  desempaquetar(await api.POST('/api/avisos/{id}/pausar', { params: { path: { id } } }));
}

export async function reactivarAviso(id: string): Promise<void> {
  desempaquetar(await api.POST('/api/avisos/{id}/reactivar', { params: { path: { id } } }));
}

export async function eliminarAviso(id: string): Promise<void> {
  desempaquetar(await api.DELETE('/api/avisos/{id}', { params: { path: { id } } }));
}
```

> Si `openapi-fetch` marca error de tipos en `query` por parámetros opcionales `undefined`, no forzar `any`: filtrar las claves indefinidas antes de pasarlas, o confirmar que el tipo generado del schema permite `undefined` (lo normal para query opcional). Ajustar sin romper el tipado estricto.

- [ ] **Step 3: Escribir el test de la capa (paso de parámetros)**

`web/src/api/avisos.test.ts`. Se mockea el cliente `api` para verificar que cada función arma la petición correcta. Seguir el estilo de `web/src/api/kyc.test.ts` / `http.test.ts` del repo.

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { api } from './http';
import { buscarAvisos, crearAviso, eliminarAviso } from './avisos';

describe('capa de API de avisos', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('buscarAvisos pasa filtros y paginación como query', async () => {
    const spy = vi.spyOn(api, 'GET').mockResolvedValue({
      data: { items: [], pagina: 1, tamano: 20, total: 0 },
      response: new Response(null, { status: 200 }),
    } as never);

    await buscarAvisos({ q: 'silla', categoriaId: 'c1' }, 2, 20);

    expect(spy).toHaveBeenCalledWith('/api/publico/avisos', {
      params: { query: { q: 'silla', categoriaId: 'c1', pagina: 2, tamano: 20 } },
    });
  });

  it('crearAviso hace POST con el body del request', async () => {
    const spy = vi.spyOn(api, 'POST').mockResolvedValue({
      data: { id: 'nuevo' },
      response: new Response(null, { status: 201 }),
    } as never);

    const req = {
      titulo: 'Mesa',
      descripcion: 'De madera',
      monto: 300,
      condicion: 'Usado',
      categoriaId: 'c1',
      ciudadId: 'u1',
    };
    const res = await crearAviso(req);

    expect(spy).toHaveBeenCalledWith('/api/avisos', { body: req });
    expect(res).toEqual({ id: 'nuevo' });
  });

  it('eliminarAviso hace DELETE con el id en el path', async () => {
    const spy = vi.spyOn(api, 'DELETE').mockResolvedValue({
      data: undefined,
      response: new Response(null, { status: 204 }),
    } as never);

    await eliminarAviso('a1');

    expect(spy).toHaveBeenCalledWith('/api/avisos/{id}', { params: { path: { id: 'a1' } } });
  });
});
```

- [ ] **Step 4: Correr typecheck, lint y tests**

Run: `cd web && npm run typecheck && npm run lint && npm run test -- avisos`
Expected: PASS (typecheck y lint sin errores; los 3 tests verdes).

- [ ] **Step 5: Commit**

```bash
git add web/src/api/catalogo.ts web/src/api/avisos.ts web/src/api/avisos.test.ts
git commit -m "feat(web): capa de API de catalogo y avisos"
```

---

### Task 3: `ExplorarPage` — listado público, filtros y paginación

**Files:**
- Create: `web/src/routes/ExplorarPage.tsx`
- Create: `web/src/lib/formato.ts`
- Test: `web/src/routes/ExplorarPage.test.tsx`
- Test: `web/src/lib/formato.test.ts`

**Interfaces:**
- Consumes: `buscarAvisos`, `FiltroBusqueda`, `AvisoPublicoResumen` (Task 2); `listarCategorias`, `listarCiudades` (Task 2); `useSearchParams`, `Link` de react-router-dom.
- Produces: `export function ExplorarPage()`; `export function formatearBob(monto: number | string): string` en `web/src/lib/formato.ts`.

- [ ] **Step 1: Escribir el test del formateador de moneda (falla)**

`web/src/lib/formato.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { formatearBob } from './formato';

describe('formatearBob', () => {
  it('formatea un número como moneda boliviana', () => {
    expect(formatearBob(1234.5)).toContain('1.234,5');
  });

  it('acepta el monto como string (viene del contrato como number | string)', () => {
    expect(formatearBob('300')).toContain('300');
  });
});
```

- [ ] **Step 2: Correr el test para verlo fallar**

Run: `cd web && npm run test -- formato`
Expected: FAIL — `formatearBob` no existe.

- [ ] **Step 3: Implementar el formateador**

`web/src/lib/formato.ts`:

```ts
// Formatea un monto en bolivianos (BOB). El contrato expone el monto como number | string
// (double de OpenAPI), por eso se coacciona a número antes de formatear.
const formateador = new Intl.NumberFormat('es-BO', {
  style: 'currency',
  currency: 'BOB',
});

export function formatearBob(monto: number | string): string {
  return formateador.format(Number(monto));
}
```

- [ ] **Step 4: Correr el test del formateador**

Run: `cd web && npm run test -- formato`
Expected: PASS.

- [ ] **Step 5: Escribir el test de `ExplorarPage` (falla)**

`web/src/routes/ExplorarPage.test.tsx`. Se mockean las capas de API; se monta con `QueryClientProvider` (retry:false) y `MemoryRouter` (la página usa `useSearchParams` y `Link`).

```tsx
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { ExplorarPage } from './ExplorarPage';
import * as avisos from '../api/avisos';
import * as catalogo from '../api/catalogo';

afterEach(() => vi.restoreAllMocks());

const resumen: avisos.AvisoPublicoResumen = {
  id: 'a1',
  titulo: 'Silla de madera',
  monto: 150,
  moneda: 'BOB',
  nombreCategoria: 'Muebles',
  nombreCiudad: 'La Paz',
  condicion: 'Usado',
  fechaCreacion: '2026-07-18T10:00:00Z',
};

function montar() {
  vi.spyOn(catalogo, 'listarCategorias').mockResolvedValue([{ id: 'c1', nombre: 'Muebles' }]);
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([{ id: 'u1', nombre: 'La Paz' }]);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <ExplorarPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('ExplorarPage', () => {
  it('muestra los avisos devueltos por la búsqueda', async () => {
    vi.spyOn(avisos, 'buscarAvisos').mockResolvedValue({
      items: [resumen],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    montar();
    expect(await screen.findByText('Silla de madera')).toBeInTheDocument();
  });

  it('muestra el estado vacío cuando no hay resultados', async () => {
    vi.spyOn(avisos, 'buscarAvisos').mockResolvedValue({
      items: [],
      pagina: 1,
      tamano: 20,
      total: 0,
    });
    montar();
    expect(await screen.findByText(/no se encontraron avisos/i)).toBeInTheDocument();
  });

  it('al buscar por texto re-consulta con el filtro q', async () => {
    const spy = vi.spyOn(avisos, 'buscarAvisos').mockResolvedValue({
      items: [resumen],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    montar();
    await screen.findByText('Silla de madera');

    await userEvent.type(screen.getByLabelText(/buscar/i), 'silla');
    await userEvent.click(screen.getByRole('button', { name: /buscar/i }));

    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(
        expect.objectContaining({ q: 'silla' }),
        1,
        expect.any(Number),
      ),
    );
  });
});
```

- [ ] **Step 6: Correr el test para verlo fallar**

Run: `cd web && npm run test -- ExplorarPage`
Expected: FAIL — `ExplorarPage` no existe.

- [ ] **Step 7: Implementar `ExplorarPage`**

`web/src/routes/ExplorarPage.tsx`. Estado de filtros + página sincronizado a la URL con `useSearchParams`; la `queryKey` deriva de esos parámetros.

```tsx
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link as RouterLink, useSearchParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Card,
  CardActionArea,
  CardContent,
  CircularProgress,
  Container,
  Grid,
  MenuItem,
  Pagination,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { buscarAvisos, type FiltroBusqueda } from '../api/avisos';
import { listarCategorias, listarCiudades } from '../api/catalogo';
import { formatearBob } from '../lib/formato';

const TAMANO = 20;
const CONDICIONES = ['Nuevo', 'Usado'];

// Lee el filtro y la página desde los search params de la URL (fuente de verdad).
function leerFiltro(params: URLSearchParams): { filtro: FiltroBusqueda; pagina: number } {
  const num = (v: string | null) => (v ? Number(v) : undefined);
  return {
    filtro: {
      q: params.get('q') ?? undefined,
      categoriaId: params.get('categoriaId') ?? undefined,
      ciudadId: params.get('ciudadId') ?? undefined,
      precioMin: num(params.get('precioMin')),
      precioMax: num(params.get('precioMax')),
      condicion: params.get('condicion') ?? undefined,
    },
    pagina: Number(params.get('pagina') ?? '1'),
  };
}

export function ExplorarPage() {
  const [params, setParams] = useSearchParams();
  const { filtro, pagina } = leerFiltro(params);

  // Borrador editable de los campos; se vuelca a la URL al pulsar "Buscar".
  const [borrador, setBorrador] = useState(filtro);

  const categorias = useQuery({ queryKey: ['categorias'], queryFn: listarCategorias });
  const ciudades = useQuery({ queryKey: ['ciudades'], queryFn: listarCiudades });

  const { data, isLoading, isError } = useQuery({
    queryKey: ['avisos-publicos', filtro, pagina],
    queryFn: () => buscarAvisos(filtro, pagina, TAMANO),
  });

  const aplicar = () => {
    const nuevo = new URLSearchParams();
    if (borrador.q) nuevo.set('q', borrador.q);
    if (borrador.categoriaId) nuevo.set('categoriaId', borrador.categoriaId);
    if (borrador.ciudadId) nuevo.set('ciudadId', borrador.ciudadId);
    if (borrador.precioMin != null) nuevo.set('precioMin', String(borrador.precioMin));
    if (borrador.precioMax != null) nuevo.set('precioMax', String(borrador.precioMax));
    if (borrador.condicion) nuevo.set('condicion', borrador.condicion);
    nuevo.set('pagina', '1');
    setParams(nuevo);
  };

  const cambiarPagina = (p: number) => {
    const nuevo = new URLSearchParams(params);
    nuevo.set('pagina', String(p));
    setParams(nuevo);
  };

  const totalPaginas = data ? Math.max(1, Math.ceil(data.total / TAMANO)) : 1;

  return (
    <Container maxWidth="lg" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Explorar avisos
      </Typography>

      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ mb: 3 }} flexWrap="wrap">
        <TextField
          label="Buscar"
          value={borrador.q ?? ''}
          onChange={(e) => setBorrador((b) => ({ ...b, q: e.target.value }))}
          onKeyDown={(e) => e.key === 'Enter' && aplicar()}
        />
        <TextField
          select
          label="Categoría"
          value={borrador.categoriaId ?? ''}
          sx={{ minWidth: 160 }}
          onChange={(e) => setBorrador((b) => ({ ...b, categoriaId: e.target.value || undefined }))}
        >
          <MenuItem value="">Todas</MenuItem>
          {(categorias.data ?? []).map((c) => (
            <MenuItem key={c.id} value={c.id}>
              {c.nombre}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          label="Ciudad"
          value={borrador.ciudadId ?? ''}
          sx={{ minWidth: 160 }}
          onChange={(e) => setBorrador((b) => ({ ...b, ciudadId: e.target.value || undefined }))}
        >
          <MenuItem value="">Todas</MenuItem>
          {(ciudades.data ?? []).map((c) => (
            <MenuItem key={c.id} value={c.id}>
              {c.nombre}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          label="Condición"
          value={borrador.condicion ?? ''}
          sx={{ minWidth: 140 }}
          onChange={(e) => setBorrador((b) => ({ ...b, condicion: e.target.value || undefined }))}
        >
          <MenuItem value="">Cualquiera</MenuItem>
          {CONDICIONES.map((c) => (
            <MenuItem key={c} value={c}>
              {c}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          label="Precio mín."
          type="number"
          value={borrador.precioMin ?? ''}
          onChange={(e) =>
            setBorrador((b) => ({
              ...b,
              precioMin: e.target.value ? Number(e.target.value) : undefined,
            }))
          }
        />
        <TextField
          label="Precio máx."
          type="number"
          value={borrador.precioMax ?? ''}
          onChange={(e) =>
            setBorrador((b) => ({
              ...b,
              precioMax: e.target.value ? Number(e.target.value) : undefined,
            }))
          }
        />
        <Button variant="contained" onClick={aplicar}>
          Buscar
        </Button>
      </Stack>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
          <CircularProgress />
        </Box>
      ) : isError ? (
        <Alert severity="error">No se pudieron cargar los avisos. Inténtalo más tarde.</Alert>
      ) : (data?.items.length ?? 0) === 0 ? (
        <Typography color="text.secondary">No se encontraron avisos.</Typography>
      ) : (
        <>
          <Grid container spacing={2}>
            {data?.items.map((a) => (
              <Grid item xs={12} sm={6} md={4} key={a.id}>
                <Card>
                  <CardActionArea component={RouterLink} to={`/avisos/${a.id}`}>
                    {/* Placeholder de foto (2C) */}
                    <Box sx={{ height: 140, bgcolor: 'grey.200' }} aria-hidden />
                    <CardContent>
                      <Typography variant="h6" noWrap>
                        {a.titulo}
                      </Typography>
                      <Typography variant="subtitle1" color="primary">
                        {formatearBob(a.monto)}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        {a.nombreCategoria} · {a.nombreCiudad} · {a.condicion}
                      </Typography>
                    </CardContent>
                  </CardActionArea>
                </Card>
              </Grid>
            ))}
          </Grid>

          <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
            <Pagination
              count={totalPaginas}
              page={pagina}
              onChange={(_, p) => cambiarPagina(p)}
            />
          </Box>
        </>
      )}
    </Container>
  );
}
```

- [ ] **Step 8: Correr typecheck, lint y tests**

Run: `cd web && npm run typecheck && npm run lint && npm run test -- ExplorarPage formato`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add web/src/routes/ExplorarPage.tsx web/src/routes/ExplorarPage.test.tsx web/src/lib/formato.ts web/src/lib/formato.test.ts
git commit -m "feat(web): pantalla publica de explorar avisos"
```

---

### Task 4: `DetalleAvisoPage` — detalle público

**Files:**
- Create: `web/src/routes/DetalleAvisoPage.tsx`
- Test: `web/src/routes/DetalleAvisoPage.test.tsx`

**Interfaces:**
- Consumes: `obtenerAvisoPublico`, `AvisoPublico` (Task 2); `formatearBob` (Task 3); `useParams`, `Link` de react-router-dom; `HttpError` de `../api/http`.
- Produces: `export function DetalleAvisoPage()`.

- [ ] **Step 1: Escribir el test (falla)**

`web/src/routes/DetalleAvisoPage.test.tsx`. Se controla el `id` de la ruta con `MemoryRouter` + `Routes`.

```tsx
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { DetalleAvisoPage } from './DetalleAvisoPage';
import * as avisos from '../api/avisos';
import { HttpError } from '../api/http';

afterEach(() => vi.restoreAllMocks());

function montar(id: string) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/avisos/${id}`]}>
        <Routes>
          <Route path="/avisos/:id" element={<DetalleAvisoPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('DetalleAvisoPage', () => {
  it('muestra el detalle de un aviso', async () => {
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockResolvedValue({
      id: 'a1',
      titulo: 'Bicicleta',
      descripcion: 'Rodado 26, poco uso',
      monto: 800,
      moneda: 'BOB',
      nombreCategoria: 'Deportes',
      nombreCiudad: 'Cochabamba',
      condicion: 'Usado',
      fechaCreacion: '2026-07-18T10:00:00Z',
    });
    montar('a1');
    expect(await screen.findByText('Bicicleta')).toBeInTheDocument();
    expect(screen.getByText('Rodado 26, poco uso')).toBeInTheDocument();
  });

  it('muestra "no disponible" ante un 404', async () => {
    vi.spyOn(avisos, 'obtenerAvisoPublico').mockRejectedValue(
      new HttpError(404, null, 'Petición fallida (404)'),
    );
    montar('inexistente');
    expect(await screen.findByText(/no está disponible/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Correr el test para verlo fallar**

Run: `cd web && npm run test -- DetalleAvisoPage`
Expected: FAIL — `DetalleAvisoPage` no existe.

- [ ] **Step 3: Implementar `DetalleAvisoPage`**

```tsx
import { useQuery } from '@tanstack/react-query';
import { Link as RouterLink, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Stack,
  Typography,
} from '@mui/material';
import { obtenerAvisoPublico } from '../api/avisos';
import { formatearBob } from '../lib/formato';
import { HttpError } from '../api/http';

export function DetalleAvisoPage() {
  const { id = '' } = useParams();
  const { data, isLoading, error } = useQuery({
    queryKey: ['aviso-publico', id],
    queryFn: () => obtenerAvisoPublico(id),
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !data) {
    const noDisponible = error instanceof HttpError && error.status === 404;
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Alert severity={noDisponible ? 'info' : 'error'}>
          {noDisponible
            ? 'Este aviso no está disponible.'
            : 'No se pudo cargar el aviso. Inténtalo más tarde.'}
        </Alert>
        <Button component={RouterLink} to="/" sx={{ mt: 2 }}>
          Volver a explorar
        </Button>
      </Container>
    );
  }

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Button component={RouterLink} to="/" sx={{ mb: 2 }}>
        ← Volver a explorar
      </Button>
      {/* Placeholder de foto (2C) */}
      <Box sx={{ height: 260, bgcolor: 'grey.200', mb: 3 }} aria-hidden />
      <Typography variant="h4" component="h1" gutterBottom>
        {data.titulo}
      </Typography>
      <Typography variant="h5" color="primary" gutterBottom>
        {formatearBob(data.monto)}
      </Typography>
      <Stack direction="row" spacing={1} sx={{ mb: 2 }}>
        <Chip label={data.nombreCategoria} />
        <Chip label={data.nombreCiudad} />
        <Chip label={data.condicion} />
      </Stack>
      <Typography variant="body1" sx={{ whiteSpace: 'pre-wrap' }}>
        {data.descripcion}
      </Typography>
    </Container>
  );
}
```

- [ ] **Step 4: Correr typecheck, lint y tests**

Run: `cd web && npm run typecheck && npm run lint && npm run test -- DetalleAvisoPage`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/routes/DetalleAvisoPage.tsx web/src/routes/DetalleAvisoPage.test.tsx
git commit -m "feat(web): detalle publico de aviso"
```

---

### Task 5: `FormAviso` — formulario compartido crear/editar

**Files:**
- Create: `web/src/routes/FormAviso.tsx`
- Test: `web/src/routes/FormAviso.test.tsx`

**Interfaces:**
- Consumes: `listarCategorias`, `listarCiudades` (Task 2).
- Produces:
  - `export interface ValoresAviso { titulo: string; descripcion: string; monto: string; condicion: string; categoriaId: string; ciudadId: string; }`
  - `export function FormAviso(props: { inicial?: Partial<ValoresAviso>; enviando: boolean; textoBoton: string; onSubmit: (valores: ValoresAviso) => void; })`
- Nota: `monto` se maneja como `string` en el form (input controlado) y el llamador lo convierte a `number` al construir el request.

- [ ] **Step 1: Escribir el test (falla)**

`web/src/routes/FormAviso.test.tsx`:

```tsx
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { FormAviso } from './FormAviso';
import * as catalogo from '../api/catalogo';

afterEach(() => vi.restoreAllMocks());

function montar(onSubmit = vi.fn()) {
  vi.spyOn(catalogo, 'listarCategorias').mockResolvedValue([{ id: 'c1', nombre: 'Muebles' }]);
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([{ id: 'u1', nombre: 'La Paz' }]);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <FormAviso enviando={false} textoBoton="Publicar" onSubmit={onSubmit} />
    </QueryClientProvider>,
  );
  return onSubmit;
}

describe('FormAviso', () => {
  it('no envía y muestra errores si faltan campos requeridos', async () => {
    const onSubmit = montar();
    await screen.findByText('Muebles'); // catálogos cargados
    await userEvent.click(screen.getByRole('button', { name: /publicar/i }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/el título es obligatorio/i)).toBeInTheDocument();
  });

  it('envía los valores cuando el formulario es válido', async () => {
    const onSubmit = montar();
    await screen.findByText('Muebles');

    await userEvent.type(screen.getByLabelText(/título/i), 'Mesa');
    await userEvent.type(screen.getByLabelText(/descripción/i), 'De madera');
    await userEvent.type(screen.getByLabelText(/precio/i), '300');
    await userEvent.click(screen.getByRole('button', { name: /publicar/i }));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ titulo: 'Mesa', descripcion: 'De madera', monto: '300' }),
    );
  });
});
```

> Nota: los selects de MUI con etiqueta requieren asociar `label`/`id`; el test valida el flujo de errores y el submit mínimo. Si seleccionar categoría/ciudad en el test resulta frágil, precargar `inicial` con `categoriaId`/`ciudadId` en el segundo caso en vez de interactuar con los selects.

- [ ] **Step 2: Correr el test para verlo fallar**

Run: `cd web && npm run test -- FormAviso`
Expected: FAIL — `FormAviso` no existe.

- [ ] **Step 3: Implementar `FormAviso`**

```tsx
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Alert, Box, Button, MenuItem, Stack, TextField } from '@mui/material';
import { listarCategorias, listarCiudades } from '../api/catalogo';

export interface ValoresAviso {
  titulo: string;
  descripcion: string;
  monto: string;
  condicion: string;
  categoriaId: string;
  ciudadId: string;
}

const CONDICIONES = ['Nuevo', 'Usado'];

const VACIO: ValoresAviso = {
  titulo: '',
  descripcion: '',
  monto: '',
  condicion: 'Usado',
  categoriaId: '',
  ciudadId: '',
};

// Valida las reglas de dominio del aviso en cliente. Devuelve errores por campo.
function validar(v: ValoresAviso): Partial<Record<keyof ValoresAviso, string>> {
  const e: Partial<Record<keyof ValoresAviso, string>> = {};
  if (!v.titulo.trim()) e.titulo = 'El título es obligatorio';
  else if (v.titulo.length > 120) e.titulo = 'Máximo 120 caracteres';
  if (!v.descripcion.trim()) e.descripcion = 'La descripción es obligatoria';
  else if (v.descripcion.length > 2000) e.descripcion = 'Máximo 2000 caracteres';
  const monto = Number(v.monto);
  if (!v.monto || Number.isNaN(monto) || monto <= 0) e.monto = 'El precio debe ser mayor a 0';
  if (!v.categoriaId) e.categoriaId = 'Elige una categoría';
  if (!v.ciudadId) e.ciudadId = 'Elige una ciudad';
  return e;
}

export function FormAviso({
  inicial,
  enviando,
  textoBoton,
  onSubmit,
}: {
  inicial?: Partial<ValoresAviso>;
  enviando: boolean;
  textoBoton: string;
  onSubmit: (valores: ValoresAviso) => void;
}) {
  const [valores, setValores] = useState<ValoresAviso>({ ...VACIO, ...inicial });
  const [errores, setErrores] = useState<Partial<Record<keyof ValoresAviso, string>>>({});

  const categorias = useQuery({ queryKey: ['categorias'], queryFn: listarCategorias });
  const ciudades = useQuery({ queryKey: ['ciudades'], queryFn: listarCiudades });

  const set = (campo: keyof ValoresAviso) => (e: { target: { value: string } }) =>
    setValores((v) => ({ ...v, [campo]: e.target.value }));

  const enviar = () => {
    const e = validar(valores);
    setErrores(e);
    if (Object.keys(e).length === 0) onSubmit(valores);
  };

  return (
    <Stack spacing={3}>
      <TextField
        label="Título"
        value={valores.titulo}
        onChange={set('titulo')}
        error={!!errores.titulo}
        helperText={errores.titulo}
        inputProps={{ maxLength: 120 }}
      />
      <TextField
        label="Descripción"
        multiline
        minRows={4}
        value={valores.descripcion}
        onChange={set('descripcion')}
        error={!!errores.descripcion}
        helperText={errores.descripcion}
        inputProps={{ maxLength: 2000 }}
      />
      <TextField
        label="Precio (BOB)"
        type="number"
        value={valores.monto}
        onChange={set('monto')}
        error={!!errores.monto}
        helperText={errores.monto}
      />
      <TextField
        select
        label="Categoría"
        value={valores.categoriaId}
        onChange={set('categoriaId')}
        error={!!errores.categoriaId}
        helperText={errores.categoriaId}
      >
        {(categorias.data ?? []).map((c) => (
          <MenuItem key={c.id} value={c.id}>
            {c.nombre}
          </MenuItem>
        ))}
      </TextField>
      <TextField
        select
        label="Ciudad"
        value={valores.ciudadId}
        onChange={set('ciudadId')}
        error={!!errores.ciudadId}
        helperText={errores.ciudadId}
      >
        {(ciudades.data ?? []).map((c) => (
          <MenuItem key={c.id} value={c.id}>
            {c.nombre}
          </MenuItem>
        ))}
      </TextField>
      <TextField select label="Condición" value={valores.condicion} onChange={set('condicion')}>
        {CONDICIONES.map((c) => (
          <MenuItem key={c} value={c}>
            {c}
          </MenuItem>
        ))}
      </TextField>
      {(categorias.isError || ciudades.isError) && (
        <Alert severity="error">No se pudieron cargar las opciones. Recarga la página.</Alert>
      )}
      <Box>
        <Button variant="contained" onClick={enviar} disabled={enviando}>
          {textoBoton}
        </Button>
      </Box>
    </Stack>
  );
}
```

- [ ] **Step 4: Correr typecheck, lint y tests**

Run: `cd web && npm run typecheck && npm run lint && npm run test -- FormAviso`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/routes/FormAviso.tsx web/src/routes/FormAviso.test.tsx
git commit -m "feat(web): formulario compartido de aviso"
```

---

### Task 6: `CrearAvisoPage` — publicar con gate KYC

**Files:**
- Create: `web/src/routes/CrearAvisoPage.tsx`
- Test: `web/src/routes/CrearAvisoPage.test.tsx`

**Interfaces:**
- Consumes: `FormAviso`, `ValoresAviso` (Task 5); `crearAviso` (Task 2); `useAuth` de `../auth/AuthContext`; `useNavigate`, `Link` de react-router-dom; `HttpError` de `../api/http`.
- Produces: `export function CrearAvisoPage()`.

- [ ] **Step 1: Escribir el test (falla)**

`web/src/routes/CrearAvisoPage.test.tsx`. Se mockea `useAuth` para controlar `verificado`.

```tsx
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { CrearAvisoPage } from './CrearAvisoPage';
import * as authCtx from '../auth/AuthContext';
import * as catalogo from '../api/catalogo';

afterEach(() => vi.restoreAllMocks());

function mockAuth(verificado: boolean) {
  vi.spyOn(authCtx, 'useAuth').mockReturnValue({
    verificado,
    estaAutenticado: true,
    cargando: false,
    usuario: null,
    permisos: [],
    tienePermiso: () => false,
    iniciarSesion: vi.fn(),
    registrar: vi.fn(),
    cerrarSesion: vi.fn(),
  } as never);
}

function montar() {
  vi.spyOn(catalogo, 'listarCategorias').mockResolvedValue([{ id: 'c1', nombre: 'Muebles' }]);
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([{ id: 'u1', nombre: 'La Paz' }]);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <CrearAvisoPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('CrearAvisoPage', () => {
  it('muestra el gate KYC si el usuario no está verificado', () => {
    mockAuth(false);
    montar();
    expect(screen.getByText(/verificar tu identidad/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/título/i)).not.toBeInTheDocument();
  });

  it('muestra el formulario si el usuario está verificado', () => {
    mockAuth(true);
    montar();
    expect(screen.getByLabelText(/título/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Correr el test para verlo fallar**

Run: `cd web && npm run test -- CrearAvisoPage`
Expected: FAIL — `CrearAvisoPage` no existe.

- [ ] **Step 3: Implementar `CrearAvisoPage`**

```tsx
import { useMutation } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { Alert, Button, Container, Typography } from '@mui/material';
import { FormAviso, type ValoresAviso } from './FormAviso';
import { crearAviso } from '../api/avisos';
import { useAuth } from '../auth/AuthContext';
import { HttpError } from '../api/http';

export function CrearAvisoPage() {
  const { verificado } = useAuth();
  const navigate = useNavigate();

  const mutacion = useMutation({
    mutationFn: (v: ValoresAviso) =>
      crearAviso({
        titulo: v.titulo,
        descripcion: v.descripcion,
        monto: Number(v.monto),
        condicion: v.condicion,
        categoriaId: v.categoriaId,
        ciudadId: v.ciudadId,
      }),
    onSuccess: () => navigate('/mis-avisos'),
  });

  const es403 = mutacion.error instanceof HttpError && mutacion.error.status === 403;

  if (!verificado) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Typography variant="h4" component="h1" gutterBottom>
          Publicar un aviso
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Necesitas verificar tu identidad antes de publicar un aviso.
        </Alert>
        <Button component={RouterLink} to="/kyc" variant="contained">
          Verificar identidad
        </Button>
      </Container>
    );
  }

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Publicar un aviso
      </Typography>
      {mutacion.isError && es403 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          Necesitas verificar tu identidad. <RouterLink to="/kyc">Verificar ahora</RouterLink>
        </Alert>
      )}
      {mutacion.isError && !es403 && (
        <Alert severity="error" sx={{ mb: 2 }}>
          No se pudo publicar el aviso. Revisa los datos e inténtalo de nuevo.
        </Alert>
      )}
      <FormAviso enviando={mutacion.isPending} textoBoton="Publicar" onSubmit={mutacion.mutate} />
    </Container>
  );
}
```

- [ ] **Step 4: Correr typecheck, lint y tests**

Run: `cd web && npm run typecheck && npm run lint && npm run test -- CrearAvisoPage`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/routes/CrearAvisoPage.tsx web/src/routes/CrearAvisoPage.test.tsx
git commit -m "feat(web): publicar aviso con gate KYC"
```

---

### Task 7: `EditarAvisoPage` — editar aviso propio

**Files:**
- Create: `web/src/routes/EditarAvisoPage.tsx`
- Test: `web/src/routes/EditarAvisoPage.test.tsx`

**Interfaces:**
- Consumes: `FormAviso`, `ValoresAviso` (Task 5); `obtenerMiAviso`, `editarAviso`, `Aviso` (Task 2); `useParams`, `useNavigate`, `Link` de react-router-dom; `HttpError` de `../api/http`.
- Produces: `export function EditarAvisoPage()`.

- [ ] **Step 1: Escribir el test (falla)**

`web/src/routes/EditarAvisoPage.test.tsx`:

```tsx
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { EditarAvisoPage } from './EditarAvisoPage';
import * as avisos from '../api/avisos';
import * as catalogo from '../api/catalogo';
import { HttpError } from '../api/http';

afterEach(() => vi.restoreAllMocks());

function montar(id: string) {
  vi.spyOn(catalogo, 'listarCategorias').mockResolvedValue([{ id: 'c1', nombre: 'Muebles' }]);
  vi.spyOn(catalogo, 'listarCiudades').mockResolvedValue([{ id: 'u1', nombre: 'La Paz' }]);
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={[`/mis-avisos/${id}/editar`]}>
        <Routes>
          <Route path="/mis-avisos/:id/editar" element={<EditarAvisoPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('EditarAvisoPage', () => {
  it('precarga el formulario con los valores del aviso', async () => {
    vi.spyOn(avisos, 'obtenerMiAviso').mockResolvedValue({
      id: 'a1',
      vendedorId: 'v1',
      titulo: 'Mesa antigua',
      descripcion: 'De roble',
      monto: 500,
      moneda: 'BOB',
      categoriaId: 'c1',
      ciudadId: 'u1',
      condicion: 'Usado',
      estado: 'Activo',
      fechaCreacion: '2026-07-18T10:00:00Z',
      fechaActualizacion: '2026-07-18T10:00:00Z',
    });
    montar('a1');
    expect(await screen.findByDisplayValue('Mesa antigua')).toBeInTheDocument();
  });

  it('muestra un mensaje si el aviso no existe o no es propio (404/403)', async () => {
    vi.spyOn(avisos, 'obtenerMiAviso').mockRejectedValue(
      new HttpError(404, null, 'Petición fallida (404)'),
    );
    montar('x');
    expect(await screen.findByText(/no se encontró/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Correr el test para verlo fallar**

Run: `cd web && npm run test -- EditarAvisoPage`
Expected: FAIL — `EditarAvisoPage` no existe.

- [ ] **Step 3: Implementar `EditarAvisoPage`**

```tsx
import { useMutation, useQuery } from '@tanstack/react-query';
import { useNavigate, useParams } from 'react-router-dom';
import { Alert, Box, Button, CircularProgress, Container, Typography } from '@mui/material';
import { FormAviso, type ValoresAviso } from './FormAviso';
import { editarAviso, obtenerMiAviso } from '../api/avisos';

export function EditarAvisoPage() {
  const { id = '' } = useParams();
  const navigate = useNavigate();

  const { data, isLoading, isError } = useQuery({
    queryKey: ['mi-aviso', id],
    queryFn: () => obtenerMiAviso(id),
  });

  const mutacion = useMutation({
    mutationFn: (v: ValoresAviso) =>
      editarAviso(id, {
        titulo: v.titulo,
        descripcion: v.descripcion,
        monto: Number(v.monto),
        condicion: v.condicion,
        categoriaId: v.categoriaId,
        ciudadId: v.ciudadId,
      }),
    onSuccess: () => navigate('/mis-avisos'),
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError || !data) {
    return (
      <Container maxWidth="sm" sx={{ py: 4 }}>
        <Alert severity="error">No se encontró el aviso o no te pertenece.</Alert>
        <Button onClick={() => navigate('/mis-avisos')} sx={{ mt: 2 }}>
          Volver a mis avisos
        </Button>
      </Container>
    );
  }

  return (
    <Container maxWidth="sm" sx={{ py: 4 }}>
      <Typography variant="h4" component="h1" gutterBottom>
        Editar aviso
      </Typography>
      {mutacion.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          No se pudo guardar el aviso. Revisa los datos e inténtalo de nuevo.
        </Alert>
      )}
      <FormAviso
        inicial={{
          titulo: data.titulo,
          descripcion: data.descripcion,
          monto: String(data.monto),
          condicion: data.condicion,
          categoriaId: data.categoriaId,
          ciudadId: data.ciudadId,
        }}
        enviando={mutacion.isPending}
        textoBoton="Guardar cambios"
        onSubmit={mutacion.mutate}
      />
    </Container>
  );
}
```

- [ ] **Step 4: Correr typecheck, lint y tests**

Run: `cd web && npm run typecheck && npm run lint && npm run test -- EditarAvisoPage`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/routes/EditarAvisoPage.tsx web/src/routes/EditarAvisoPage.test.tsx
git commit -m "feat(web): editar aviso propio"
```

---

### Task 8: `MisAvisosPage` — listado y gestión del dueño

**Files:**
- Create: `web/src/routes/MisAvisosPage.tsx`
- Test: `web/src/routes/MisAvisosPage.test.tsx`

**Interfaces:**
- Consumes: `listarMisAvisos`, `pausarAviso`, `reactivarAviso`, `eliminarAviso`, `AvisoResumen` (Task 2); `formatearBob` (Task 3); `useNavigate`, `Link` de react-router-dom.
- Produces: `export function MisAvisosPage()`.

- [ ] **Step 1: Escribir el test (falla)**

`web/src/routes/MisAvisosPage.test.tsx`:

```tsx
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter } from 'react-router-dom';
import { MisAvisosPage } from './MisAvisosPage';
import * as avisos from '../api/avisos';

afterEach(() => vi.restoreAllMocks());

const activo: avisos.AvisoResumen = {
  id: 'a1',
  titulo: 'Mesa',
  monto: 300,
  moneda: 'BOB',
  categoriaId: 'c1',
  ciudadId: 'u1',
  condicion: 'Usado',
  estado: 'Activo',
  fechaCreacion: '2026-07-18T10:00:00Z',
};

function montar() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <MemoryRouter>
        <MisAvisosPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('MisAvisosPage', () => {
  it('lista mis avisos', async () => {
    vi.spyOn(avisos, 'listarMisAvisos').mockResolvedValue({
      items: [activo],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    montar();
    expect(await screen.findByText('Mesa')).toBeInTheDocument();
  });

  it('pausar un aviso activo llama al endpoint', async () => {
    vi.spyOn(avisos, 'listarMisAvisos').mockResolvedValue({
      items: [activo],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    const pausar = vi.spyOn(avisos, 'pausarAviso').mockResolvedValue(undefined);
    montar();
    await screen.findByText('Mesa');
    await userEvent.click(screen.getByRole('button', { name: /pausar/i }));
    await waitFor(() => expect(pausar).toHaveBeenCalledWith('a1'));
  });

  it('eliminar pide confirmación antes de llamar al endpoint', async () => {
    vi.spyOn(avisos, 'listarMisAvisos').mockResolvedValue({
      items: [activo],
      pagina: 1,
      tamano: 20,
      total: 1,
    });
    const eliminar = vi.spyOn(avisos, 'eliminarAviso').mockResolvedValue(undefined);
    montar();
    await screen.findByText('Mesa');
    await userEvent.click(screen.getByRole('button', { name: /eliminar/i }));
    // El diálogo de confirmación aparece; recién al confirmar se llama al endpoint.
    expect(eliminar).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: /confirmar/i }));
    await waitFor(() => expect(eliminar).toHaveBeenCalledWith('a1'));
  });
});
```

- [ ] **Step 2: Correr el test para verlo fallar**

Run: `cd web && npm run test -- MisAvisosPage`
Expected: FAIL — `MisAvisosPage` no existe.

- [ ] **Step 3: Implementar `MisAvisosPage`**

```tsx
import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Container,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Pagination,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material';
import {
  eliminarAviso,
  listarMisAvisos,
  pausarAviso,
  reactivarAviso,
  type AvisoResumen,
} from '../api/avisos';
import { formatearBob } from '../lib/formato';

const TAMANO = 20;

export function MisAvisosPage() {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [pagina, setPagina] = useState(1);
  const [aEliminar, setAEliminar] = useState<AvisoResumen | null>(null);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['mis-avisos', pagina],
    queryFn: () => listarMisAvisos(pagina, TAMANO),
  });

  const refrescar = () => qc.invalidateQueries({ queryKey: ['mis-avisos'] });

  const pausar = useMutation({ mutationFn: pausarAviso, onSuccess: refrescar });
  const reactivar = useMutation({ mutationFn: reactivarAviso, onSuccess: refrescar });
  const eliminar = useMutation({
    mutationFn: eliminarAviso,
    onSuccess: () => {
      setAEliminar(null);
      refrescar();
    },
  });

  const totalPaginas = data ? Math.max(1, Math.ceil(data.total / TAMANO)) : 1;

  return (
    <Container maxWidth="md" sx={{ py: 4 }}>
      <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
        <Typography variant="h4" component="h1">
          Mis avisos
        </Typography>
        <Button component={RouterLink} to="/publicar" variant="contained">
          Publicar aviso
        </Button>
      </Stack>

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 8 }}>
          <CircularProgress />
        </Box>
      ) : isError ? (
        <Alert severity="error">No se pudieron cargar tus avisos. Inténtalo más tarde.</Alert>
      ) : (data?.items.length ?? 0) === 0 ? (
        <Typography color="text.secondary">
          Todavía no publicaste avisos. <RouterLink to="/publicar">Publica el primero</RouterLink>.
        </Typography>
      ) : (
        <>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Título</TableCell>
                <TableCell>Precio</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell align="right">Acciones</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data?.items.map((a) => (
                <TableRow key={a.id}>
                  <TableCell>{a.titulo}</TableCell>
                  <TableCell>{formatearBob(a.monto)}</TableCell>
                  <TableCell>
                    <Chip
                      size="small"
                      label={a.estado}
                      color={a.estado === 'Activo' ? 'success' : 'default'}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Stack direction="row" spacing={1} justifyContent="flex-end">
                      <Button size="small" onClick={() => navigate(`/mis-avisos/${a.id}/editar`)}>
                        Editar
                      </Button>
                      {a.estado === 'Activo' ? (
                        <Button
                          size="small"
                          onClick={() => pausar.mutate(a.id)}
                          disabled={pausar.isPending}
                        >
                          Pausar
                        </Button>
                      ) : (
                        <Button
                          size="small"
                          onClick={() => reactivar.mutate(a.id)}
                          disabled={reactivar.isPending}
                        >
                          Reactivar
                        </Button>
                      )}
                      <Button size="small" color="error" onClick={() => setAEliminar(a)}>
                        Eliminar
                      </Button>
                    </Stack>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <Box sx={{ display: 'flex', justifyContent: 'center', mt: 4 }}>
            <Pagination count={totalPaginas} page={pagina} onChange={(_, p) => setPagina(p)} />
          </Box>
        </>
      )}

      <Dialog open={aEliminar !== null} onClose={() => setAEliminar(null)}>
        <DialogTitle>Eliminar aviso</DialogTitle>
        <DialogContent>
          <Typography>
            ¿Seguro que quieres eliminar «{aEliminar?.titulo}»? Esta acción no se puede deshacer.
          </Typography>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAEliminar(null)}>Cancelar</Button>
          <Button
            color="error"
            variant="contained"
            disabled={eliminar.isPending}
            onClick={() => aEliminar && eliminar.mutate(aEliminar.id)}
          >
            Confirmar
          </Button>
        </DialogActions>
      </Dialog>
    </Container>
  );
}
```

- [ ] **Step 4: Correr typecheck, lint y tests**

Run: `cd web && npm run typecheck && npm run lint && npm run test -- MisAvisosPage`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add web/src/routes/MisAvisosPage.tsx web/src/routes/MisAvisosPage.test.tsx
git commit -m "feat(web): mis avisos con pausar/reactivar/eliminar"
```

---

### Task 9: `AppLayout` + `AppBar` y cableado del router

Última tarea: crea el shell de navegación y re-parenta todas las rutas (existentes + nuevas) bajo él. Se hace al final porque referencia todas las páginas ya creadas.

**Files:**
- Create: `web/src/app/AppLayout.tsx`
- Modify: `web/src/app/router.tsx`
- Test: `web/src/app/AppLayout.test.tsx`

**Interfaces:**
- Consumes: todas las páginas de las tareas 3–8; `useAuth` de `../auth/AuthContext`; `Outlet`, `Link`, `useNavigate` de react-router-dom.
- Produces: `export function AppLayout()` (renderiza `AppBar` + `<Outlet/>`); router con la ruta padre `AppLayout` y `/` = `ExplorarPage`.

- [ ] **Step 1: Escribir el test del layout (falla)**

`web/src/app/AppLayout.test.tsx`. Se mockea `useAuth` para verificar los enlaces condicionados por sesión.

```tsx
import { describe, it, expect, vi, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import * as authCtx from '../auth/AuthContext';

afterEach(() => vi.restoreAllMocks());

function mockAuth(estaAutenticado: boolean) {
  vi.spyOn(authCtx, 'useAuth').mockReturnValue({
    estaAutenticado,
    verificado: false,
    cargando: false,
    usuario: null,
    permisos: [],
    tienePermiso: () => false,
    iniciarSesion: vi.fn(),
    registrar: vi.fn(),
    cerrarSesion: vi.fn(),
  } as never);
}

function montar() {
  render(
    <MemoryRouter>
      <AppLayout />
    </MemoryRouter>,
  );
}

describe('AppLayout', () => {
  it('muestra "Entrar" cuando no hay sesión', () => {
    mockAuth(false);
    montar();
    expect(screen.getByRole('link', { name: /explorar/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /entrar/i })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /mis avisos/i })).not.toBeInTheDocument();
  });

  it('muestra enlaces del dueño cuando hay sesión', () => {
    mockAuth(true);
    montar();
    expect(screen.getByRole('link', { name: /publicar/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /mis avisos/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Correr el test para verlo fallar**

Run: `cd web && npm run test -- AppLayout`
Expected: FAIL — `AppLayout` no existe.

- [ ] **Step 3: Implementar `AppLayout`**

`web/src/app/AppLayout.tsx`:

```tsx
import { AppBar, Box, Button, Container, Toolbar, Typography } from '@mui/material';
import { Link as RouterLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function AppLayout() {
  const { estaAutenticado, cerrarSesion } = useAuth();
  const navigate = useNavigate();

  const salir = async () => {
    await cerrarSesion();
    navigate('/');
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      <AppBar position="static">
        <Toolbar>
          <Typography
            variant="h6"
            component={RouterLink}
            to="/"
            sx={{ color: 'inherit', textDecoration: 'none', flexShrink: 0 }}
          >
            CaseritoApp
          </Typography>
          <Box sx={{ flexGrow: 1, display: 'flex', gap: 1, ml: 3 }}>
            <Button color="inherit" component={RouterLink} to="/">
              Explorar
            </Button>
            {estaAutenticado && (
              <>
                <Button color="inherit" component={RouterLink} to="/publicar">
                  Publicar
                </Button>
                <Button color="inherit" component={RouterLink} to="/mis-avisos">
                  Mis avisos
                </Button>
              </>
            )}
          </Box>
          {estaAutenticado ? (
            <>
              <Button color="inherit" component={RouterLink} to="/perfil">
                Perfil
              </Button>
              <Button color="inherit" onClick={salir}>
                Salir
              </Button>
            </>
          ) : (
            <Button color="inherit" component={RouterLink} to="/login">
              Entrar
            </Button>
          )}
        </Toolbar>
      </AppBar>
      <Container component="main" maxWidth={false} disableGutters sx={{ flexGrow: 1 }}>
        <Outlet />
      </Container>
    </Box>
  );
}
```

> Nota de accesibilidad para el test: los `Button` de MUI con `component={RouterLink}` se exponen con rol `link`. Si el "CaseritoApp" (que también es link a `/`) hace ambiguo el `getByRole('link', { name: /explorar/i })`, el test filtra por el nombre exacto "Explorar", que no colisiona con "CaseritoApp".

- [ ] **Step 4: Correr el test del layout**

Run: `cd web && npm run test -- AppLayout`
Expected: PASS.

- [ ] **Step 5: Re-cablear el router**

Reemplazar `web/src/app/router.tsx` para montar `AppLayout` como ruta padre con `children`, poner `/` = `ExplorarPage` y añadir las rutas nuevas. Las rutas protegidas siguen envueltas en `ProtectedRoute`.

```tsx
import { createBrowserRouter } from 'react-router-dom';
import { AppLayout } from './AppLayout';
import { ExplorarPage } from '../routes/ExplorarPage';
import { DetalleAvisoPage } from '../routes/DetalleAvisoPage';
import { CrearAvisoPage } from '../routes/CrearAvisoPage';
import { EditarAvisoPage } from '../routes/EditarAvisoPage';
import { MisAvisosPage } from '../routes/MisAvisosPage';
import { LoginPage } from '../routes/LoginPage';
import { RegistroPage } from '../routes/RegistroPage';
import { PerfilPage } from '../routes/PerfilPage';
import { NotFoundPage } from '../routes/NotFoundPage';
import { KycPage } from '../routes/KycPage';
import { AdminKycPage } from '../routes/AdminKycPage';
import { ProtectedRoute } from '../auth/ProtectedRoute';
import { RequierePermiso } from '../auth/RequierePermiso';

export const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { path: '/', element: <ExplorarPage /> },
      { path: '/avisos/:id', element: <DetalleAvisoPage /> },
      { path: '/login', element: <LoginPage /> },
      { path: '/registro', element: <RegistroPage /> },
      {
        path: '/publicar',
        element: (
          <ProtectedRoute>
            <CrearAvisoPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/mis-avisos',
        element: (
          <ProtectedRoute>
            <MisAvisosPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/mis-avisos/:id/editar',
        element: (
          <ProtectedRoute>
            <EditarAvisoPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/perfil',
        element: (
          <ProtectedRoute>
            <PerfilPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/kyc',
        element: (
          <ProtectedRoute>
            <KycPage />
          </ProtectedRoute>
        ),
      },
      {
        path: '/admin/kyc',
        element: (
          <ProtectedRoute>
            <RequierePermiso permiso="kyc.revisar">
              <AdminKycPage />
            </RequierePermiso>
          </ProtectedRoute>
        ),
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
```

- [ ] **Step 6: Correr toda la suite del frontend + build**

Run: `cd web && npm run typecheck && npm run lint && npm run test && npm run build`
Expected: PASS — toda la suite verde y build sin errores.

- [ ] **Step 7: Commit**

```bash
git add web/src/app/AppLayout.tsx web/src/app/AppLayout.test.tsx web/src/app/router.tsx
git commit -m "feat(web): shell de navegacion y cableado de rutas de catalogo"
```

---

## Self-Review

**Cobertura del spec:**
- §3 rutas/layout → Task 9. §4 capa API → Task 2. §5 backend anónimo → Task 1. §6 Explorar → Task 3. §7 Detalle → Task 4. §8 Publicar/Editar → Tasks 5–7. §9 Mis avisos → Task 8. §10 tests → distribuidos en cada tarea. §11 anti-PII → constraint global (mensajes genéricos vía `HttpError`). §12 decisiones → reflejadas. §13 fuera de alcance → placeholders de foto (Tasks 3–4), sin reportar/vendedor.
- Formateo de moneda (§6) no estaba como archivo en el spec; se añadió `web/src/lib/formato.ts` (Task 3) por DRY (lo usan Explorar, Detalle y Mis avisos).

**Type consistency:** `FiltroBusqueda` (Task 2) usado en Task 3. `ValoresAviso` (Task 5) consumido por Tasks 6–7 con la misma forma (`monto: string`, convertido a `number` en el request). `formatearBob(monto: number | string)` (Task 3) usado en Tasks 3, 4, 8. Firmas de `avisos.ts`/`catalogo.ts` (Task 2) coinciden con los mocks de los tests. Router de Task 9 importa exactamente los nombres exportados por las páginas.

**Placeholder scan:** sin TBD/TODO; todos los pasos de código llevan código completo.
