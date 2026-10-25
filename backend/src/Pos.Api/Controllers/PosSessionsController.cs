using Microsoft.AspNetCore.Mvc;
using Pos.Application.Sessions;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/pos/sessions")]
public sealed class PosSessionsController(GetActiveSessionUseCase getActiveSession) : ControllerBase
{
    /// <summary>Sesión de caja abierta en Odoo donde se registrarán las ventas.</summary>
    [HttpGet("active")]
    public async Task<ActionResult<PosSessionDto>> GetActive(CancellationToken cancellationToken) =>
        Ok(await getActiveSession.ExecuteAsync(cancellationToken));
}
