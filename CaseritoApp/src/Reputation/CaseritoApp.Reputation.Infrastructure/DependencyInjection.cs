using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.Reputation.Application.Resenas;
using CaseritoApp.Reputation.Infrastructure.Resenas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Reputation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AgregarReputation(
        this IServiceCollection servicios,
        IConfiguration config)
    {
        var cadena = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cadena))
        {
            servicios.AddDbContext<ReputationDbContext>(
                opciones => opciones.UseSqlServer(cadena));
        }

        servicios.AddScoped<IRepositorioResenas, RepositorioResenasEfCore>();
        servicios.AddScoped<IConsultaResenas, ConsultaResenasEfCore>();
        servicios.AddScoped<UnitOfWorkReputation>();
        servicios.AddScoped<IUnitOfWork>(
            proveedor => proveedor.GetRequiredService<UnitOfWorkReputation>());
        return servicios;
    }
}
