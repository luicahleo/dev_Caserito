using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.Catalog.Application.Avisos;
using CaseritoApp.Catalog.Infrastructure.Avisos;
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
        servicios.AddScoped<IUnitOfWork, UnitOfWorkCatalog>();

        return servicios;
    }
}
