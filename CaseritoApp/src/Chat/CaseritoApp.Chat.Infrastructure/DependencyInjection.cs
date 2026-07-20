using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.Chat.Application.Conversaciones;
using CaseritoApp.Chat.Application.Mensajes;
using CaseritoApp.Chat.Infrastructure.Conversaciones;
using CaseritoApp.Chat.Infrastructure.Mensajes;
using CaseritoApp.Chat.Infrastructure.TiempoReal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CaseritoApp.Chat.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AgregarChat(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cadena))
        {
            servicios.AddDbContext<ChatDbContext>(opciones => opciones.UseSqlServer(cadena));
        }

        servicios.AddScoped<IRepositorioConversaciones, RepositorioConversacionesEfCore>();
        servicios.AddScoped<IConsultaConversaciones, ConsultaConversacionesEfCore>();
        servicios.AddScoped<IRepositorioMensajes, RepositorioMensajesEfCore>();
        servicios.AddScoped<IConsultaMensajes, ConsultaMensajesEfCore>();
        servicios.TryAddSingleton(TimeProvider.System);
        servicios.AddScoped<IAlmacenEntregasTiempoReal, AlmacenEntregasTiempoRealSql>();
        servicios.AddScoped<IUnitOfWork, UnitOfWorkChat>();
        return servicios;
    }
}
