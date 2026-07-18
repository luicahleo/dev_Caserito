# Fase 2 — Bloque 2E: UI web de catálogo (frontend React)

**Fecha:** 2026-07-18
**Contexto:** Catalog (frontend `web/`) + un ajuste mínimo en `Host`
**Tipo:** frontend (SPA React) que consume la API de catálogo ya existente
**Base:** bloques 2A (CRUD del dueño, `/api/avisos` + `/api/catalogo`, `[Authorize]`)
y 2B (descubrimiento público, `/api/publico/avisos`, anónimo), ambos mergeados.

## 1. Objetivo

Que el marketplace se pueda usar de punta a punta desde el navegador: cualquiera
explora y ve el detalle de avisos sin login; un usuario autenticado publica,
edita, pausa, reactiva y elimina sus avisos, y ve "mis avisos". Se apoya en el
cliente OpenAPI tipado (`web/src/api/schema.d.ts`) y en los patrones ya
establecidos de MUI + React Router + TanStack Query.

## 2. Alcance

**Incluye:**

- Pantalla pública de listado/búsqueda con filtros (texto, categoría, ciudad,
  rango de precio, condición) + paginación.
- Detalle público de un aviso (anónimo).
- Pantallas protegidas del dueño: crear, editar, pausar, reactivar, eliminar, y
  "mis avisos".
- Gate KYC en la creación (proactivo en UI + manejo defensivo del 403).
- Shell de navegación compartido (AppBar) y raíz `/` = explorar.
- Ajuste backend mínimo: abrir `/api/catalogo/*` a anónimo (necesario para los
  dropdowns de filtro públicos).

**Difiere (no en este bloque):**

- Fotos del aviso (2C) — se muestran placeholders.
- Moderación / reportar (2D) — sin botón de reportar.
- Datos del vendedor y "contactar al vendedor" (Fase 3 — Chat).
- Selector de orden (2B fija el orden en "recientes primero", sin parámetro).
- Gestión de categorías/ciudades por admin (YAGNI).
- i18n más allá de español.

## 3. Arquitectura y rutas

Se introduce un **layout compartido** `AppLayout` con `AppBar` de MUI, montado
como ruta padre en `web/src/app/router.tsx`; todas las rutas cuelgan de él vía
`<Outlet/>`. La raíz pasa a ser el catálogo público (hoy `/` redirige a
`/perfil`).

| Ruta | Página | Acceso | Notas |
|---|---|---|---|
| `/` | `ExplorarPage` | anónimo | listado público + filtros + paginación |
| `/avisos/:id` | `DetalleAvisoPage` | anónimo | detalle público; 404 → "no disponible" |
| `/publicar` | `CrearAvisoPage` | protegida | gate KYC proactivo |
| `/mis-avisos` | `MisAvisosPage` | protegida | listado del dueño + acciones |
| `/mis-avisos/:id/editar` | `EditarAvisoPage` | protegida | precarga con `GET /mios/{id}` |
| `/perfil`, `/kyc`, `/admin/kyc`, `/login`, `/registro` | existentes | igual | se re-parentan bajo el layout |

- La edición se anida bajo `/mis-avisos/...` para no colisionar con el detalle
  público `/avisos/:id`. `/publicar` es un segmento estático (React Router lo
  rankea por encima de segmentos dinámicos, sin ambigüedad).
- `AppBar`: siempre "Explorar". Con sesión → "Publicar", "Mis avisos", "Perfil",
  "Salir". Sin sesión → "Entrar". Los enlaces protegidos usan
  `useAuth().estaAutenticado`.
- Las rutas protegidas siguen envueltas en `ProtectedRoute` (patrón existente).

## 4. Capa de API (`web/src/api/`)

Dos módulos nuevos, wrappers finos sobre `api` + `desempaquetar` (patrón de
`kyc.ts`). Re-exportan los tipos del schema; sin PII en mensajes de error (el
transporte ya mapea a `HttpError` con solo status + code).

### `catalogo.ts` (referencia)

- `listarCategorias(): Promise<CategoriaDto[]>` → `GET /api/catalogo/categorias`.
- `listarCiudades(): Promise<CiudadDto[]>` → `GET /api/catalogo/ciudades`.

### `avisos.ts`

Tipos re-exportados: `AvisoPublicoResumenDto`, `AvisoPublicoDto`,
`AvisoResumenDto`, `AvisoDto`, `CrearAvisoRequest`, `EditarAvisoRequest`,
`ResultadoPaginadoOfAvisoPublicoResumenDto`, `ResultadoPaginadoOfAvisoResumenDto`.
Un tipo local `FiltroBusqueda` agrupa `q`, `categoriaId`, `ciudadId`, `precioMin`,
`precioMax`, `condicion` (todos opcionales) para pasarlos como query.

Funciones públicas:

- `buscarAvisos(filtro, pagina, tamano)` → `GET /api/publico/avisos`.
- `obtenerAvisoPublico(id)` → `GET /api/publico/avisos/{id}`.

Funciones del dueño:

- `listarMisAvisos(pagina, tamano)` → `GET /api/avisos/mios`.
- `obtenerMiAviso(id)` → `GET /api/avisos/mios/{id}`.
- `crearAviso(req: CrearAvisoRequest)` → `POST /api/avisos` (201 → `{ id }`).
- `editarAviso(id, req: EditarAvisoRequest)` → `PUT /api/avisos/{id}` (204).
- `pausarAviso(id)` → `POST /api/avisos/{id}/pausar` (204).
- `reactivarAviso(id)` → `POST /api/avisos/{id}/reactivar` (204).
- `eliminarAviso(id)` → `DELETE /api/avisos/{id}` (204).

**Nota de contrato:** `CrearAvisoRequest`/`EditarAvisoRequest` **no** incluyen
`moneda` (el backend asume BOB). El monto viaja como `number`.

## 5. Cambio backend mínimo (desbloquea filtros anónimos)

Los DTOs públicos resuelven **nombres** (`nombreCategoria`, `nombreCiudad`) pero
el listado público filtra por **ids** (`categoriaId`, `ciudadId`), y los
endpoints de catálogo de referencia son `[Authorize]`. Un usuario anónimo no
podría poblar los dropdowns de filtro.

**Decisión:** `GET /api/catalogo/categorias` y `GET /api/catalogo/ciudades` pasan
de `[Authorize]` a `AllowAnonymous`. Es semánticamente limpio: son listas de
catálogo sembradas, no contienen PII, y ya alimentan una superficie pública.

Trabajo asociado:

- Quitar el requisito de auth de esos dos endpoints en el `Host`.
- Ajustar el/los test(s) de integración que hoy asuman `401` sin token en esos
  endpoints (ahora deben responder `200` sin Bearer).
- Actualizar `CLAUDE.md` (sección Auth) donde documenta `/api/catalogo/*` como
  `[Authorize]`.

## 6. Explorar (público) — `ExplorarPage`

- **Filtros:** texto (`q`), categoría (dropdown poblado con `listarCategorias`),
  ciudad (dropdown con `listarCiudades`), precio mínimo, precio máximo, condición
  (Nuevo/Usado).
- **Estado de filtros + página sincronizado a la URL** vía `useSearchParams`
  (React Router): enlaces compartibles y botón "atrás" funcional. La `queryKey`
  de TanStack Query deriva de los parámetros normalizados de la URL.
- **Resultados:** grid de `Card` (MUI) con título, precio formateado
  (`Intl.NumberFormat('es-BO', ...)`, p. ej. "Bs 1.234,00"), categoría, ciudad,
  condición. **Placeholder gris** donde irá la foto (2C). Cada card enlaza a
  `/avisos/:id`.
- **Paginación:** `MUI Pagination` usando `total` del `ResultadoPaginado`.
- **Estados:** carga (`CircularProgress`), vacío ("No se encontraron avisos"),
  error (`Alert`).

## 7. Detalle público — `DetalleAvisoPage`

- `obtenerAvisoPublico(id)`; muestra título, descripción completa, precio,
  categoría, ciudad, condición, fecha de creación; placeholder de foto.
- `404` → mensaje "Este aviso no está disponible" + enlace a Explorar (no se
  distingue inexistente/pausado/eliminado, coherente con 2B).
- Sin datos del vendedor ni "contactar" (Fase 3); sin botón de reportar (2D).

## 8. Publicar / Editar (dueño)

Formulario compartido **`FormAviso`** (evita duplicar el form entre crear y
editar): campos título (≤120), descripción (≤2000), monto (>0), categoría
(select), ciudad (select), condición (select/radio Nuevo/Usado). Moneda implícita
BOB. Validación cliente espejo de las reglas de dominio, más surface de errores
`400` (validation problem details) mapeados por campo cuando el backend los
devuelve.

### `CrearAvisoPage`

- Lee `verificado` del `AuthContext`. Si **no** está verificado: en lugar del
  formulario muestra un `Alert` con un llamado a verificar identidad y enlace a
  `/kyc`; no se puede enviar.
- Si está verificado: renderiza `FormAviso`. El `403` del backend se maneja igual
  como red de seguridad (mensaje + enlace a `/kyc`).
- Éxito → navega a `/mis-avisos`.

### `EditarAvisoPage`

- Precarga con `obtenerMiAviso(id)`; `403`/`404` → mensaje ("no encontrado o no es
  tuyo"). Rellena `FormAviso` con los valores actuales.
- Éxito → vuelve a `/mis-avisos`.

## 9. Mis avisos (dueño) — `MisAvisosPage`

- `listarMisAvisos(pagina, tamano)` paginado (excluye eliminados por contrato).
- Grid/tabla con título, precio, estado (Activo/Pausado), condición.
- Acciones por aviso según estado:
  - **Editar** → navega a `/mis-avisos/:id/editar`.
  - **Pausar** (si `Activo`) / **Reactivar** (si `Pausado`).
  - **Eliminar** con `Dialog` de confirmación (patrón de `AdminKycPage`).
- Mutaciones con `useMutation` + `invalidateQueries(['mis-avisos'])`.
- Enlace/CTA a `/publicar` cuando la lista está vacía.

## 10. Tests (Vitest + React Testing Library)

Patrón existente: `vi.spyOn` sobre los módulos de API (`../api/avisos`,
`../api/catalogo`), montaje con `QueryClientProvider` (`retry:false`) y
`MemoryRouter` donde la página use router/hooks de navegación.

- **Explorar:** render de resultados; estado vacío; aplicar un filtro cambia la
  `queryKey` y refetch; cambio de página; sincronización con `useSearchParams`.
- **Detalle:** render OK y rama `404`.
- **Crear:** gate KYC (no verificado → CTA a `/kyc` sin form; verificado → form);
  validación cliente (título/descr/monto inválidos); submit exitoso navega.
- **Editar:** precarga rellena el form; submit exitoso.
- **Mis avisos:** listado; pausar/reactivar refrescan; confirmación de eliminar.
- **Layout/AppBar:** enlaces condicionados por `estaAutenticado`.

## 11. Anti-PII (cliente)

Mismo criterio que el resto del frontend: nunca loguear tokens, ni volcar cuerpos
de error a consola. Los mensajes de error mostrados al usuario son genéricos
(derivan de `HttpError`, solo status + code no-PII). El catálogo no maneja PII
sensible.

## 12. Decisiones tomadas en el brainstorming

1. **Alcance:** completo — público (explorar + detalle) + dueño (crear/editar/
   pausar/reactivar/eliminar + mis avisos) + shell de navegación.
2. **Filtros públicos:** abrir `/api/catalogo/*` a `AllowAnonymous` (cambio
   backend mínimo) para poblar los dropdowns de categoría/ciudad.
3. **Shell y landing:** `/` = explorar; `AppLayout` con `AppBar` global y enlaces
   condicionados por sesión.
4. **Gate KYC:** proactivo en la UI (CTA a `/kyc` si no verificado) + manejo
   defensivo del `403`.
5. **Paginación:** clásica con `MUI Pagination` (usa `total`); orden fijo
   "recientes primero" (sin selector, según 2B).
6. **Filtros sincronizados a la URL** (`useSearchParams`) y **un `FormAviso`
   compartido** entre crear y editar.

## 13. Fuera de alcance de 2E

- Fotos / imágenes del aviso (2C).
- Moderación / reportar (2D).
- Vendedor y contactar (Fase 3 — Chat).
- Selector de orden y búsqueda avanzada.
- Gestión de categorías/ciudades por admin.
