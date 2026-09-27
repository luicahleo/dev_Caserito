# Distintivo de verificado — Plan de implementación

> **Para agentes ejecutores:** SUB-SKILL REQUERIDA: usar
> `superpowers:subagent-driven-development` (recomendado) o
> `superpowers:executing-plans` para implementar tarea por tarea. Los pasos usan
> casillas (`- [ ]`) para seguimiento.

**Spec:** `docs/superpowers/specs/2026-09-25-distintivo-verificado-design.md`

**Objetivo:** Mostrar el sello de identidad verificada en el listado de avisos,
el detalle de aviso y la conversación abierta, con un único componente
compartido.

**Arquitectura:** El listado se resuelve en el backend —Catalog aporta
`VendedorId`, el Host compone un DTO plano con `VendedorVerificado` usando una
consulta en lote contra Identity—. El detalle y la conversación consultan desde
el frontend el perfil público, que ya expone `verificado`.

**Stack:** .NET 10, Clean Architecture por bounded context, MediatR + Result,
xUnit + NSubstitute, Testcontainers.MsSql. React + TypeScript, MUI, TanStack
Query, Vitest. Backend desde `CaseritoApp/`, frontend desde `web/`, gate desde la
raíz.

## Restricciones globales

- Trabajar en la rama `develop`. No crear ramas. No hacer push.
- Textos de UI y comentarios en español, con acentos y UTF-8.
- **La fuente del dato es `EstaVerificadoAsync` (KYC aprobado), nunca
  `EstaHabilitadoParaMarketplaceAsync`**, que exime al rol `AdminPlataforma`.
- **Nunca se marca lo no verificado**: si el valor es `false` o la consulta
  falla, no se pinta nada.
- Antes de cada commit, desde la raíz: `./verify.ps1 -Changed`. Prohibido
  `--no-verify`. Si el gate falla, se arregla el código, nunca el gate.
- Docker debe estar corriendo para la Tarea 3 (integración con Testcontainers).
- Copy literal: `Usuario verificado` (chip y nombre accesible del icono).
- La solución compila con warnings-as-errors.

## Estructura de archivos

| Archivo | Responsabilidad |
|---|---|
| `Identity.Application/Kyc/IConsultaVerificacionKyc.cs` (modificar) | Declara la consulta en lote |
| `Identity.Infrastructure/Kyc/ConsultaVerificacionKycEfCore.cs` (modificar) | La implementa |
| `Catalog.Application/Avisos/DtosAvisoPublico.cs` (modificar) | `AvisoPublicoResumenDto` gana `VendedorId` |
| `Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs` (modificar) | Proyecta `VendedorId` |
| `Host/Endpoints/PublicoEndpoints.cs` (modificar) | DTO compuesto y marcado de la página |
| `web/src/api/schema.d.ts` (regenerar) | Contrato |
| `web/src/perfil/DistintivoVerificado.tsx` (crear) | El sello, con modo compacto |
| `web/src/routes/ExplorarPage.tsx` (modificar) | Icono en la tarjeta |
| `web/src/routes/DetalleAvisoPage.tsx` (modificar) | Chip tras consultar el perfil |
| `web/src/routes/ConversacionPage.tsx` (modificar) | Chip de la contraparte |
| `web/src/routes/PerfilPublicoPage.tsx` (modificar) | Migra su chip inline |

---

### Tarea 1: Consulta en lote de verificados

**Archivos:**
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/IConsultaVerificacionKyc.cs`
- Modificar: `CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConsultaVerificacionKycEfCore.cs`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/KycVerificadosEnLoteTests.cs` (crear)

**Interfaces:**
- Consume: nada.
- Produce: `Task<IReadOnlySet<Guid>> ObtenerVerificadosAsync(IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct)` en `IConsultaVerificacionKyc`.

**Por qué el test es de integración:** la implementación es una consulta EF Core
sobre `VerificacionesKyc`; probarla con un doble no probaría nada. Requiere
Docker.

- [ ] **Paso 1: Escribir el test que falla**

Crear `CaseritoApp/tests/CaseritoApp.IntegrationTests/KycVerificadosEnLoteTests.cs`:

```csharp
using CaseritoApp.Identity.Application.Kyc;
using CaseritoApp.Identity.Domain.Kyc;
using CaseritoApp.Identity.Infrastructure;
using CaseritoApp.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CaseritoApp.IntegrationTests;

/// <summary>
/// Consulta en lote usada por el listado público para marcar vendedores verificados
/// sin caer en N+1.
/// </summary>
public sealed class KycVerificadosEnLoteTests(CaseritoApiFactory factory)
    : IClassFixture<CaseritoApiFactory>
{
    private async Task<Guid> SembrarUsuarioConKycAsync(bool aprobado)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var usuarioId = Guid.NewGuid();
        var verificacion = VerificacionKyc.Crear(usuarioId);
        var envio = verificacion.EnviarSolicitud(
            "doc-lote", "selfie-lote", TipoDocumento.CedulaIdentidad, DateTimeOffset.UtcNow);
        if (aprobado)
        {
            verificacion.Aprobar(envio.Valor.Id, SistemaActor.Id, DateTimeOffset.UtcNow);
        }

        db.VerificacionesKyc.Add(verificacion);
        await db.SaveChangesAsync();
        return usuarioId;
    }

    [Fact]
    public async Task Devuelve_solo_los_usuarios_con_kyc_aprobado()
    {
        var aprobado1 = await SembrarUsuarioConKycAsync(aprobado: true);
        var aprobado2 = await SembrarUsuarioConKycAsync(aprobado: true);
        var pendiente = await SembrarUsuarioConKycAsync(aprobado: false);

        using var scope = factory.Services.CreateScope();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaVerificacionKyc>();

        var verificados = await consulta.ObtenerVerificadosAsync(
            [aprobado1, aprobado2, pendiente], CancellationToken.None);

        Assert.Contains(aprobado1, verificados);
        Assert.Contains(aprobado2, verificados);
        Assert.DoesNotContain(pendiente, verificados);
    }

    [Fact]
    public async Task Con_coleccion_vacia_devuelve_conjunto_vacio()
    {
        using var scope = factory.Services.CreateScope();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaVerificacionKyc>();

        var verificados = await consulta.ObtenerVerificadosAsync([], CancellationToken.None);

        Assert.Empty(verificados);
    }
}
```

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~KycVerificadosEnLoteTests"
```

Esperado: error de compilación — `ObtenerVerificadosAsync` no existe.

- [ ] **Paso 3: Implementación mínima**

En `IConsultaVerificacionKyc.cs`, dentro de la interfaz:

```csharp
    /// <summary>
    /// Usuarios con KYC aprobado dentro del conjunto dado, resueltos en una sola consulta.
    /// Lo usa el listado público para marcar vendedores verificados sin incurrir en N+1.
    /// </summary>
    public Task<IReadOnlySet<Guid>> ObtenerVerificadosAsync(
        IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct);
```

En `ConsultaVerificacionKycEfCore.cs`, dentro de la clase:

```csharp
    public async Task<IReadOnlySet<Guid>> ObtenerVerificadosAsync(
        IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct)
    {
        if (usuarioIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var verificados = await db.VerificacionesKyc
            .Where(v => usuarioIds.Contains(v.Id))
            .Where(v => v.Solicitudes.Any(s => s.Estado == EstadoKyc.Aprobada))
            .Select(v => v.Id)
            .ToListAsync(ct);

        return verificados.ToHashSet();
    }
```

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~KycVerificadosEnLoteTests"
```

Esperado: PASS, 2 tests.

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add CaseritoApp/src/Identity/CaseritoApp.Identity.Application/Kyc/IConsultaVerificacionKyc.cs CaseritoApp/src/Identity/CaseritoApp.Identity.Infrastructure/Kyc/ConsultaVerificacionKycEfCore.cs CaseritoApp/tests/CaseritoApp.IntegrationTests/KycVerificadosEnLoteTests.cs
git commit -m "feat(kyc): consulta en lote de usuarios verificados"
```

---

### Tarea 2: El listado de avisos expone el vendedor

**Archivos:**
- Modificar: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Application/Avisos/DtosAvisoPublico.cs:6-15`
- Modificar: `CaseritoApp/src/Catalog/CaseritoApp.Catalog.Infrastructure/Avisos/ConsultaAvisosPublicaEfCore.cs:63-75`
- Test: `CaseritoApp/tests/CaseritoApp.IntegrationTests/DescubrimientoAvisosTests.cs`

**Interfaces:**
- Consume: nada de tareas anteriores.
- Produce: `AvisoPublicoResumenDto` con `Guid VendedorId` como **segundo** parámetro posicional, inmediatamente después de `Id`, para reflejar el orden de `AvisoPublicoDto`.

**Verificado antes de escribir este plan:** ningún test de `CaseritoApp/tests`
construye `AvisoPublicoResumenDto` a mano (búsqueda de `AvisoPublicoResumenDto(`
sin resultados en tests). Si aparece alguno, añadirle el campo y seguir.

- [ ] **Paso 1: Escribir el test que falla**

En `DescubrimientoAvisosTests.cs`, que ya cubre `/api/publico/avisos` y trae los
helpers `UsuarioVerificadoAsync(HttpClient)` (línea 32), `LoguearAsync` (78) y
`CrearAvisoAsync` (85), añadir:

```csharp
    [Fact]
    public async Task El_listado_publico_expone_el_vendedor_del_aviso()
    {
        var cliente = factory.CreateClient();

        var respuesta = await cliente.GetFromJsonAsync<JsonElement>("/api/publico/avisos?pagina=1&tamano=20");

        var items = respuesta.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        foreach (var item in items)
        {
            Assert.True(
                item.TryGetProperty("vendedorId", out var vendedorId),
                "El resumen público debe exponer vendedorId.");
            Assert.NotEqual(Guid.Empty, vendedorId.GetGuid());
        }
    }
```

Sembrar antes un aviso activo con `UsuarioVerificadoAsync` + `CrearAvisoAsync`,
como hacen los tests vecinos: el listado debe devolver al menos un item para que
la aserción tenga valor. Añadir los `using` de `System.Text.Json` y
`System.Net.Http.Json` si faltan.

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~El_listado_publico_expone_el_vendedor"
```

Esperado: FAIL — la propiedad `vendedorId` no existe en el item.

- [ ] **Paso 3: Implementación mínima**

En `DtosAvisoPublico.cs`, el record queda:

```csharp
public sealed record AvisoPublicoResumenDto(
    Guid Id,
    Guid VendedorId,
    string Titulo,
    decimal Monto,
    string Moneda,
    string NombreCategoria,
    string NombreCiudad,
    string Condicion,
    DateTime FechaCreacion,
    IReadOnlyList<FotoAvisoDto> Fotos);
```

En `ConsultaAvisosPublicaEfCore.cs`, la proyección del listado añade
`a.VendedorId` justo después de `a.Id`:

```csharp
            .Select(a => new AvisoPublicoResumenDto(
                a.Id,
                a.VendedorId,
                a.Titulo,
                a.Precio.Monto,
                a.Precio.Moneda.ToString(),
                db.Categorias.Where(c => c.Id == a.CategoriaId).Select(c => c.Nombre).First(),
                db.Ciudades.Where(c => c.Id == a.CiudadId).Select(c => c.Nombre).First(),
                a.Condicion.ToString(),
                a.FechaCreacion,
                a.Fotos
                  .OrderBy(f => f.Orden)
                  .Select(f => new FotoAvisoDto(f.Id, $"/api/fotos/{f.Clave}", f.Orden))
                  .ToList()))
```

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~AvisosPublicos"
dotnet build
```

Esperado: PASS y build sin warnings.

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add CaseritoApp/src/Catalog CaseritoApp/tests/CaseritoApp.IntegrationTests
git commit -m "feat(avisos): expone el vendedor en el resumen publico"
```

---

### Tarea 3: El Host compone el distintivo en el listado

**Archivos:**
- Modificar: `CaseritoApp/src/Host/CaseritoApp.Host/Endpoints/PublicoEndpoints.cs`
- Test: el mismo archivo de integración de la Tarea 2

**Interfaces:**
- Consume: `IConsultaVerificacionKyc.ObtenerVerificadosAsync` (Tarea 1);
  `AvisoPublicoResumenDto` con `VendedorId` (Tarea 2).
- Produce: `AvisoPublicoResumenConVendedorDto` (plano) con su fábrica estática
  `Desde(AvisoPublicoResumenDto aviso, bool verificado)`; el endpoint
  `GET /api/publico/avisos` pasa a devolver
  `ResultadoPaginado<AvisoPublicoResumenConVendedorDto>`.

**Requisito:** Docker.

- [ ] **Paso 1: Escribir el test que falla**

Añadir al archivo de integración del listado público:

```csharp
    [Fact]
    public async Task El_listado_marca_verificado_al_vendedor_con_kyc_aprobado()
    {
        var cliente = factory.CreateClient();

        var respuesta = await cliente.GetFromJsonAsync<JsonElement>("/api/publico/avisos?pagina=1&tamano=20");

        var items = respuesta.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        foreach (var item in items)
        {
            Assert.True(
                item.TryGetProperty("vendedorVerificado", out var verificado),
                "El item del listado debe exponer vendedorVerificado.");
            Assert.True(verificado.ValueKind is JsonValueKind.True or JsonValueKind.False);
        }
    }
```

Y el test que protege la decisión de diseño más importante del bloque: un
administrador de plataforma **no** es un usuario verificado. Sembrar un aviso
cuyo vendedor tenga el rol `AdminPlataforma` y ningún KYC aprobado, usando los
helpers del archivo, y afirmar:

```csharp
    [Fact]
    public async Task Un_vendedor_admin_sin_kyc_no_aparece_como_verificado()
    {
        // El rol AdminPlataforma exime del gate de publicación
        // (EstaHabilitadoParaMarketplaceAsync), pero NO es una identidad verificada.
        // Si alguien cambia la fuente del distintivo por ese método, este test falla.
        var vendedorAdmin = await SembrarVendedorAdminConAvisoAsync();
        var cliente = factory.CreateClient();

        var respuesta = await cliente.GetFromJsonAsync<JsonElement>("/api/publico/avisos?pagina=1&tamano=20");

        var item = respuesta.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("vendedorId").GetGuid() == vendedorAdmin);
        Assert.False(item.GetProperty("vendedorVerificado").GetBoolean());
    }
```

Implementar `SembrarVendedorAdminConAvisoAsync()` en el propio archivo,
reutilizando los helpers de siembra de usuario y de aviso que ya tenga; debe
crear el usuario, asignarle el rol `AdminPlataforma` con `UserManager`, y crear
un aviso activo suyo. Devuelve el `Guid` del vendedor.

- [ ] **Paso 2: Ejecutar los tests y verificar que fallan**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~verificado"
```

Esperado: FAIL — la propiedad `vendedorVerificado` no existe.

- [ ] **Paso 3: Implementación mínima**

En `PublicoEndpoints.cs`:

```csharp
/// <summary>
/// Resumen público de un aviso con el sello de identidad de su vendedor. Es un DTO del Host
/// y no de Catalog: ese contexto no puede consultar Identity, así que no debe declarar un
/// campo que no puede calcular.
/// </summary>
public sealed record AvisoPublicoResumenConVendedorDto(
    Guid Id,
    Guid VendedorId,
    string Titulo,
    decimal Monto,
    string Moneda,
    string NombreCategoria,
    string NombreCiudad,
    string Condicion,
    DateTime FechaCreacion,
    IReadOnlyList<FotoAvisoDto> Fotos,
    bool VendedorVerificado)
{
    public static AvisoPublicoResumenConVendedorDto Desde(
        AvisoPublicoResumenDto aviso, bool verificado) =>
        new(aviso.Id, aviso.VendedorId, aviso.Titulo, aviso.Monto, aviso.Moneda,
            aviso.NombreCategoria, aviso.NombreCiudad, aviso.Condicion, aviso.FechaCreacion,
            aviso.Fotos, verificado);
}
```

Cambiar la declaración del endpoint:

```csharp
        grupo.MapGet("/", BuscarAsync)
            .Produces<ResultadoPaginado<AvisoPublicoResumenConVendedorDto>>(StatusCodes.Status200OK)
            .ProducesValidationProblem();
```

Y en `BuscarAsync`, añadir el parámetro `IConsultaVerificacionKyc consultaKyc`
junto a `ISender sender` y componer antes de devolver:

```csharp
            var resultado = await sender.Send(
                new BuscarAvisosQuery(q, categoriaId, ciudadId, precioMin, precioMax, condicion, pagina, tamano), ct);

            // Una sola consulta para toda la página: marcar item a item sería N+1.
            var verificados = await consultaKyc.ObtenerVerificadosAsync(
                resultado.Items.Select(a => a.VendedorId).Distinct().ToList(), ct);

            var items = resultado.Items
                .Select(a => AvisoPublicoResumenConVendedorDto.Desde(a, verificados.Contains(a.VendedorId)))
                .ToList();

            return Results.Ok(new ResultadoPaginado<AvisoPublicoResumenConVendedorDto>(
                items, resultado.Pagina, resultado.Tamano, resultado.Total));
```

Añadir el `using CaseritoApp.Identity.Application.Kyc;` al principio del archivo.
`ResultadoPaginado<T>` aquí es el de `Catalog.Application.Avisos`, que ya está
importado.

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `CaseritoApp/`:

```
dotnet test tests/CaseritoApp.IntegrationTests --filter "FullyQualifiedName~AvisosPublicos"
dotnet build
```

Esperado: PASS, incluidos los tres tests nuevos de las tareas 2 y 3.

- [ ] **Paso 5: Regenerar el contrato y commitear**

Los dos comandos exactos que ejecuta CI (`.github/workflows/ci.yml:82-93`). Desde
`CaseritoApp/`:

```
dotnet build src/Host/CaseritoApp.Host/CaseritoApp.Host.csproj -p:GenerateOpenApi=true
```

con `ASPNETCORE_ENVIRONMENT=Testing`. Después, desde `web/`:

```
npm run generate:api
```

CI valida con `git diff --exit-code` sobre
`CaseritoApp/artifacts/openapi/CaseritoApp.Host.json` y `web/src/api/schema.d.ts`,
así que **ambos archivos deben quedar commiteados**.

Comprobar que `web/src/api/schema.d.ts` contiene
`AvisoPublicoResumenConVendedorDto` con `vendedorId` y `vendedorVerificado`.

El frontend **aún no compila** en este punto: `PaginaAvisosPublicos` apunta a un
esquema que cambió de nombre. Se arregla en la Tarea 5; por eso este commit deja
el frontend en rojo a propósito y el `verify.ps1 -Changed` de este paso solo debe
cubrir el alcance de backend. **Si el gate marca el frontend en rojo, seguir a la
Tarea 5 antes de commitear, y hacer un único commit con ambas.**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add CaseritoApp/src/Host CaseritoApp/tests/CaseritoApp.IntegrationTests CaseritoApp/artifacts/openapi/CaseritoApp.Host.json web/src/api/schema.d.ts
git commit -m "feat(avisos): marca al vendedor verificado en el listado publico"
```

---

### Tarea 4: Componente del distintivo

**Archivos:**
- Crear: `web/src/perfil/DistintivoVerificado.tsx`
- Crear: `web/src/perfil/DistintivoVerificado.test.tsx`
- Modificar: `web/src/routes/PerfilPublicoPage.tsx:55`

**Interfaces:**
- Consume: nada.
- Produce: `DistintivoVerificado({ verificado, compacto }: { verificado: boolean; compacto?: boolean }): JSX.Element | null`.

- [ ] **Paso 1: Escribir el test que falla**

Crear `web/src/perfil/DistintivoVerificado.test.tsx`:

```tsx
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DistintivoVerificado } from './DistintivoVerificado';

describe('DistintivoVerificado', () => {
  it('muestra el sello con texto cuando el usuario está verificado', () => {
    render(<DistintivoVerificado verificado />);

    expect(screen.getByText('Usuario verificado')).toBeInTheDocument();
  });

  it('en modo compacto muestra solo el icono, con nombre accesible', () => {
    render(<DistintivoVerificado verificado compacto />);

    expect(screen.queryByText('Usuario verificado')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Usuario verificado')).toBeInTheDocument();
  });

  it('no muestra nada cuando el usuario no está verificado', () => {
    const { container } = render(<DistintivoVerificado verificado={false} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('tampoco muestra nada en modo compacto si no está verificado', () => {
    const { container } = render(<DistintivoVerificado verificado={false} compacto />);

    expect(container).toBeEmptyDOMElement();
  });
});
```

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `web/`: `npx vitest run src/perfil/DistintivoVerificado.test.tsx`

Esperado: FAIL — no se resuelve el módulo.

- [ ] **Paso 3: Implementación mínima**

Crear `web/src/perfil/DistintivoVerificado.tsx`:

```tsx
import VerifiedRoundedIcon from '@mui/icons-material/VerifiedRounded';
import { Chip, Tooltip } from '@mui/material';

/**
 * Sello de identidad verificada. Nunca marca lo contrario: si el usuario no está
 * verificado no se pinta nada, porque la ausencia del sello no es una acusación.
 */
export function DistintivoVerificado({
  verificado,
  compacto = false,
}: {
  verificado: boolean;
  compacto?: boolean;
}) {
  if (!verificado) return null;

  if (compacto) {
    return (
      <Tooltip title="Usuario verificado" enterTouchDelay={0}>
        <VerifiedRoundedIcon
          fontSize="small"
          aria-label="Usuario verificado"
          sx={{ color: 'success.main' }}
        />
      </Tooltip>
    );
  }

  return <Chip color="success" size="small" label="Usuario verificado" />;
}
```

En `web/src/routes/PerfilPublicoPage.tsx`, sustituir la línea 55:

```tsx
        {perfil.data.verificado && <Chip color="success" label="Usuario verificado" />}
```

por:

```tsx
        <DistintivoVerificado verificado={perfil.data.verificado} />
```

y añadir `import { DistintivoVerificado } from '../perfil/DistintivoVerificado';`.
Si `Chip` deja de usarse en el archivo, quitarlo de la importación de MUI para no
romper el lint.

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `web/`:
- `npx vitest run src/perfil/DistintivoVerificado.test.tsx` → PASS, 4 tests.
- `npx vitest run src/routes/PerfilPublicoPage.test.tsx` → PASS (el texto no cambió).
- `npm run lint` → sin errores.

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add web/src/perfil/DistintivoVerificado.tsx web/src/perfil/DistintivoVerificado.test.tsx web/src/routes/PerfilPublicoPage.tsx
git commit -m "feat(perfil): unifica el sello de usuario verificado"
```

---

### Tarea 5: El distintivo en la tarjeta del listado

**Archivos:**
- Modificar: `web/src/routes/ExplorarPage.tsx` (bloque `CardContent`, líneas 198-207)
- Test: `web/src/routes/ExplorarPage.test.tsx`

**Interfaces:**
- Consume: `DistintivoVerificado` (Tarea 4); el campo `vendedorVerificado` del
  item del listado (Tarea 3).
- Produce: nada.

**Contexto:** el fixture `resumen` del archivo de test (líneas 12-22) necesita
los dos campos nuevos; al ser el DTO plano, ninguno de los existentes se mueve.

- [ ] **Paso 1: Escribir el test que falla**

En `web/src/routes/ExplorarPage.test.tsx`, añadir los campos al fixture existente:

```tsx
const resumen: avisos.AvisoPublicoResumen = {
  id: 'a1',
  vendedorId: 'v1',
  titulo: 'Silla de madera',
  monto: 150,
  moneda: 'BOB',
  nombreCategoria: 'Muebles',
  nombreCiudad: 'La Paz',
  condicion: 'Usado',
  fechaCreacion: '2026-07-18T10:00:00Z',
  fotos: [],
  vendedorVerificado: true,
};
```

y añadir el test:

```tsx
  it('muestra el sello en la tarjeta de un vendedor verificado y no en otro', async () => {
    vi.spyOn(avisos, 'buscarAvisos').mockResolvedValue({
      items: [
        resumen,
        { ...resumen, id: 'a2', titulo: 'Mesa sin sello', vendedorVerificado: false },
      ],
      pagina: 1,
      tamano: 20,
      total: 2,
    });
    montar();

    await screen.findByText('Silla de madera');
    expect(screen.getAllByLabelText('Usuario verificado')).toHaveLength(1);
  });
```

- [ ] **Paso 2: Ejecutar el test y verificar que falla**

Desde `web/`: `npx vitest run src/routes/ExplorarPage.test.tsx`

Esperado: FAIL — no existe ningún elemento con ese nombre accesible. Si además
falla el typecheck por `PaginaAvisosPublicos`, es lo esperado tras la Tarea 3 y
se corrige en el paso siguiente.

- [ ] **Paso 3: Implementación mínima**

En `web/src/api/avisos.ts`, apuntar los tipos al esquema nuevo:

```ts
export type AvisoPublicoResumen = components['schemas']['AvisoPublicoResumenConVendedorDto'];
export type PaginaAvisosPublicos =
  components['schemas']['ResultadoPaginadoOfAvisoPublicoResumenConVendedorDto'];
```

Comprobar los nombres exactos en `web/src/api/schema.d.ts` tras la regeneración y
usar los que aparezcan ahí.

En `web/src/routes/ExplorarPage.tsx`, importar el componente:

```tsx
import { DistintivoVerificado } from '../perfil/DistintivoVerificado';
```

y dentro de `CardContent`, tras el título:

```tsx
                      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                        <Typography variant="subtitle1" color="primary">
                          {formatearBob(a.monto)}
                        </Typography>
                        <DistintivoVerificado verificado={a.vendedorVerificado} compacto />
                      </Stack>
```

sustituyendo el `<Typography variant="subtitle1" color="primary">` suelto que hay
hoy. `Stack` ya está importado en el archivo.

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `web/`:
- `npx vitest run src/routes/ExplorarPage.test.tsx` → PASS.
- `npm run typecheck` → sin errores.
- `npm run lint` → sin errores.

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add web/src/api/avisos.ts web/src/routes/ExplorarPage.tsx web/src/routes/ExplorarPage.test.tsx
git commit -m "feat(avisos): muestra el sello de verificado en el listado"
```

---

### Tarea 6: El distintivo en detalle de aviso y conversación

**Archivos:**
- Modificar: `web/src/routes/DetalleAvisoPage.tsx`
- Modificar: `web/src/routes/ConversacionPage.tsx`
- Test: `web/src/routes/DetalleAvisoPage.test.tsx` y `web/src/routes/ConversacionPage.test.tsx`

**Interfaces:**
- Consume: `DistintivoVerificado` (Tarea 4);
  `obtenerPerfilPublico(id: string): Promise<PerfilPublico>` de
  `web/src/api/reputation.ts:46`, cuyo resultado incluye `verificado: boolean`.
- Produce: nada.

- [ ] **Paso 1: Escribir los tests que fallan**

En `web/src/routes/DetalleAvisoPage.test.tsx`, añadir —importando el módulo del
perfil público como `import * as perfiles from '../api/reputation';`—:

```tsx
  it('muestra el sello cuando el vendedor está verificado', async () => {
    vi.spyOn(perfiles, 'obtenerPerfilPublico').mockResolvedValue({
      id: 'v1',
      nombreVisible: 'Vendedor',
      ciudadId: 'c1',
      nombreCiudad: 'La Paz',
      verificado: true,
      promedio: null,
      totalResenas: 0,
    } as never);

    montar();

    expect(await screen.findByText('Usuario verificado')).toBeInTheDocument();
  });

  it('no muestra sello si la consulta del perfil falla', async () => {
    vi.spyOn(perfiles, 'obtenerPerfilPublico').mockRejectedValue(new Error('caido'));

    montar();

    await screen.findByText('Mesa de madera');
    expect(screen.queryByText('Usuario verificado')).not.toBeInTheDocument();
  });
```

Ajustar el título esperado (`'Mesa de madera'`) al que use el fixture del
archivo, y reutilizar su función `montar()` y sus mocks de `obtenerAvisoPublico`.

En `web/src/routes/ConversacionPage.test.tsx`, añadir:

```tsx
  it('muestra el sello de la contraparte verificada', async () => {
    vi.spyOn(chat, 'buscarConversacionPropia').mockResolvedValue({
      id: 'c1', avisoId: 'a1', contraparteId: 'otra-persona', rol: 'Comprador',
      creadaEn: '', ultimaActividadEn: '', ultimaSecuencia: 1, noLeidos: 0,
      estado: 0, origenCierre: null, puedeEnviar: true,
    });
    vi.spyOn(chat, 'obtenerMensajes').mockResolvedValue({ siguienteCursor: null, items: [] });
    vi.spyOn(perfiles, 'obtenerPerfilPublico').mockResolvedValue({
      id: 'otra-persona',
      nombreVisible: 'Contraparte',
      ciudadId: 'c1',
      nombreCiudad: 'La Paz',
      verificado: true,
      promedio: null,
      totalResenas: 0,
    } as never);

    montar();

    expect(await screen.findByText('Usuario verificado')).toBeInTheDocument();
  });
```

añadiendo el import `import * as perfiles from '../api/reputation';`.

- [ ] **Paso 2: Ejecutar los tests y verificar que fallan**

Desde `web/`:

```
npx vitest run src/routes/DetalleAvisoPage.test.tsx src/routes/ConversacionPage.test.tsx
```

Esperado: FAIL en los tres tests nuevos — el texto no aparece.

- [ ] **Paso 3: Implementación mínima**

En `web/src/routes/DetalleAvisoPage.tsx`, junto a las demás consultas:

```tsx
  const perfilVendedor = useQuery({
    queryKey: ['perfil-publico', data?.vendedorId],
    queryFn: () => obtenerPerfilPublico(data!.vendedorId),
    enabled: Boolean(data?.vendedorId),
    retry: false,
  });
```

y junto al botón «Ver perfil del vendedor»:

```tsx
      <DistintivoVerificado verificado={perfilVendedor.data?.verificado ?? false} />
```

En `web/src/routes/ConversacionPage.tsx`:

```tsx
  const perfilContraparte = useQuery({
    queryKey: ['perfil-publico', conversacion?.contraparteId],
    queryFn: () => obtenerPerfilPublico(conversacion!.contraparteId),
    enabled: Boolean(conversacion?.contraparteId),
    retry: false,
  });
```

y en la cabecera de la conversación, junto al nombre de la contraparte:

```tsx
      <DistintivoVerificado verificado={perfilContraparte.data?.verificado ?? false} />
```

En ambos archivos, importar `DistintivoVerificado` y `obtenerPerfilPublico`.
`retry: false` y el `?? false` son los que garantizan que un fallo no pinte nada
ni reintente contra un endpoint con rate limit.

- [ ] **Paso 4: Ejecutar los tests y verificar que pasan**

Desde `web/`:
- `npx vitest run src/routes/DetalleAvisoPage.test.tsx src/routes/ConversacionPage.test.tsx` → PASS.
- `npm run test` → suite completa en verde.
- `npm run typecheck`, `npm run lint`, `npm run build` → sin errores.

- [ ] **Paso 5: Commit**

Desde la raíz:

```bash
./verify.ps1 -Changed
git add web/src/routes/DetalleAvisoPage.tsx web/src/routes/DetalleAvisoPage.test.tsx web/src/routes/ConversacionPage.tsx web/src/routes/ConversacionPage.test.tsx
git commit -m "feat(perfil): muestra el sello en el aviso y la conversacion"
```

---

## Cierre

- [ ] Backend desde `CaseritoApp/`: `dotnet test` y `dotnet format --verify-no-changes`.
      **Nota:** este último devuelve exit 2 de forma normal en este repo (4 `CA1506`
      y 1 `CA1505`, que son exactamente la baseline de
      `quality/complexity-baseline.json`). Solo es regresión si aparecen más.
- [ ] Frontend desde `web/`: `npm run test`, `npm run typecheck`, `npm run lint`,
      `npm run build`.
- [ ] Comprobar que `web/src/api/schema.d.ts` regenerado está commiteado y que el
      job `contract` no tendría nada que reprochar.
- [ ] Revisar el diff completo contra el spec: fuente del dato, ausencia de marca
      negativa, y que ninguna superficie use `EstaHabilitadoParaMarketplaceAsync`.
- [ ] `git diff --check` y árbol limpio.

**No hacer push.** `develop` se publica solo cuando el usuario lo pida.

## Cobertura de los criterios de aceptación

| Criterio del spec | Dónde se cubre |
|---|---|
| 1. Tarjeta con distintivo compacto accesible | Tarea 5 |
| 2. Tarjeta sin distintivo si no procede | Tarea 5 (segundo item del fixture) |
| 3. Chip en el detalle de aviso | Tarea 6 |
| 4. Chip en la conversación | Tarea 6 |
| 5. El perfil público conserva el sello | Tarea 4 |
| 6. Un admin sin KYC no aparece verificado | Tarea 3, `Un_vendedor_admin_sin_kyc_no_aparece_como_verificado` |
| 7. Un fallo de perfil no pinta ni rompe | Tarea 6, segundo test del detalle |
| 8. Una sola consulta por página | Tarea 1 (método en lote) y Tarea 3 (uso único en el endpoint) |
