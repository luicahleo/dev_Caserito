namespace CaseritoApp.SmokeLib.Domain;

/// <summary>Entidad smoke para validar el andamiaje. Se elimina en Fase 0.</summary>
public sealed class SampleEntity
{
    public SampleEntity(string nombre) => Nombre = nombre;

    public string Nombre { get; }
}
