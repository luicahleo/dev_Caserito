using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.Notifications.Application.Busquedas;
using CaseritoApp.Notifications.Application.Notificaciones;
using CaseritoApp.Notifications.Application.PuntosEncuentro;
using CaseritoApp.Notifications.Application.Push;
using CaseritoApp.Notifications.Infrastructure.Busquedas;
using CaseritoApp.Notifications.Infrastructure.Email;
using CaseritoApp.Notifications.Infrastructure.Notificaciones;
using CaseritoApp.Notifications.Infrastructure.PuntosEncuentro;
using CaseritoApp.Notifications.Infrastructure.Push;
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
        servicios.AddScoped<IBusquedaGuardadaRepository, BusquedaGuardadaRepositoryEfCore>();
        servicios.AddScoped<IPuntoEncuentroSeguroRepository, PuntoEncuentroSeguroRepositoryEfCore>();
        servicios.AddScoped<IRepositorioSuscripcionesPush, RepositorioSuscripcionesPushEfCore>();
        servicios.AddScoped<IAlmacenIntencionesPush, AlmacenIntencionesPushEfCore>();
        servicios.AddScoped<UnitOfWorkNotifications>();
        servicios.AddScoped<IUnitOfWork>(
            proveedor => proveedor.GetRequiredService<UnitOfWorkNotifications>());

        servicios.Configure<OpcionesEmail>(config.GetSection(OpcionesEmail.Seccion));
        servicios.AddOptions<OpcionesWebPush>()
            .Bind(config.GetSection(OpcionesWebPush.Seccion))
            .Validate(o => !o.Habilitado
                || (!string.IsNullOrWhiteSpace(o.ClavePublica)
                    && !string.IsNullOrWhiteSpace(o.ClavePrivada)
                    && Uri.TryCreate(o.Sujeto, UriKind.Absolute, out _)),
                "La configuración de Web Push no es válida.")
            .ValidateOnStart();
        servicios.AddDataProtection();
        servicios.AddScoped<IWebPushSender, WebPushSender>();
        servicios.AddScoped<IProtectorComprobantesEntrega, ProtectorComprobantesEntrega>();
        if (!entorno.IsEnvironment("Testing"))
        {
            servicios.AddHostedService<DespachadorWebPush>();
        }

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
