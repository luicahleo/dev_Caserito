using CaseritoApp.BuildingBlocks.Domain;

namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>
/// Raíz de agregado de un aviso del marketplace. Encapsula la máquina de estados
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
        EstadoModeracion = EstadoModeracionAviso.Visible;
        FechaCreacion = ahoraUtc;
        FechaActualizacion = ahoraUtc;
    }

    /// <summary>Id opaco del dueño (claim <c>sub</c>); sin FK cruzada a Identity.</summary>
    public Guid VendedorId { get; private set; }

    /// <summary>Título del aviso.</summary>
    public string Titulo { get; private set; }

    /// <summary>Descripción del aviso.</summary>
    public string Descripcion { get; private set; }

    /// <summary>Precio del aviso.</summary>
    public Dinero Precio { get; private set; }

    /// <summary>Categoría (referencia al catálogo sembrado).</summary>
    public Guid CategoriaId { get; private set; }

    /// <summary>Ciudad (referencia al catálogo sembrado).</summary>
    public Guid CiudadId { get; private set; }

    /// <summary>Condición del artículo.</summary>
    public CondicionArticulo Condicion { get; private set; }

    /// <summary>Estado actual del aviso.</summary>
    public EstadoAviso Estado { get; private set; }

    /// <summary>Restricción aplicada por moderación, independiente del estado decidido por el dueño.</summary>
    public EstadoModeracionAviso EstadoModeracion { get; private set; }

    /// <summary>Fecha de creación (UTC).</summary>
    public DateTime FechaCreacion { get; private set; }

    /// <summary>Fecha de última modificación (UTC).</summary>
    public DateTime FechaActualizacion { get; private set; }

    // Colección de fotos (máx. 5). EF accede por el campo privado.
    private readonly List<FotoAviso> _fotos = [];

    /// <summary>Fotos del aviso ordenadas por <see cref="FotoAviso.Orden"/> ASC.</summary>
    public IReadOnlyList<FotoAviso> Fotos => _fotos.AsReadOnly();

    /// <summary>Crea un aviso nuevo en estado <see cref="EstadoAviso.Activo"/> y emite <see cref="AvisoPublicado"/>.</summary>
    public static Aviso Crear(
        Guid vendedorId, string titulo, string descripcion, Dinero precio,
        Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc)
    {
        var aviso = new Aviso(vendedorId, titulo, descripcion, precio, categoriaId, ciudadId, condicion, ahoraUtc);
        aviso.AgregarEvento(new AvisoPublicado(aviso.Id, vendedorId));
        return aviso;
    }

    /// <summary>Edita los datos del aviso. Prohibido si está eliminado.</summary>
    public Result Editar(
        string titulo, string descripcion, Dinero precio,
        Guid categoriaId, Guid ciudadId, CondicionArticulo condicion, DateTime ahoraUtc)
    {
        if (EstaEliminadoPorModeracion())
        {
            return FalloEliminadoPorModeracion();
        }

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
        if (EstaEliminadoPorModeracion())
        {
            return FalloEliminadoPorModeracion();
        }

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
        if (EstaEliminadoPorModeracion())
        {
            return FalloEliminadoPorModeracion();
        }

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
        if (EstaEliminadoPorModeracion())
        {
            return FalloEliminadoPorModeracion();
        }

        if (Estado == EstadoAviso.Eliminado)
        {
            return Result.Fallo(new Error(ErroresAviso.TransicionInvalida, "El aviso ya está eliminado."));
        }

        Estado = EstadoAviso.Eliminado;
        Tocar(ahoraUtc);
        AgregarEvento(new AvisoEliminado(Id));
        return Result.Exito();
    }

    /// <summary>Oculta temporalmente el aviso de las superficies públicas.</summary>
    public Result OcultarPorModeracion(DateTime ahoraUtc)
    {
        if (EstadoModeracion != EstadoModeracionAviso.Visible)
        {
            return FalloModeracion("Solo se puede ocultar un aviso visible.");
        }

        EstadoModeracion = EstadoModeracionAviso.Oculto;
        Tocar(ahoraUtc);
        return Result.Exito();
    }

    /// <summary>Restaura un aviso previamente ocultado por moderación.</summary>
    public Result RestaurarPorModeracion(DateTime ahoraUtc)
    {
        if (EstadoModeracion != EstadoModeracionAviso.Oculto)
        {
            return FalloModeracion("Solo se puede restaurar un aviso oculto.");
        }

        EstadoModeracion = EstadoModeracionAviso.Visible;
        Tocar(ahoraUtc);
        return Result.Exito();
    }

    /// <summary>Elimina el aviso de forma terminal por moderación.</summary>
    public Result EliminarPorModeracion(DateTime ahoraUtc)
    {
        if (EstadoModeracion == EstadoModeracionAviso.EliminadoPorModeracion)
        {
            return FalloModeracion("El aviso ya fue eliminado por moderación.");
        }

        EstadoModeracion = EstadoModeracionAviso.EliminadoPorModeracion;
        Tocar(ahoraUtc);
        return Result.Exito();
    }

    /// <summary>Agrega una foto al aviso. Máximo 5 fotos por aviso.</summary>
    public Result AgregarFoto(string clave, string contentType)
    {
        if (EstaEliminadoPorModeracion())
        {
            return FalloEliminadoPorModeracion();
        }

        if (_fotos.Count >= 5)
        {
            return Result.Fallo(new Error(
                ErroresAviso.LimiteFotosAlcanzado,
                "Un aviso puede tener hasta 5 fotos."));
        }

        var orden = _fotos.Count == 0 ? 0 : _fotos.Max(f => f.Orden) + 1;
        _fotos.Add(new FotoAviso(Id, clave, contentType, orden));
        Tocar(DateTime.UtcNow);
        return Result.Exito();
    }

    /// <summary>
    /// Quita una foto del aviso y la devuelve para que el llamante pueda borrar el blob.
    /// </summary>
    public Result<FotoAviso> QuitarFoto(Guid fotoId)
    {
        if (EstaEliminadoPorModeracion())
        {
            return Result.Fallo<FotoAviso>(new Error(
                ErroresAviso.EliminadoPorModeracion,
                "El aviso fue eliminado por moderación."));
        }

        var foto = _fotos.FirstOrDefault(f => f.Id == fotoId);
        if (foto is null)
        {
            return Result.Fallo<FotoAviso>(new Error(
                ErroresAviso.FotoNoEncontrada,
                "La foto no pertenece a este aviso."));
        }

        _fotos.Remove(foto);
        Tocar(DateTime.UtcNow);
        return Result.Exito(foto);
    }

    private void Tocar(DateTime ahoraUtc) => FechaActualizacion = ahoraUtc;

    private static Result FalloModeracion(string mensaje) =>
        Result.Fallo(new Error(ErroresAviso.TransicionModeracionInvalida, mensaje));

    private bool EstaEliminadoPorModeracion() =>
        EstadoModeracion == EstadoModeracionAviso.EliminadoPorModeracion;

    private static Result FalloEliminadoPorModeracion() =>
        Result.Fallo(new Error(
            ErroresAviso.EliminadoPorModeracion,
            "El aviso fue eliminado por moderación."));
}
