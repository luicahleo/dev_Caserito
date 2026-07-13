using CaseritoApp.SmokeLib.Domain;

namespace CaseritoApp.SmokeLib.Application;

/// <summary>Servicio smoke: Application depende de Domain (dirección permitida).</summary>
public sealed class SampleService
{
    public string Describir(SampleEntity entidad) => $"Entidad: {entidad.Nombre}";
}
