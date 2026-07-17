using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>
/// RaÃ­z de agregado de un aviso del marketplace. Encapsula la mÃ¡quina de estados
/// (<see cref="EstadoAviso.Activo"/> â‡„ <see cref="EstadoAviso.Pausado"/>, y
/// <see cref="EstadoAviso.Eliminado"/> como estado terminal por soft-delete).
/// </summary>
public sealed class Aviso : AggregateRoot
{
    // Constructor para EF Core.
    private Aviso()
    {
        Titulo = null!;
        Descripcion = null!;
        Precio = null!;
    }

    private Aviso(
        Guid vendedorId, string titulo, string descripcion, Dinero precio,
        Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc)
    {
        VendedorId = vendedorId;
        Titulo = titulo;
        Descripcion = descripcion;
        Precio = precio;
        CategoriaId = categoriaId;
        CiudadId = ciudadId;
        Condicion = condicion;
        Estado = EstadoAviso.Activo;
        FechaCreacion = ahoraUtc;
        FechaActualizacion = ahoraUtc;
    }

    /// <summary>Id opaco del dueÃ±o (claim <c>sub</c>); sin FK cruzada a Identity.</summary>
    public Guid VendedorId { get; private set; }

    /// <summary>TÃ­tulo del aviso.</summary>
    public string Titulo { get; private set; }

    /// <summary>DescripciÃ³n del aviso.</summary>
    public string Descripcion { get; private set; }

    /// <summary>Precio del aviso.</summary>
    public Dinero Precio { get; private set; }

    /// <summary>CategorÃ­a (referencia al catÃ¡logo sembrado).</summary>
    public Guid CategoriaId { get; private set; }

    /// <summary>Ciudad (referencia al catÃ¡logo sembrado).</summary>
    public Guid CiudadId { get; private set; }

    /// <summary>CondiciÃ³n del artÃ­culo.</summary>
    public CondicionArticulo Condicion { get; private set; }

    /// <summary>Estado actual del aviso.</summary>
    public EstadoAviso Estado { get; private set; }

    /// <summary>Fecha de creaciÃ³n (UTC).</summary>
    public DateTime FechaCreacion { get; private set; }

    /// <summary>Fecha de Ãºltima modificaciÃ³n (UTC).</summary>
    public DateTime FechaActualizacion { get; private set; }

    /// <summary>Crea un aviso nuevo en estado <see cref="EstadoAviso.Activo"/> y emite <see cref="AvisoPublicado"/>.</summary>
    public static Aviso Crear(
        Guid vendedorId, string titulo, string descripcion, Dinero precio,
        Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc)
    {
        var aviso = new Aviso(vendedorId, titulo, descripcion, precio, categoriaId, ciudadId, condicion, ahoraUtc);
        aviso.AgregarEvento(new AvisoPublicado(aviso.Id, vendedorId));
        return aviso;
    }

    /// <summary>Edita los datos del aviso. Prohibido si estÃ¡ eliminado.</summary>
    public Result Editar(
        string titulo, string descripcion, Dinero precio,
        Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc)
    {
        if (Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.TransicionInvalida, "No se puede editar un aviso eliminado."));
        }

        Titulo = titulo;
        Descripcion = descripcion;
        Precio = precio;
        CategoriaId = categoriaId;
        CiudadId = ciudadId;
        Condicion = condicion;
        Tocar(ahoraUtc);
        AgregarEvento(new AvisoEditado(Id));
        return Result.Exito();
    }

    /// <summary>Pausa un aviso activo.</summary>
    public Result Pausar(DateTime ahoraUtc)
    {
        if (Estado != EstadoAviso.Activo)
        {
            return Result.Fallo(new Error(ErroresAviso.TransicionInvalida, "Solo se puede pausar un aviso activo."));
        }

        Estado = EstadoAviso.Pausado;
        Tocar(ahoraUtc);
        AgregarEvento(new AvisoPausado(Id));
        return Result.Exito();
    }

    /// <summary>Reactiva un aviso pausado.</summary>
    public Result Reactivar(DateTime ahoraUtc)
    {
        if (Estado != EstadoAviso.Pausado)
        {
            return Result.Fallo(new Error(ErroresAviso.TransicionInvalida, "Solo se puede reactivar un aviso pausado."));
        }

        Estado = EstadoAviso.Activo;
        Tocar(ahoraUtc);
        AgregarEvento(new AvisoReactivado(Id));
        return Result.Exito();
    }

    /// <summary>Elimina (soft-delete) el aviso. No se puede eliminar dos veces.</summary>
    public Result Eliminar(DateTime ahoraUtc)
    {
        if (Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.TransicionInvalida, "El aviso ya estÃ¡ eliminado."));
        }

        Estado = EstadoAviso.Eliminado;
        Tocar(ahoraUtc);
        AgregarEvento(new AvisoEliminado(Id));
        return Result.Exito();
    }

    private void Tocar(DateTime ahoraUtc) => FechaActualizacion = ahoraUtc;
}
