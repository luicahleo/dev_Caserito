using System.Security.Cryptography;
using System.Text.Json;
using CaseritoApp.Notifications.Application.Push;
using Microsoft.AspNetCore.DataProtection;

namespace CaseritoApp.Notifications.Infrastructure.Push;

public sealed class ProtectorComprobantesEntrega : IProtectorComprobantesEntrega
{
    private readonly ITimeLimitedDataProtector _protector;

    public ProtectorComprobantesEntrega(IDataProtectionProvider proveedor) =>
        _protector = proveedor
            .CreateProtector("CaseritoApp.WebPush.Entrega.v1")
            .ToTimeLimitedDataProtector();

    public string Crear(DatosComprobanteEntrega datos, DateTimeOffset expiraEn) =>
        _protector.Protect(JsonSerializer.Serialize(datos), expiraEn);

    public bool TryValidar(string comprobante, out DatosComprobanteEntrega datos)
    {
        datos = default!;
        if (string.IsNullOrWhiteSpace(comprobante) || comprobante.Length > 4096)
        {
            return false;
        }

        try
        {
            var valor = JsonSerializer.Deserialize<DatosComprobanteEntrega>(
                _protector.Unprotect(comprobante));
            if (valor is null || valor.ConversacionId == Guid.Empty
                || valor.DestinatarioId == Guid.Empty || valor.Secuencia <= 0)
            {
                return false;
            }

            datos = valor;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return false;
        }
    }
}
