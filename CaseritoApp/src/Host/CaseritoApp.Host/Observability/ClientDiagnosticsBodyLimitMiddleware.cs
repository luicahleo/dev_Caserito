namespace CaseritoApp.Host.Observability;

public sealed class ClientDiagnosticsBodyLimitMiddleware(RequestDelegate next)
{
    private const long MaxBodySize = 16384;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/api/diagnosticos/frontend")
            && context.Request.ContentLength > MaxBodySize)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        await next(context);
    }
}
