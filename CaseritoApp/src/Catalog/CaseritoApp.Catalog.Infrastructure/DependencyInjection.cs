using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Application.Fotos;
using CaseritoApp.Catalog.Application.Moderacion;
using CaseritoApp.Catalog.Infrastructure.Avisos;
using CaseritoApp.Catalog.Infrastructure.Fotos;
using CaseritoApp.Catalog.Infrastructure.Moderacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Catalog.Infrastructure;

/// <summary>Registro de persistencia y adaptadores del contexto Catalog.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el <see cref="CatalogDbContext"/> (si hay cadena de conexión), los repositorios y
    /// la unidad de trabajo del contexto. La unidad de trabajo se registra de forma incondicional
    /// (misma razón que en Identity: en tests el DbContext se cablea aparte).
    /// </summary>
    public static IServiceCollection AgregarCatalog(this IServiceCollection servicios, IConfiguration config)
    {
        var cadena = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cadena))
        {
            servicios.AddDbContext<CatalogDbContext>(o => o.UseSqlServer(cadena));
        }

        servicios.AddScoped<IRepositorioAvisos, RepositorioAvisosEfCore>();
        servicios.AddScoped<IConsultaCatalogo, ConsultaCatalogoEfCore>();
        servicios.AddScoped<IConsultaAvisosPublica, ConsultaAvisosPublicaEfCore>();
        servicios.AddScoped<IUnitOfWork, UnitOfWorkCatalog>();

        servicios.Configure<OpcionesAlmacenFotos>(config.GetSection("AlmacenFotos"));
        servicios.AddScoped<IAlmacenFotosAviso, AlmacenFotoAvisoDisco>();
        servicios.AddScoped<IConsultaFotoPublica, ConsultaFotoPublicaEfCore>();
        servicios.AddScoped<IRepositorioReportesAviso, RepositorioReportesAvisoEfCore>();
        servicios.AddScoped<IRepositorioRegistrosModeracion, RepositorioRegistrosModeracionEfCore>();

        return servicios;
    }
}
