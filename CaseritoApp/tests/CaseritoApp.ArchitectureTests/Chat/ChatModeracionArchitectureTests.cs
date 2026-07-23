using CaseritoApp.Chat.Application.Moderacion;
using CaseritoApp.Chat.Domain.Moderacion;
using CaseritoApp.Host.Endpoints;
using CaseritoApp.Identity.Domain.Autorizacion;
using CaseritoApp.Identity.Infrastructure.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CaseritoApp.ArchitectureTests.Chat;

public sealed class ChatModeracionArchitectureTests
{
    [Fact]
    public void DomainYApplication_ConservanDependenciasPermitidas()
    {
        var referenciasDomain = typeof(RegistroModeracionChat).Assembly
            .GetReferencedAssemblies()
            .Select(referencia => referencia.Name!)
            .Where(nombre => nombre.StartsWith("CaseritoApp.", StringComparison.Ordinal))
            .ToArray();
        var referenciasApplication = typeof(ReporteChatColaDto).Assembly
            .GetReferencedAssemblies()
            .Select(referencia => referencia.Name!)
            .ToArray();
        string[] capasProhibidas =
        [
            "CaseritoApp.Host",
            "CaseritoApp.Chat.Infrastructure",
            "CaseritoApp.Identity",
            "CaseritoApp.Catalog"
        ];

        Assert.Equal(["CaseritoApp.BuildingBlocks.Domain"], referenciasDomain);
        Assert.DoesNotContain(referenciasApplication, referencia =>
            capasProhibidas.Any(capa =>
                referencia.StartsWith(capa, StringComparison.Ordinal)));
    }

    [Fact]
    public void EndpointsAdministrativos_RequierenChatModerar()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<ISender>(_ => null!);
        builder.Services.AddSingleton<IPublisher>(_ => null!);
        var app = builder.Build();
        app.MapModeracionChatEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(fuente => fuente.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.RoutePattern.RawText?.StartsWith(
                    "/api/admin/moderacion/chat",
                    StringComparison.Ordinal) is true)
            .ToArray();
        var policyEsperada = PoliticasAutorizacion.Permiso(Permisos.ChatModerar);

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                autorizacion => autorizacion.Policy == policyEsperada));
    }
}
