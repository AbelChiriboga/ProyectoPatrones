using Microsoft.AspNetCore.Mvc;
using Pos.Application.Sessions;

namespace Pos.Api.Controllers;

/// <summary>
/// Sesiones de caja del punto de venta en Odoo.
/// </summary>
[ApiController]
[Route("api/v1/pos/sessions")]
[Route("api/pos/sessions")]
public sealed class PosSessionsController(GetActiveSessionUseCase getActiveSession) : ControllerBase
{
    /// <summary>
    /// Obtiene la sesión de caja actualmente abierta en Odoo para registrar ventas.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación de la petición HTTP.</param>
    /// <returns>Datos de la sesión activa, moneda y métodos de pago habilitados.</returns>
    /// <response code="200">Sesión activa encontrada y devuelta.</response>
    /// <response code="401">No autorizado. Requiere token Bearer JWT válido.</response>
    /// <response code="404">No existe ninguna sesión POS abierta en Odoo.</response>
    /// <response code="502">Fallo en la comunicación con Odoo.</response>
    [HttpGet("active")]
    [ProducesResponseType(typeof(PosSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PosSessionDto>> GetActive(CancellationToken cancellationToken) =>
        Ok(await getActiveSession.ExecuteAsync(cancellationToken));
}
