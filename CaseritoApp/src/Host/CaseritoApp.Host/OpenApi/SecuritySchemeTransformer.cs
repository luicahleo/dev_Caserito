using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OpenApi;
// En este entorno (.NET 10, Microsoft.OpenApi 2.x resuelto vía CPM) el modelo de OpenAPI.NET
// ya es el de interfaces (IOpenApiSecurityScheme, referencias tipadas *Reference): los tipos
// viven en el namespace plano Microsoft.OpenApi (no en Microsoft.OpenApi.Models) y no existen
// OpenApiReference ni la propiedad Reference/SecurityRequirements que documentaba el brief.
using Microsoft.OpenApi;

namespace CaseritoApp.Host.OpenApi;

/// <summary>
/// Registra el esquema de seguridad Bearer JWT en el documento OpenAPI y lo aplica como
/// requisito global, para que el contrato refleje los endpoints <c>[Authorize]</c>.
/// </summary>
internal sealed class SecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var esquema = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
        };

        document.Components ??= new OpenApiComponents();
        var componentes = document.Components;
        componentes.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        var esquemasSeguridad = componentes.SecuritySchemes;
        esquemasSeguridad["Bearer"] = esquema;

        var referenciaEsquema = new OpenApiSecuritySchemeReference("Bearer", document);
        document.Security ??= [];
        var requisitos = document.Security;
        requisitos.Add(new OpenApiSecurityRequirement { [referenciaEsquema] = [] });

        return Task.CompletedTask;
    }
}
