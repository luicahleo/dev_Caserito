using CaseritoApp.Chat.Domain.Moderacion;

namespace CaseritoApp.Chat.Application.Moderacion;

public interface IRepositorioRegistrosModeracionChat
{
    public void Agregar(RegistroModeracionChat registro);
}

public interface IAuditorModeracionChat
{
    public Task<bool> RegistrarConsultaEvidenciaAsync(
        Guid reporteId, Guid conversacionId, Guid moderadorId, DateTimeOffset fecha, CancellationToken ct);
}
