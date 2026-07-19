# Catálogo 2D — Moderación de avisos — Plan de implementación

**Fecha:** 2026-07-19  
**Spec:** `docs/superpowers/specs/2026-07-19-catalogo-2d-moderacion-design.md`  
**Rama:** `feat/catalogo-2d-moderacion`

## Reglas globales

- Ejecutar backend desde `CaseritoApp/` y frontend desde `web/`.
- TDD estricto por tarea: test que falla, confirmar el fallo, implementación
  mínima, confirmar verde, autorrevisión y commit.
- No referenciar Identity desde Domain/Application/Infrastructure de Catalog.
  Las constantes de policy se usan exclusivamente en Host.
- No loguear texto del reporte, contenido del aviso, fotos, reportantes ni
  tokens. Los ids opacos de aviso/moderador sí pueden persistirse en auditoría.
- Versiones solo en `Directory.Packages.props`. Este bloque no añade paquetes.
- Todos los archivos se guardan en UTF-8 y los textos visibles están en español.

## Contratos compartidos del bloque

```csharp
public enum EstadoModeracionAviso { Visible, Oculto, EliminadoPorModeracion }
public enum MotivoReporteAviso
{
    EstafaOEngano, ProductoProhibido, ContenidoInapropiado, DuplicadoOSpam, Otro,
}
public enum EstadoReporteAviso { Pendiente, Atendido, Descartado }
public enum AccionModeracionAviso { Ocultar, Restaurar, Eliminar, DescartarReporte }

public sealed record ReportarAvisoCommand(
    Guid AvisoId, Guid ReportanteId, string Motivo, string? Detalle) : ICommand<Guid>;
public sealed record OcultarAvisoPorModeracionCommand(Guid AvisoId, Guid ModeradorId) : ICommand;
public sealed record RestaurarAvisoPorModeracionCommand(Guid AvisoId, Guid ModeradorId) : ICommand;
public sealed record EliminarAvisoPorModeracionCommand(Guid AvisoId, Guid ModeradorId) : ICommand;
public sealed record DescartarReporteAvisoCommand(Guid ReporteId, Guid ModeradorId) : ICommand;
```

```typescript
type MotivoReporte =
  | 'EstafaOEngano'
  | 'ProductoProhibido'
  | 'ContenidoInapropiado'
  | 'DuplicadoOSpam'
  | 'Otro';
```

---

## Tarea 1 — Dominio de moderación

**Produce:** máquina de estado de moderación, `ReporteAviso`,
`RegistroModeracion` y errores de dominio.

**Archivos exactos:**

- Crear `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/EstadoModeracionAviso.cs`.
- Crear `src/Catalog/CaseritoApp.Catalog.Domain/Moderacion/MotivoReporteAviso.cs`.
- Crear `src/Catalog/CaseritoApp.Catalog.Domain/Moderacion/EstadoReporteAviso.cs`.
- Crear `src/Catalog/CaseritoApp.Catalog.Domain/Moderacion/AccionModeracionAviso.cs`.
- Crear `src/Catalog/CaseritoApp.Catalog.Domain/Moderacion/ReporteAviso.cs`.
- Crear `src/Catalog/CaseritoApp.Catalog.Domain/Moderacion/RegistroModeracion.cs`.
- Modificar `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Aviso.cs`.
- Modificar `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/ErroresAviso.cs`.
- Crear `tests/CaseritoApp.UnitTests/Catalog/ModeracionDominioTests.cs`.

**Código de comportamiento:**

```csharp
public Result OcultarPorModeracion(DateTime ahoraUtc)
{
    if (EstadoModeracion != EstadoModeracionAviso.Visible)
        return FalloModeracion("Solo se puede ocultar un aviso visible.");
    EstadoModeracion = EstadoModeracionAviso.Oculto;
    Tocar(ahoraUtc);
    return Result.Exito();
}

public Result RestaurarPorModeracion(DateTime ahoraUtc)
{
    if (EstadoModeracion != EstadoModeracionAviso.Oculto)
        return FalloModeracion("Solo se puede restaurar un aviso oculto.");
    EstadoModeracion = EstadoModeracionAviso.Visible;
    Tocar(ahoraUtc);
    return Result.Exito();
}

public Result EliminarPorModeracion(DateTime ahoraUtc)
{
    if (EstadoModeracion == EstadoModeracionAviso.EliminadoPorModeracion)
        return FalloModeracion("El aviso ya fue eliminado por moderación.");
    EstadoModeracion = EstadoModeracionAviso.EliminadoPorModeracion;
    Tocar(ahoraUtc);
    return Result.Exito();
}
```

`ReporteAviso.Crear` asigna ids/fecha y estado pendiente. `Atender` y
`Descartar` validan estado pendiente, asignan resolución y no aceptan una
segunda resolución. `RegistroModeracion.Crear` es la única factory pública.

**Prueba roja:** compilar los tests antes de crear los tipos; debe fallar por
tipos inexistentes.

**Verificación:**

```powershell
dotnet test CaseritoApp.sln --filter "FullyQualifiedName~ModeracionDominioTests"
```

Esperado: tests de estado inicial, ocultar/restaurar/eliminar, terminalidad,
atender/descartar y doble resolución en verde.

**Commit:** `feat(catalog): modela reportes y estado de moderacion`

---

## Tarea 2 — Reglas del dueño ante moderación

**Consume:** estado de moderación de Tarea 1.  
**Produce:** todas las mutaciones del dueño bloquean el estado terminal; eliminar
atiende reportes pendientes mediante el caso de uso.

**Archivos exactos:**

- Modificar `src/Catalog/CaseritoApp.Catalog.Domain/Avisos/Aviso.cs`.
- Modificar tests `tests/CaseritoApp.UnitTests/Catalog/AvisoTests.cs` y
  `AvisoFotosTests.cs`.
- Modificar handlers `EditarAvisoCommand.cs`, `PausarAvisoCommand.cs`,
  `ReactivarAvisoCommand.cs`, `EliminarAvisoCommand.cs`,
  `SubirFotoAvisoCommand.cs`, `BorrarFotoAvisoCommand.cs`.
- Crear o ampliar tests de handlers en `tests/CaseritoApp.UnitTests/Catalog/`.

**Regla completa:** `Editar`, `Pausar`, `Reactivar`, `Eliminar`, `AgregarFoto` y
`QuitarFoto` devuelven `ErroresAviso.EliminadoPorModeracion` si el estado de
moderación es terminal. `Oculto` no bloquea ninguna de estas operaciones.

**Verificación:**

```powershell
dotnet test CaseritoApp.sln --filter "FullyQualifiedName~AvisoTests|FullyQualifiedName~AvisoFotosTests|FullyQualifiedName~TransicionesAvisoHandlerTests|FullyQualifiedName~SubirBorrarFotoCommandHandlerTests"
```

Esperado: verde, incluidos oculto editable y eliminado por moderación bloqueado.

**Commit:** `feat(catalog): aplica moderacion a operaciones del dueno`

---

## Tarea 3 — Persistencia y migración

**Produce:** tablas, columna, índices, repositorios y proyecciones EF.

**Archivos exactos:**

- Modificar `CatalogDbContext.cs` para añadir `DbSet<ReporteAviso>` y
  `DbSet<RegistroModeracion>`.
- Modificar `Avisos/ConfiguracionCatalog.cs`.
- Crear `Moderacion/IRepositorioReportesAviso.cs` en Application.
- Crear `Moderacion/IRepositorioRegistrosModeracion.cs` en Application.
- Crear `Moderacion/RepositorioReportesAvisoEfCore.cs` en Infrastructure.
- Crear `Moderacion/RepositorioRegistrosModeracionEfCore.cs` en Infrastructure.
- Modificar `DependencyInjection.cs`.
- Generar migración `AgregarModeracionAvisos`.

**Configuración obligatoria:**

```csharp
e.Property(a => a.EstadoModeracion)
 .HasConversion<string>().HasMaxLength(30).IsRequired().IsConcurrencyToken();

e.HasIndex(r => new { r.AvisoId, r.ReportanteId })
 .IsUnique()
 .HasFilter("[Estado] = N'Pendiente'");
e.Property(r => r.Estado)
 .HasConversion<string>().HasMaxLength(20).IsRequired().IsConcurrencyToken();
```

`ReporteAviso` tiene FK interna con `Aviso` y `DeleteBehavior.Restrict`.
`RegistroModeracion` no tiene navegación mutable y nunca se actualiza.

**Verificación:** `dotnet build CaseritoApp.sln`; inspeccionar migración y
snapshot; `git diff --check`.

**Commit:** `feat(catalog): persiste reportes y auditoria de moderacion`

---

## Tarea 4 — Reportar un aviso

**Produce:** command/validator/handler y endpoint comunitario.

**Archivos exactos:**

- Crear `Application/Moderacion/ReportarAvisoCommand.cs`.
- Crear `tests/CaseritoApp.UnitTests/Catalog/ReportarAvisoCommandTests.cs`.
- Crear o modificar `Host/Endpoints/ModeracionEndpoints.cs`.

**Flujo del handler:**

1. Validar ids, motivo parseable y detalle de máximo 500.
2. Cargar aviso con `IRepositorioAvisos.ObtenerAsync`.
3. Devolver `NoEncontrado` salvo `Activo + Visible`.
4. Rechazar `VendedorId == ReportanteId`.
5. Consultar pendiente existente y devolver error duplicado.
6. Crear/agregar `ReporteAviso` y devolver id.
7. Traducir carrera del índice único a `409` en Host sin exponer datos.

**Endpoint completo:** `POST /api/avisos/{id}/reportes`,
`.RequireAuthorization()`, `.Accepts<ReportarAvisoRequest>()`,
`.Produces<ReporteCreadoResponse>(201)`, 400/401/404/409.

**Verificación:** tests unitarios y build verde.

**Commit:** `feat(catalog): permite reportar avisos visibles`

---

## Tarea 5 — Cola y detalle de moderación

**Produce:** DTOs, consultas agrupadas y endpoints de lectura.

**Archivos exactos:**

- Crear `Application/Moderacion/DtosModeracion.cs`.
- Crear `ListarAvisosReportadosQuery.cs`.
- Crear `ObtenerAvisoReportadoQuery.cs`.
- Ampliar `IRepositorioReportesAviso` y su adaptador EF.
- Crear tests unitarios de validators/handlers.
- Ampliar `ModeracionEndpoints.cs`.

**DTOs:**

```csharp
public sealed record AvisoReportadoResumenDto(
    Guid AvisoId, string Titulo, string EstadoAviso, string EstadoModeracion,
    int CantidadReportes, IReadOnlyList<string> Motivos, DateTime ReporteMasAntiguo);
public sealed record ReporteAvisoDto(
    Guid Id, string Motivo, string? Detalle, string Estado,
    DateTime FechaCreacion, DateTime? FechaResolucion);
public sealed record AvisoReportadoDto(
    Guid AvisoId, string Titulo, string Descripcion, string EstadoAviso,
    string EstadoModeracion, IReadOnlyList<FotoAvisoDto> Fotos,
    IReadOnlyList<ReporteAvisoDto> Reportes);
```

Paginación: `pagina >= 1`, `tamano 1..50`, estado parseable. Orden por reporte
más antiguo ascendente y `AvisoId` como desempate.

**Endpoints:** GET `/api/admin/moderacion/avisos` y GET
`/api/admin/moderacion/avisos/{id}`, ambos bajo policy
`publicaciones.moderar`.

**Commit:** `feat(catalog): agrega cola agrupada de moderacion`

---

## Tarea 6 — Acciones, auditoría y resolución automática

**Produce:** cuatro comandos administrativos y cierre automático al eliminar el
dueño.

**Archivos exactos:**

- Crear `OcultarAvisoPorModeracionCommand.cs`.
- Crear `RestaurarAvisoPorModeracionCommand.cs`.
- Crear `EliminarAvisoPorModeracionCommand.cs`.
- Crear `DescartarReporteAvisoCommand.cs`.
- Crear `tests/CaseritoApp.UnitTests/Catalog/AccionesModeracionCommandTests.cs`.
- Modificar `EliminarAvisoCommand.cs` y sus tests.
- Ampliar endpoints administrativos.

**Reglas:** ocultar/eliminar cargan el aviso y reportes pendientes, ejecutan la
transición, atienden todos con el moderador y agregan un registro. Restaurar solo
transiciona y audita. Descartar carga un reporte pendiente, lo descarta y audita
con su `ReporteId`. Eliminar por el dueño atiende pendientes con moderador nulo y
no crea registro de moderación.

El Host captura `ConflictoConcurrenciaException` y responde `409` genérico.

**Commit:** `feat(catalog): implementa decisiones y auditoria de moderacion`

---

## Tarea 7 — Visibilidad pública y fotos

**Produce:** una única regla pública `Activo + Visible` para búsqueda, detalle y
bytes de fotos.

**Archivos exactos:**

- Modificar `ConsultaAvisosPublicaEfCore.cs`.
- Crear `Application/Fotos/ObtenerFotoPublicaQuery.cs`.
- Añadir búsqueda de foto/aviso al puerto apropiado y adaptador EF.
- Modificar `Host/Endpoints/FotosEndpoints.cs`.
- Ampliar unit/integration tests.

`ObtenerFotoPublicaQuery` consulta metadatos y visibilidad antes de llamar a
`IAlmacenFotosAviso.ObtenerAsync`; cualquier estado no público o blob inexistente
produce `null`, mapeado a `404`.

**Commit:** `fix(catalog): bloquea superficies publicas de avisos moderados`

---

## Tarea 8 — Integración backend y contrato

**Archivos exactos:**

- Crear `tests/CaseritoApp.IntegrationTests/ModeracionAvisosTests.cs`.
- Modificar `Program.cs` para mapear endpoints si es necesario.
- Regenerar `artifacts/openapi/CaseritoApp.Host.json`.
- Regenerar `web/src/api/schema.d.ts`.

**Escenarios:** 401 reporte, 400 autorreporte, 409 duplicado, 403 cola sin
permiso, moderador autorizado, filtros, ocultar, fotos/detalle/listado 404,
restaurar, eliminar terminal, acciones del dueño bloqueadas y cierre automático.

**Comandos:**

```powershell
dotnet test CaseritoApp.sln --filter "FullyQualifiedName~ModeracionAvisosTests"
$env:ASPNETCORE_ENVIRONMENT='Testing'
dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -p:GenerateOpenApi=true
# desde web/
npm run generate:api
npm run typecheck
```

Esperado: integración verde, contrato con siete rutas nuevas y schema sin
deriva.

**Commit:** `test(catalog): integra moderacion y actualiza contrato`

---

## Tarea 9 — UI de reporte y estado del dueño

**Archivos exactos:**

- Modificar `web/src/api/avisos.ts` y sus tests.
- Modificar `DetalleAvisoPage.tsx` y sus tests.
- Modificar `LoginPage.tsx` y sus tests para retorno interno seguro.
- Modificar DTO/render de `MisAvisosPage.tsx` y tests.

El diálogo usa select de los cinco motivos, detalle multiline de 500 caracteres,
éxito visible y errores por status. Login recibe `location.state.from` y solo
navega a rutas internas que comiencen en `/` y no en `//`; fallback `/perfil`.

**Verificación:** `npm run typecheck`, `npm run lint`, tests dirigidos.

**Commit:** `feat(web): agrega reporte y estado de moderacion del dueno`

---

## Tarea 10 — UI administrativa y rutas

**Archivos exactos:**

- Crear `web/src/api/moderacion.ts` y tests.
- Crear `web/src/routes/AdminModeracionPage.tsx` y tests.
- Modificar `web/src/app/router.tsx`.
- Modificar `web/src/app/AppLayout.tsx` y tests.

La página usa TanStack Query, filtro de estado, tabla agrupada y diálogo de
revisión. Las mutaciones invalidan `['admin', 'moderacion']`. Ruta protegida con
`RequierePermiso('publicaciones.moderar')`; enlace condicionado por
`tienePermiso`.

**Verificación:** typecheck, lint, tests y build.

**Commit:** `feat(web): agrega cola y acciones de moderacion`

---

## Cierre

```powershell
# CaseritoApp/
dotnet build CaseritoApp.sln
dotnet test CaseritoApp.sln
dotnet format CaseritoApp.sln --verify-no-changes

# web/
npm run typecheck
npm run lint
npm run test
npm run build

# raíz
git diff master...HEAD --check
git status --short --branch
```

Revisar todo el diff contra el spec: aislamiento, policy por permiso, no PII,
máquina de estados, consistencia OpenAPI y ausencia de trabajo extra. No hacer
merge ni push; presentar resultados y esperar autorización explícita para
`git merge --no-ff feat/catalogo-2d-moderacion` en `master`.
