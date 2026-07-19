namespace CaseritoApp.Catalog.Domain.Avisos;

/// <summary>
/// Foto asociada a un aviso. Referencia opaca a un blob del almacén de fotos.
/// La foto con menor <see cref="Orden"/> es la principal.
/// </summary>
public sealed class FotoAviso
{
    // Constructor para EF Core.
#pragma warning disable S1144
    private FotoAviso()
    {
        Clave = null!;
        ContentType = null!;
    }
#pragma warning restore S1144

    internal FotoAviso(Guid avisoId, string clave, string contentType, int orden)
    {
        Id = Guid.NewGuid();
        AvisoId = avisoId;
        Clave = clave;
        ContentType = contentType;
        Orden = orden;
    }

    /// <summary>Identificador de la foto.</summary>
    public Guid Id { get; private set; }

    /// <summary>Id del aviso al que pertenece.</summary>
    public Guid AvisoId { get; private set; }

    /// <summary>Clave opaca que identifica el blob en el almacén de fotos.</summary>
    public string Clave { get; private set; }

    /// <summary>Tipo MIME del archivo (image/jpeg o image/png).</summary>
    public string ContentType { get; private set; }

    /// <summary>Orden de presentación. Menor valor = foto principal.</summary>
    public int Orden { get; private set; }
}
