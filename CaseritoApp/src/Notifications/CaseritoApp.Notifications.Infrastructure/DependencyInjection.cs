using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.Notifications.Application.Notificaciones;
using CaseritoApp.Notifications.Infrastructure.Email;
using CaseritoApp.Notifications.Infrastructure.Notificaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CaseritoApp.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AgregarNotifications(
        this IServiceCollection servicios,
        IConfiguration config,
        IHostEnvironment entorno)
    {
        var cadena = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cadena))
        {
            servicios.AddDbContext<NotificationsDbContext>(
                opciones => opciones.UseSqlServer(cadena));
        }

        servicios.AddScoped<INotificacionRepository, NotificacionRepositoryEfCore>();
        servicios.AddScoped<UnitOfWorkNotifications>();
        servicios.AddScoped<IUnitOfWork>(
            proveedor => proveedor.GetRequiredService<UnitOfWorkNotifications>());

        servicios.Configure<OpcionesEmail>(config.GetSection(OpcionesEmail.Seccion));

        if (entorno.IsDevelopment() || entorno.IsEnvironment("Testing"))
        {
            servicios.AddScoped<IEmailSender, LogEmailSender>();
        }
        else
        {
            servicios.AddScoped<IEmailSender, SmtpEmailSender>();
        }

        return servicios;
    }
}
