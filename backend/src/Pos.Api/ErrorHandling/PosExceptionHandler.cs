using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Api.ErrorHandling;

/// <summary>
/// Traduce las excepciones de Domain/Application a respuestas ProblemDetails (RFC 9457) consistentes,
/// para que los controladores no repitan manejo de errores y el cliente reciba siempre el mismo formato.
/// </summary>
public sealed class PosExceptionHandler(IProblemDetailsService problemDetails, ILogger<PosExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title) = exception switch
        {
            ValidationException e => (StatusCodes.Status400BadRequest, e.Code, "Datos inválidos"),
            DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Code, "Regla de negocio incumplida"),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Code, "Recurso no encontrado"),
            AuthenticationFailedException e => (StatusCodes.Status401Unauthorized, e.Code, "No autenticado"),
            IntegrationException { Kind: IntegrationFailureKind.Timeout } =>
                (StatusCodes.Status504GatewayTimeout, "odoo.timeout", "Odoo no respondió a tiempo"),
            IntegrationException { Kind: IntegrationFailureKind.Unavailable } =>
                (StatusCodes.Status503ServiceUnavailable, "odoo.unavailable", "Odoo no está disponible"),
            IntegrationException =>
                (StatusCodes.Status502BadGateway, "odoo.error", "Error en la integración con Odoo"),
            _ => (StatusCodes.Status500InternalServerError, "server.error", "Error interno"),
        };

        if (status >= 500)
            logger.LogError(exception, "Fallo procesando {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Los errores internos no exponen detalles; los demás ya contienen mensajes pensados para el operador.
                Detail = status == StatusCodes.Status500InternalServerError ? "Ocurrió un error inesperado." : exception.Message,
                Extensions = { ["code"] = code },
            },
        });
    }
}
