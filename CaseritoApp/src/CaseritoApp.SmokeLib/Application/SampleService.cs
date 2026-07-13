using CaseritoApp.SmokeLib.Domain;

namespace CaseritoApp.SmokeLib.Application;

/// <summary>Servicio smoke: Application depende de Domain (dirección permitida).</summary>
public static class SampleService
{
    public static string Describir(SampleEntity entidad) => $"Entidad: {entidad.Nombre}";
}
