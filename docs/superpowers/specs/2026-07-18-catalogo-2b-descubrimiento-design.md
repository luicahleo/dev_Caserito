# Fase 2 — Bloque 2B: Descubrimiento (backend)

**Fecha:** 2026-07-18
**Contexto:** Catalog
**Tipo:** backend (read-side público)
**Base:** bloque 2A (dominio `Aviso` + CRUD del dueño), commit merge `60b62ce`.

## 1. Objetivo

Que el público (usuarios anónimos) pueda **encontrar** avisos: buscarlos por
texto y filtros, listarlos por defecto, y ver el detalle de uno. Solo se exponen
avisos en estado `Activo`.

Es el segundo bloque con valor de producto de marketplace: 2A permitió al dueño
publicar; 2B permite que el resto los descubra.

## 2. Alcance

**Incluye:**

- Búsqueda por texto sobre título/descripción.
- Filtros combinables: categoría, ciudad, rango de precio, condición.
- Listado por defecto (sin parámetros): avisos recientes.
- Detalle público de un aviso.
- Solo avisos en estado `Activo`. Los `Pausado` y `Eliminado` nunca aparecen en
  listados, búsquedas ni detalle.

**Difiere (no en este bloque):**

- Fotos del aviso (2C).
- Moderación / reportes (2D).
- UI web de catálogo (2E).
- Exponer datos del vendedor y "contactar al vendedor" (Fase 3 — Chat).

## 3. Endpoints

Ambos **anónimos** (`AllowAnonymous`): el descubrimiento es público, cualquiera
puede ver el catálogo sin autenticarse. Son endpoints nuevos, separados de los
`[Authorize]` del dueño de 2A (`/api/avisos`, `/api/catalogo`).

### `GET /api/publico/avisos`

Búsqueda + filtros + paginación. **Todos los parámetros son opcionales**; sin
parámetros equivale al listado de avisos recientes.

Parámetros de query:

| Parámetro    | Tipo    | Notas                                                        |
|--------------|---------|--------------------------------------------------------------|
| `q`          | string? | Texto libre; se tokeniza (ver §5).                           |
| `categoriaId`| Guid?   | Filtra por categoría.                                        |
| `ciudadId`   | Guid?   | Filtra por ciudad.                                           |
| `precioMin`  | decimal?| Cota inferior de precio (BOB).                               |
| `precioMax`  | decimal?| Cota superior de precio (BOB).                               |
| `condicion`  | string? | `Nuevo` / `Usado` (`CondicionArticulo`).                     |
| `pagina`     | int     | Default `1`, ≥ 1.                                            |
| `tamano`     | int     | Default `20`, rango `1–50`.                                  |

Respuesta: `200` con `ResultadoPaginado<AvisoPublicoResumenDto>`.

### `GET /api/publico/avisos/{id}`

Detalle público de un aviso.

- `200` con `AvisoPublicoDto` si el aviso existe **y** está `Activo`.
- `404` en cualquier otro caso: inexistente, `Pausado` o `Eliminado`. No se
  distingue el motivo, para no filtrar información sobre avisos no públicos.

## 4. DTOs públicos (nuevos)

Se crean DTOs dedicados en `Catalog.Application` (no se reutilizan
`AvisoResumenDto` / `AvisoDto` de 2A, que están pensados para la vista del dueño
y exponen `VendedorId` / `Estado`).

```csharp
public sealed record AvisoPublicoResumenDto(
    Guid Id,
    string Titulo,
    decimal Monto,
    string Moneda,
    string NombreCategoria,
    string NombreCiudad,
    string Condicion,
    DateTime FechaCreacion);

public sealed record AvisoPublicoDto(
    Guid Id,
    string Titulo,
    string Descripcion,
    decimal Monto,
    string Moneda,
    string NombreCategoria,
    string NombreCiudad,
    string Condicion,
    DateTime FechaCreacion);
```

Decisiones de forma:

- Resuelven **nombres** de categoría y ciudad (join al catálogo sembrado), no
  GUIDs crudos: mejor UX pública y evita que el cliente resuelva nombres aparte.
- **No** exponen `VendedorId` (menor superficie de correlación; se añadirá si
  Chat lo necesita en Fase 3), ni `Estado` (siempre `Activo` por construcción),
  ni `FechaActualizacion`.

## 5. Búsqueda y filtros

Estrategia: **búsqueda por base de datos** con `LIKE` (sin motor dedicado ni
Full-Text Search), suficiente a la escala del MVP y sin añadir dependencias a la
imagen de SQL Server (funciona igual bajo Testcontainers).

- **Tokenización de `q`:** se parte por espacios en blanco en tokens no vacíos.
  Cada token debe aparecer (**AND** entre tokens) en `Titulo` **o** `Descripcion`
  (`LIKE '%token%'`). Si `q` es nulo/vacío/solo espacios, no se aplica filtro de
  texto.
- **Normalización acento/mayúscula-insensible:** las comparaciones `LIKE` usan un
  `COLLATE Latin1_General_CI_AI` explícito (vía `EF.Functions.Collate`), de modo
  que la insensibilidad a mayúsculas y **acentos** no depende de la colación de la
  columna (que es accent-sensitive por defecto en SQL Server). Así "camara"
  encuentra "cámara". Esto cierra el follow-up de "case-insensitive depende de la
  colación" anotado en bloques previos.
- **Filtros** (todos opcionales, combinables entre sí y con `q`):
  - `categoriaId`: `Where(a => a.CategoriaId == categoriaId)`.
  - `ciudadId`: `Where(a => a.CiudadId == ciudadId)`.
  - `precioMin` / `precioMax`: cotas independientes sobre `Precio.Monto` (BOB es
    la única moneda existente hoy).
  - `condicion`: `Where(a => a.Condicion == condicion)` (parseada a enum).
- **Orden por defecto:** `FechaCreacion DESC`, con desempate por `Id` para que la
  paginación sea determinista y estable. No se expone parámetro de orden en este
  bloque (YAGNI).
- **Paginación:** reutiliza `ResultadoPaginado<T>`. `tamano` default 20, rango
  1–50 (tope más conservador que el 1–100 del listado del dueño, al ser una
  superficie pública). `pagina` ≥ 1. `Total` es el conteo total tras aplicar
  filtros.

## 6. Capas (CQRS-lite; patrones de 2A)

### Application (`CaseritoApp.Catalog.Application`)

- **Puerto de lectura nuevo** `IConsultaAvisosPublica` (separado de
  `IConsultaCatalogo`, que es para catálogos de referencia):

  ```csharp
  public interface IConsultaAvisosPublica
  {
      Task<ResultadoPaginado<AvisoPublicoResumenDto>> BuscarAsync(
          FiltroBusquedaAvisos filtro, int pagina, int tamano, CancellationToken ct);

      Task<AvisoPublicoDto?> ObtenerPublicoAsync(Guid id, CancellationToken ct);
  }
  ```

  `FiltroBusquedaAvisos` es un record con los criterios ya normalizados
  (`Texto`, `CategoriaId`, `CiudadId`, `PrecioMin`, `PrecioMax`, `Condicion`).

- `BuscarAvisosQuery(...)` → `ResultadoPaginado<AvisoPublicoResumenDto>`, con
  handler que delega en el puerto, y validator:
  - `pagina ≥ 1`, `tamano` entre 1 y 50.
  - Si vienen ambos, `precioMin ≤ precioMax`.
  - `precioMin`/`precioMax` ≥ 0 si vienen.
  - `condicion`, si viene, debe parsear a `CondicionArticulo` (parseo defensivo).
- `ObtenerAvisoPublicoQuery(Guid Id)` → `AvisoPublicoDto?`; el endpoint mapea
  `null` → `404`.

### Infrastructure (`CaseritoApp.Catalog.Infrastructure`)

- Adaptador `ConsultaAvisosPublicaEfCore` sobre `CatalogDbContext`:
  - Base `IQueryable`: `Where(a => a.Estado == EstadoAviso.Activo)`.
  - Aplica filtros condicionalmente (solo los provistos).
  - Texto: `EF.Functions.Like(EF.Functions.Collate(campo, "Latin1_General_CI_AI"), $"%{token}%")`
    por token, sobre título y descripción.
  - Join a `Categorias` / `Ciudades` para resolver nombres.
  - `OrderByDescending(FechaCreacion).ThenBy(Id)`.
  - Proyección directa a DTO en la consulta (`Select`), conteo total aparte.
- Registro en `DependencyInjection`.

### Domain

- Sin cambios. El descubrimiento es puramente read-side; no toca el agregado ni
  su máquina de estados.

### Aislamiento de contexto

- Catalog no referencia otros bounded contexts. Verificado por
  `CaseritoApp.ArchitectureTests`.

## 7. Contrato OpenAPI y cliente TS

- Los dos endpoints se anotan (`.Produces<ResultadoPaginadoOfAvisoPublicoResumenDto>()`,
  `.Produces<AvisoPublicoDto>()`, `.ProducesProblem(404)`) para que entren al
  contrato OpenAPI y al cliente TS tipado vía el job `contract` del CI.
- Al ser anónimos, no requieren Bearer (revisar que el requisito global de
  seguridad no los marque como autenticados — follow-up conocido del bloque H).

## 8. Tests

- **Unit** (`CaseritoApp.UnitTests`): validators de `BuscarAvisosQuery`
  (paginación, rango de precio invertido, condición inválida).
- **Integración** (Testcontainers, `CaseritoApiFactory`):
  - Solo aparecen avisos `Activo` (sembrar Activo + Pausado + Eliminado; verificar
    que solo el Activo se lista y que detalle de Pausado/Eliminado da 404).
  - Cada filtro por separado (categoría, ciudad, precioMin, precioMax, condición).
  - Combinación de `q` + varios filtros.
  - Acento-insensibilidad ("camara" encuentra "cámara"; "CÁMARA" también).
  - Tokens múltiples (AND).
  - Paginación (total, páginas, orden recientes-primero determinista).
  - Detalle: `200` para Activo, `404` para inexistente / Pausado / Eliminado.

## 9. Follow-ups de 2A a barrer de paso

Como 2B toca `AvisosEndpoints` / handlers, se aprovechan los menores anotados:

- Eliminar el helper `UserId()` (que produce `Guid.Empty` silencioso) y pasar el
  `userId` parseado una vez a las lambdas.
- `Enum.TryParse` defensivo en los handlers Crear/Editar.
- Pragma `S1144` superfluo/inconsistente en los ctores EF de `Categoria`/`Ciudad`.

**Fuera de alcance:** la no-transaccionalidad del `UnitOfWorkBehavior` sigue
latente porque 2B no introduce ningún command que escriba en dos contextos (es
read-only). Se aborda cuando aparezca el primer command cross-contexto.

## 10. Decisiones tomadas en el brainstorming

1. Búsqueda de texto: `LIKE` tokenizado con normalización (colación CI_AI), no
   Full-Text Search ni motor dedicado.
2. Filtros: categoría, ciudad, rango de precio y condición — todos combinables.
3. Orden por defecto: más recientes primero.
4. Autenticación: anónimo (descubrimiento público).
5. Endpoints: uno de búsqueda + uno de detalle (mínima superficie).
6. DTOs públicos nuevos con nombres resueltos, sin `VendedorId` / `Estado`.
