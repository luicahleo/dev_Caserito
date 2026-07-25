using CaseritoApp.BuildingBlocks.Application.Abstractions;
using CaseritoApp.Orders.Application.Ordenes;
using CaseritoApp.Orders.Infrastructure.Ordenes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.Orders.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AgregarOrders(
        this IServiceCollection servicios,
        IConfiguration config)
    {
        var cadena = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(cadena))
        {
            servicios.AddDbContext<OrdersDbContext>(opciones => opciones.UseSqlServer(cadena));
        }

        servicios.AddScoped<IRepositorioOrdenes, RepositorioOrdenesEfCore>();
        servicios.AddScoped<IConsultaOrdenes, ConsultaOrdenesEfCore>();
        servicios.AddScoped<IConsultaOrdenParaReputacion, ConsultaOrdenParaReputacionEfCore>();
        servicios.AddScoped<UnitOfWorkOrders>();
        servicios.AddScoped<IUnitOfWork>(
            proveedor => proveedor.GetRequiredService<UnitOfWorkOrders>());
        return servicios;
    }
}
