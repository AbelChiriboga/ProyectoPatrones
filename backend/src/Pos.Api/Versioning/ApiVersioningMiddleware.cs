using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Pos.Api.Versioning;

/// <summary>
/// Middleware de versionamiento formal de la API HTTP.
/// Añade cabeceras de versionamiento a las respuestas (X-Api-Version, api-supported-versions)
/// y valida peticiones con cabecera explícita X-Api-Version / api-version.
/// Garantiza total retrocompatibilidad para clientes existentes que no envían la cabecera.
/// </summary>
public sealed class ApiVersioningMiddleware(RequestDelegate next)
{
    public const string DefaultVersion = "1.0";
    public static readonly string[] SupportedVersions = ["1.0", "1"];

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Siempre inyectar las cabeceras estándar de la API v1 en la respuesta
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append("X-Api-Version", DefaultVersion);
            context.Response.Headers.Append("api-supported-versions", DefaultVersion);
            return Task.CompletedTask;
        });

        // 2. Si el cliente envía una cabecera de versión explícita, validarla
        var requestedVersion = context.Request.Headers["X-Api-Version"].FirstOrDefault()
            ?? context.Request.Headers["api-version"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(requestedVersion))
        {
            var trimmed = requestedVersion.Trim();
            if (!SupportedVersions.Contains(trimmed))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Versión de API no soportada",
                    Detail = $"La versión '{trimmed}' no está soportada. Versiones disponibles: {string.Join(", ", SupportedVersions)}.",
                    Extensions = { ["code"] = "api.unsupported_version" }
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
                return;
            }
        }

        await next(context);
    }
}
