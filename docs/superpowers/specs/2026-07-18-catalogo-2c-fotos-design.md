# Fase 2 — Bloque 2C: Fotos del aviso (backend + frontend)

**Fecha:** 2026-07-18
**Contexto:** Catalog (backend) + frontend `web/`
**Base:** bloques 2A, 2B y 2E mergeados en master (HEAD 62a0e33).

## 1. Objetivo

Que un aviso pueda tener imágenes. Hoy la UI muestra placeholders grises en
`ExplorarPage` y `DetalleAvisoPage`. Este bloque agrega el almacenamiento de
imágenes y las cablea de punta a punta: el dueño puede subir/borrar fotos al
crear o editar un aviso; el público las ve en el listado y el detalle.

## 2. Alcance

**Incluye:**

- Entidad `FotoAviso` en el agregado `Aviso` (Domain + EF + migración).
- Interfaz de almacenamiento de blobs de fotos (`IAlmacenFotosAviso`) y
  adaptador de disco (sin cifrado — las fotos de producto no son PII).
- Validación de imágenes: magic bytes + whitelist jpeg/png + límite 5 MiB
  (helper `ValidacionFotoAviso` en `Catalog.Application`).
- Endpoints backend:
  - `POST /api/avisos/{id}/fotos` — subir foto (dueño, `[Authorize]`).
  - `DELETE /api/avisos/{id}/fotos/{fotoId}` — borrar foto (dueño, `[Authorize]`).
  - `GET /api/fotos/{clave}` — servir el blob (anónimo).
- Actualizar DTOs públicos (`AvisoPublicoResumenDto`, `AvisoPublicoDto`) y del
  dueño (`AvisoResumenDto`, `AvisoDto`) para incluir la lista de fotos.
- UI frontend: componente de subida en `FormAviso` (crear + editar); render
  real en `ExplorarPage` (foto principal) y `DetalleAvisoPage` (foto principal
  + miniaturas).

**Difiere (no en este bloque):**

- Thumbnails / redimensionado automático.
- CDN o object storage en prod.
- Moderación de imágenes (2D).
- Reordenar fotos por drag-and-drop.
- Foto principal elegida por el usuario (la de menor `Orden` es la principal).

## 3. Decisiones de diseño

1. **Almacenamiento:** blobs simples en disco/volumen, sin cifrado. Nueva
   interfaz `IAlmacenFotosAviso` análoga a `IAlmacenBlobsKyc` pero sin
   `IEncryptor`. El adaptador de dev escribe a disco; en prod se sustituye por
   object storage sin tocar dominio ni casos de uso.
2. **No PII:** las fotos de producto no son PII. No se aplica `IEncryptor`
   ni `IAuditorAccesoPii`. Los logs registran solo `AvisoId`, `FotoId`, `clave`
   (opaca) y resultado, nunca contenido.
3. **Foto principal:** la foto con menor `Orden` es la principal. El `Orden`
   se asigna automáticamente al insertar (max existente + 1; la primera foto = 0).
   No se expone reordenar en este bloque.
4. **Límites:** máx. **5 fotos por aviso**, máx. **5 MiB por foto**, formatos
   jpeg/png, validación de magic bytes.
5. **Acceso al blob:** `GET /api/fotos/{clave}` es anónimo (el descubrimiento
   es anónimo; las fotos van en los DTOs públicos).
6. **Separación de endpoints:** subida y borrado bajo `/api/avisos/{id}/fotos`
   (dueño, autenticado). Servicio bajo `/api/fotos/{clave}` (público). Así el
   endpoint público no requiere Bearer y no colisiona con la autorización global.
7. **DTOs:** se agrega `fotos: FotoAvisoDto[]` a los cuatro DTOs existentes.
   `FotoAvisoDto` = `{ Id, Url, Orden }` (la `Url` es `/api/fotos/{clave}`,
   construida en la capa Application).

## 4. Dominio (`Catalog.Domain`)

### Entidad `FotoAviso`

```csharp
public sealed class FotoAviso
{
    // Ctor para EF Core.
    private FotoAviso() { Clave = null!; ContentType = null!; }

    internal FotoAviso(Guid avisoId, string clave, string contentType, int orden)
    {
        Id = Guid.NewGuid();
        AvisoId = avisoId;
        Clave = clave;
        ContentType = contentType;
        Orden = orden;
    }

    public Guid Id { get; private set; }
    public Guid AvisoId { get; private set; }
    /// <summary>Clave opaca que identifica el blob en el almacén.</summary>
    public string Clave { get; private set; }
    public string ContentType { get; private set; }
    /// <summary>Orden de presentación. La foto con menor Orden es la principal.</summary>
    public int Orden { get; private set; }
}
```

### Cambios en `Aviso`

```csharp
// Colección de fotos (máx. 5). EF accede por campo.
private readonly List<FotoAviso> _fotos = [];
public IReadOnlyList<FotoAviso> Fotos => _fotos.AsReadOnly();

public Result AgregarFoto(string clave, string contentType)
{
    if (_fotos.Count >= 5)
        return Result.Fallo(new Error(ErroresAviso.LimiteFotosAlcanzado,
            "Un aviso puede tener hasta 5 fotos."));
    var orden = _fotos.Count == 0 ? 0 : _fotos.Max(f => f.Orden) + 1;
    _fotos.Add(new FotoAviso(Id, clave, contentType, orden));
    Tocar(DateTime.UtcNow);
    return Result.Exito();
}

public Result<FotoAviso> QuitarFoto(Guid fotoId)
{
    var foto = _fotos.FirstOrDefault(f => f.Id == fotoId);
    if (foto is null)
        return Result<FotoAviso>.Fallo(new Error(ErroresAviso.FotoNoEncontrada,
            "La foto no pertenece a este aviso."));
    _fotos.Remove(foto);
    Tocar(DateTime.UtcNow);
    return Result<FotoAviso>.Exito(foto);
}
```

Errores nuevos en `ErroresAviso`:

```csharp
public const string LimiteFotosAlcanzado = "aviso.limite_fotos_alcanzado";
public const string FotoNoEncontrada      = "aviso.foto_no_encontrada";
```

## 5. Casos de uso (`Catalog.Application`)

### Puerto de almacenamiento

```csharp
namespace CaseritoApp.Catalog.Application.Fotos;

public interface IAlmacenFotosAviso
{
    Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct);
    Task<(byte[] Contenido, string ContentType)> ObtenerAsync(string clave, CancellationToken ct);
    Task EliminarAsync(string clave, CancellationToken ct);
}
```

### Validación de imagen

```csharp
public static class ValidacionFotoAviso
{
    public const long LimiteBytes = 5 * 1024 * 1024;
    public static readonly string[] ContentTypesPermitidos = ["image/jpeg", "image/png"];

    private static readonly byte[] _firmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] _firmaPng  = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool EsImagenValida(byte[] contenido, string contentType) { ... }
    // misma lógica de magic bytes que ValidacionImagenKyc
}
```

### DTO

```csharp
public sealed record FotoAvisoDto(Guid Id, string Url, int Orden);
```

### Actualización de DTOs existentes

| DTO | Cambio |
|---|---|
| `AvisoPublicoResumenDto` | + `IReadOnlyList<FotoAvisoDto> Fotos` |
| `AvisoPublicoDto` | + `IReadOnlyList<FotoAvisoDto> Fotos` |
| `AvisoResumenDto` | + `IReadOnlyList<FotoAvisoDto> Fotos` |
| `AvisoDto` | + `IReadOnlyList<FotoAvisoDto> Fotos` |

La URL se construye en los handlers/adaptadores: `$"/api/fotos/{foto.Clave}"`.

### Commands nuevos

**`SubirFotoAvisoCommand(Guid AvisoId, byte[] Contenido, string ContentType)`** → `Guid`

- Verifica dueño (`VendedorId == sub`); aviso no `Eliminado`.
- `ValidacionFotoAviso.EsImagenValida` → fallo = 400.
- `IAlmacenFotosAviso.GuardarAsync` → clave opaca.
- `aviso.AgregarFoto(clave, contentType)`.
- Persiste. Si falla tras guardar el blob: `EliminarAsync` best-effort.

**`BorrarFotoAvisoCommand(Guid AvisoId, Guid FotoId)`** → vacío

- Verifica dueño; aviso no `Eliminado`.
- `aviso.QuitarFoto(fotoId)` → devuelve `FotoAviso` con la clave.
- Persiste.
- `IAlmacenFotosAviso.EliminarAsync(clave)` best-effort (warning si falla).

### Queries actualizadas

`ListarMisAvisos`, `ObtenerMiAviso`, `BuscarAvisosQuery`, `ObtenerAvisoPublicoQuery`:
incluyen `Fotos` al proyectar, con join/include a `FotoAviso` ordenado `Orden ASC`.

## 6. Infraestructura (`Catalog.Infrastructure`)

### `AlmacenFotoAvisoDisco`

```csharp
public sealed class AlmacenFotoAvisoDisco(IOptions<OpcionesAlmacenFotos> opciones)
    : IAlmacenFotosAviso
{
    private readonly string _rutaBase = opciones.Value.RutaBase;

    public async Task<string> GuardarAsync(byte[] contenido, string contentType, CancellationToken ct)
    {
        Directory.CreateDirectory(_rutaBase);
        var clave = Guid.NewGuid().ToString("N");
        await File.WriteAllBytesAsync(RutaBlob(clave), contenido, ct);
        await File.WriteAllTextAsync(RutaMeta(clave), contentType, ct);
        return clave;
    }

    public async Task<(byte[], string)> ObtenerAsync(string clave, CancellationToken ct)
    {
        var contenido    = await File.ReadAllBytesAsync(RutaBlob(clave), ct);
        var contentType  = await File.ReadAllTextAsync(RutaMeta(clave), ct);
        return (contenido, contentType);
    }

    public Task EliminarAsync(string clave, CancellationToken ct)
    {
        File.Delete(RutaBlob(clave));
        File.Delete(RutaMeta(clave));
        return Task.CompletedTask;
    }

    private string RutaBlob(string clave) => Path.Combine(_rutaBase, $"{clave}.bin");
    private string RutaMeta(string clave) => Path.Combine(_rutaBase, $"{clave}.meta");
}

public sealed class OpcionesAlmacenFotos
{
    public string RutaBase { get; set; } = "/data/fotos-avisos";
}
```

Registrado en `DependencyInjection` de `Catalog.Infrastructure`.

### EF Core — `FotoAviso` (en `ConfiguracionCatalog`)

```csharp
// Dentro del builder.Entity<Aviso>:
e.HasMany(a => a.Fotos)
 .WithOne()
 .HasForeignKey(f => f.AvisoId)
 .OnDelete(DeleteBehavior.Cascade);
e.Navigation(a => a.Fotos).UsePropertyAccessMode(PropertyAccessMode.Field);

// Entidad separada:
builder.Entity<FotoAviso>(e =>
{
    e.ToTable("FotosAviso");
    e.HasKey(f => f.Id);
    e.Property(f => f.Id).ValueGeneratedNever();
    e.Property(f => f.AvisoId).IsRequired();
    e.Property(f => f.Clave).HasMaxLength(200).IsRequired();
    e.Property(f => f.ContentType).HasMaxLength(50).IsRequired();
    e.Property(f => f.Orden).IsRequired();
    e.HasIndex(f => f.AvisoId);
    e.HasIndex(f => new { f.AvisoId, f.Orden });
});
```

Migración: `AgregarFotosAviso`.

## 7. Endpoints + contrato OpenAPI (`Host`)

### `POST /api/avisos/{id}/fotos` (dueño)

```
Content-Type: multipart/form-data   Body: file (IFormFile)
201 { id: Guid } | 400 | 403 | 404
```

### `DELETE /api/avisos/{id}/fotos/{fotoId}` (dueño)

```
204 | 403 | 404
```

### `GET /api/fotos/{clave}` (anónimo)

Lee blob vía `IAlmacenFotosAviso.ObtenerAsync`, retorna `Results.File(contenido, contentType)`.
- Clave no encontrada → 404.
- **No entra al contrato OpenAPI tipado** (binario, no JSON). La URL se incluye
  como string en los DTOs.

## 8. Frontend (`web/`)

### `avisos.ts` — funciones nuevas

```typescript
export type FotoAvisoDto = { id: string; url: string; orden: number };

export async function subirFotoAviso(
  avisoId: string, archivo: File
): Promise<{ id: string }>

export async function borrarFotoAviso(
  avisoId: string, fotoId: string
): Promise<void>
```

Usan `fetch` con `FormData` (multipart); no pasan por el wrapper JSON de `http.ts`.

### `FormAviso` — sección de fotos

- **Props nuevas:** `avisoId?: string`, `fotosIniciales?: FotoAvisoDto[]`.
- Flujo **crear:** el submit del form crea el aviso → obtiene el `id` → sube
  fotos seleccionadas en secuencia → navega a `/mis-avisos`.
- Flujo **editar:** el `avisoId` ya existe; subida/borrado inmediatos con
  `useMutation` + `invalidateQueries(['mis-avisos'])`.
- Input `type="file"` multiple, `accept="image/jpeg,image/png"`.
- Previsualización con `URL.createObjectURL`.
- Miniaturas de fotos ya subidas con botón de borrar (editar).
- Validación client-side: máx. 5 fotos total, máx. 5 MiB/foto (antes de enviar).
- Botón "Guardar" deshabilitado mientras hay subidas en curso.

### `ExplorarPage` — foto principal

```tsx
{a.fotos.length > 0 ? (
  <CardMedia component="img" height={140} image={a.fotos[0].url} alt={a.titulo} />
) : (
  <Box sx={{ height: 140, bgcolor: 'grey.200' }} aria-hidden />
)}
```

### `DetalleAvisoPage` — galería

Foto principal grande + miniaturas clicables (estado local `selectedIdx`).
Fallback al placeholder si `fotos.length === 0`.

## 9. Tests

### Unit — dominio

- `AgregarFoto`: primer orden = 0; segunda = 1; sexta = error `LimiteFotosAlcanzado`.
- `QuitarFoto`: foto existente removida; foto inexistente = error `FotoNoEncontrada`.
- `ValidacionFotoAviso`: jpeg/png válidos; magic bytes incorrectos; tamaño excedido; content-type no permitido.

### Unit — handlers

- `SubirFoto`: no dueño → 403; aviso eliminado → 400; imagen inválida → 400;
  fallo al persistir borra blob best-effort.
- `BorrarFoto`: no dueño → 403; foto inexistente → 404.

### Integración (Testcontainers)

- `POST /api/avisos/{id}/fotos` → 201 con id de foto.
- Subir sexta foto → 400.
- Subir imagen con magic bytes incorrectos → 400.
- `DELETE /api/avisos/{id}/fotos/{fotoId}` → 204; foto ajena → 403/404.
- `GET /api/fotos/{clave}` → bytes + content-type correcto.
- `GET /api/fotos/inexistente` → 404.
- DTOs públicos y de dueño incluyen `fotos` ordenadas por `Orden`.
- Aviso sin fotos → `fotos: []`.

### Frontend — Vitest + RTL

- `FormAviso` (editar): miniaturas visibles; borrar invoca `borrarFotoAviso`;
  seleccionar 5+ fotos deshabilita el input.
- `ExplorarPage`: con fotos → `CardMedia`; sin fotos → placeholder gris.
- `DetalleAvisoPage`: con varias fotos → galería + miniaturas; clic cambia
  foto principal; sin fotos → placeholder.

## 10. Anti-PII

Fotos de producto no son PII. Logs: solo `AvisoId`, `FotoId`, clave opaca y
resultado. Sin `IEncryptor`, sin auditoría de acceso. El endpoint de servicio
retorna bytes directos.

## 11. Fuera de alcance de 2C

- Thumbnails / redimensionado.
- CDN / object storage en prod.
- Moderación de imágenes (2D).
- Reordenar fotos (drag-and-drop).
- Foto principal elegida por el usuario.
- Marca de agua / watermark.
