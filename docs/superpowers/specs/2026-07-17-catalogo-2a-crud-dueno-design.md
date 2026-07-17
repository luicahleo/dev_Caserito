# Bloque 2A — Dominio Aviso + CRUD del dueño (Catálogo, backend)

> Fase 2 (Catálogo y descubrimiento) del MVP de CaseritoApp. Primer sub-bloque:
> el modelo de dominio del aviso y los casos de uso del dueño. Deriva del plan
> `CaseritoApp/Documentacion/plan-desarrollo-mvp-v1.md` (Fase 2) y del brief.

## Contexto y alcance

Fase 2 completa abarca: publicación, edición/eliminación, búsqueda/filtros,
almacenamiento de imágenes y moderación. Se descompone en sub-bloques:

| Sub-bloque | Contenido | Estado |
|---|---|---|
| **2A — Dominio Aviso + CRUD dueño** | Agregado `Aviso`, máquina de estados, crear/editar/pausar/reactivar/eliminar (dueño), catálogos de referencia, persistencia, tests | **este spec** |
| 2B — Descubrimiento | Listar/buscar/filtrar (público) + ver detalle, paginación | diferido |
| 2C — Fotos | Almacenamiento de imágenes del aviso | diferido |
| 2D — Moderación | Reportar, ocultar/eliminar por Moderador (RBAC) | diferido |
| 2E — UI web catálogo | Pantallas React (publicar/explorar/detalle) | diferido |

**Este bloque es solo backend, solo el flujo del dueño.** La búsqueda y el
detalle públicos son 2B; las fotos son 2C; ambos se anticipan en el diseño para
no requerir refactor.

### Decisiones tomadas en el brainstorming

1. **Alcance:** solo 2A (CRUD del dueño). Descubrimiento, fotos, moderación y UI en bloques aparte.
2. **Categorías:** catálogo fijo sembrado (entidad `Categoria` en schema `catalog`, seeder idempotente). No gestionable por UI todavía.
3. **Ubicación:** ciudad de catálogo fijo sembrado (entidad `Ciudad`); el aviso referencia `CiudadId`. Sin zona/barrio ni lat/long en este bloque.
4. **Gate KYC:** el claim `verificado` (KYC aprobado, ya emitido en el JWT) **es requisito para crear** un aviso. Editar/pausar/reactivar/eliminar de avisos propios no re-verifica.
5. **Estados:** `Activo ⇄ Pausado`, y `Eliminado` (soft-delete, terminal). "Vendido" se difiere a Orders (Fase 4), porque nace del cierre de una orden.

## Patrones que se reutilizan

- **CQRS-lite:** MediatR + `Result` + FluentValidation (patrón Identity).
- **Clean Architecture:** `Domain` sin dependencias externas; `Application` solo depende de `Domain`; verificado por `ArchitectureTests`.
- **Schema por contexto sin FK cruzada** (patrón Fase 0): `catalog`; `VendedorId` es un `Guid` opaco, sin FK al schema `identity`.
- **Cliente OpenAPI tipado** (Bloque H): los endpoints nuevos se anotan con `.Produces/.Accepts/.ProducesProblem` y entran al contrato + cliente TS vía el job `contract` del CI.
- **Seeder idempotente** con el flag `Migraciones:EjecutarAlArranque` (patrón del seeder de roles RBAC).
- **Anti-PII:** el catálogo no maneja PII sensible; logs solo con ids/estado/acción/resultado.

## 1. Dominio (`Catalog.Domain`)

### Agregado raíz `Aviso`

Campos:

- `Id: Guid` (value-generated never; `Entity` base asigna `Guid.NewGuid()`).
- `VendedorId: Guid` — id opaco del dueño (claim `sub`); sin FK cruzada.
- `Titulo: string` — no vacío, máx. 120.
- `Descripcion: string` — no vacío, máx. 2000.
- `Precio: Money` — value object: `Monto decimal` (> 0) + `Moneda` (enum; MVP solo `BOB`).
- `CategoriaId: Guid` — referencia al catálogo sembrado; validada como existente.
- `CiudadId: Guid` — referencia al catálogo sembrado; validada como existente.
- `Condicion: CondicionArticulo` — enum `Nuevo | Usado`, persistido como string.
- `Estado: EstadoAviso` — enum `Activo | Pausado | Eliminado`, persistido como string.
- `FechaCreacion: DateTime` (UTC), `FechaActualizacion: DateTime` (UTC).

### Máquina de estados

Métodos en el agregado, invariantes encapsuladas dentro:

- Crear → nace en `Activo`.
- `Pausar()`: `Activo → Pausado`.
- `Reactivar()`: `Pausado → Activo`.
- `Eliminar()`: cualquiera → `Eliminado` (soft-delete). **Terminal**: un aviso `Eliminado` no se puede editar, pausar, reactivar ni volver a eliminar (esas operaciones devuelven error de dominio).
- `Editar(...)`: permitido en `Activo` o `Pausado`; prohibido en `Eliminado`.

Todo cambio actualiza `FechaActualizacion`.

### Entidades de referencia

- `Categoria`: `Id: Guid`, `Nombre: string`, `Activa: bool`, `Orden: int`.
- `Ciudad`: `Id: Guid`, `Nombre: string`, `Activa: bool`, `Orden: int`.

Sembradas por seeder idempotente. No gestionables por UI en este bloque (2A). Ids
estables (Guids fijos en el seeder) para reproducibilidad entre entornos.

### Value object `Money`

- `Monto: decimal` (> 0), `Moneda: Moneda` (enum; MVP solo `BOB`).
- Owned type en EF; inmutable; igualdad por valor.

### Eventos de dominio

Levantados en el agregado, **sin consumidores ni dispatcher todavía** (eventos
in-process diferidos, mismo criterio que el resto del proyecto):

- `AvisoPublicado` (gancho para el contrato `ProductPublished` de Fase 0).
- `AvisoEditado`, `AvisoPausado`, `AvisoReactivado`, `AvisoEliminado`.

### Fotos (fuera de 2A)

No se implementan aquí. El agregado se diseña para admitir una colección
`FotoAviso` (referencias opacas a blobs) en el bloque 2C sin refactor del modelo.

## 2. Casos de uso (`Catalog.Application`)

CQRS-lite: MediatR + `Result` + FluentValidation. Todos `[Authorize]`.

### Commands (dueño)

- **`CrearAviso`** — `(titulo, descripcion, monto, moneda, categoriaId, ciudadId, condicion) → Guid`.
  - **Gated por claim `verificado`**: si el usuario no está verificado → `Result` de fallo → 403.
  - Valida que `CategoriaId` y `CiudadId` existan y estén activas.
  - `VendedorId` = claim `sub` del llamante.
- **`EditarAviso`** — `(id, titulo, descripcion, monto, moneda, categoriaId, ciudadId, condicion) → 204`.
  - Solo dueño (`VendedorId == sub`). Prohibido si `Eliminado`.
- **`PausarAviso`** — `(id) → 204`. Solo dueño.
- **`ReactivarAviso`** — `(id) → 204`. Solo dueño.
- **`EliminarAviso`** — `(id) → 204`. Solo dueño; soft-delete.

### Queries (dueño)

- **`ListarMisAvisos`** — `(pagina, tamano) → ResultadoPaginado<AvisoResumenDto>`. Avisos del `sub`, excluye `Eliminado`.
- **`ObtenerMiAviso`** — `(id) → AvisoDto`. Detalle propio (para cargar el form de edición). Solo dueño.

### Autorización y errores

- Propiedad: los commands y `ObtenerMiAviso` verifican `VendedorId == sub`. Si el aviso existe pero no es del llamante → **403**; si no existe → **404**.
- `CrearAviso` exige `verificado` → **403** si falta.
- La identidad del llamante (`sub`, `verificado`) se toma de los claims del JWT (mismo mecanismo que Identity/KYC).

## 3. Endpoints + contrato OpenAPI (`Host`)

Bajo `/api/avisos`, anotados con `.Produces<T>()`/`.Accepts<T>()`/`.ProducesProblem()`
(patrón Bloque H) para que entren al contrato y al cliente TS:

- `POST /api/avisos` — crear → `201 { id }` | 400 | 403 (no verificado).
- `PUT /api/avisos/{id}` — editar → 204 | 400 | 403 | 404.
- `POST /api/avisos/{id}/pausar` → 204 | 400 | 403 | 404.
- `POST /api/avisos/{id}/reactivar` → 204 | 400 | 403 | 404.
- `DELETE /api/avisos/{id}` — soft-delete → 204 | 403 | 404.
- `GET /api/avisos/mios` — listar mis avisos → `200 ResultadoPaginado<AvisoResumenDto>`.
- `GET /api/avisos/mios/{id}` — detalle propio → `200 AvisoDto` | 403 | 404.

Referencia (para poblar formularios en la UI de 2E), `[Authorize]`:

- `GET /api/catalogo/categorias` → `200 CategoriaDto[]` (solo activas, ordenadas).
- `GET /api/catalogo/ciudades` → `200 CiudadDto[]` (solo activas, ordenadas).

Mapeo `Result`→HTTP: 200/201/204 éxito · 400 validación · 403 no-dueño/no-verificado · 404 inexistente.

## 4. Persistencia (`Catalog.Infrastructure`)

- `CatalogDbContext` (ya existe, vacío): configuraciones EF de `Aviso`, `Categoria`, `Ciudad`.
- `Money` como owned type de `Aviso`; enums (`Condicion`, `Estado`, `Moneda`) como string.
- Índices: `VendedorId`, `Estado` (y compuesto `(VendedorId, Estado)` para `ListarMisAvisos`).
- Migración `CatalogInicial` (schema `catalog`).
- Seeder idempotente de `Categoria` y `Ciudad` (Guids fijos), ejecutado con el flag `Migraciones:EjecutarAlArranque` existente y en la factory de tests.
- `UnitOfWork` del contexto siguiendo el patrón de Identity.

## 5. Tests

- **Unit (dominio):** invariantes del agregado (título/descripción/precio inválidos), transiciones de estado (pausar/reactivar/eliminar, terminalidad de `Eliminado`, editar prohibido tras eliminar), `Money` (monto > 0, igualdad por valor).
- **Unit (handlers):** verificación de propiedad; gate `verificado` en `CrearAviso`; validación de `CategoriaId`/`CiudadId` inexistentes o inactivas.
- **Integración (Testcontainers.MsSql):** flujo completo crear→editar→pausar→reactivar→eliminar; 403 al operar sobre aviso ajeno; 403 al crear sin `verificado`; `ListarMisAvisos` excluye `Eliminado` y pagina; seeder siembra categorías/ciudades; endpoints de referencia responden.
- **Arquitectura:** las reglas existentes de `ArchitectureTests` cubren los proyectos de Catalog automáticamente (Domain sin dependencias externas, Application solo Domain).

## Anti-PII

El catálogo no maneja PII sensible (no hay documentos, selfies ni datos
biométricos). Los logs registran solo ids (aviso, vendedor, categoría, ciudad),
estado, acción y resultado. No se loguea contenido libre del usuario más allá de
lo necesario para diagnóstico, y nunca datos de identidad.

## Deja preparado el futuro

- El aviso captura `CiudadId` y atributos que luego alimentan el cálculo de envío por origen/destino (Fase 4).
- El agregado admite la colección `FotoAviso` (2C) sin refactor.
- El evento `AvisoPublicado` es el gancho del contrato `ProductPublished` (Fase 0) cuando exista el dispatcher.
- La búsqueda/detalle público (2B) leerá el mismo agregado; `Estado` y los índices ya lo soportan.

## Fuera de alcance de 2A

- Búsqueda, filtros y detalle público (2B).
- Fotos / almacenamiento de imágenes (2C).
- Reportes y moderación (2D).
- UI web (2E).
- Estado "Vendido" (nace de Orders, Fase 4).
- Gestión de categorías/ciudades por admin (YAGNI para ~20 usuarios).
- Múltiples monedas (MVP solo BOB).
