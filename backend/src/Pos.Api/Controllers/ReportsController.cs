using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Auth;
using Pos.Application.Reports;

namespace Pos.Api.Controllers;

/// <summary>
/// Reportes globales consolidados de ventas — exclusivo para el rol Administrador.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Route("api/reports")]
[Authorize(Policy = OperatorRoles.Admin)]
public sealed class ReportsController(GetSalesReportUseCase report) : ControllerBase
{
    /// <summary>
    /// Genera el reporte consolidado global de ventas, agrupado por cajero y método de pago.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <param name="from">Fecha y hora inicial del período (opcional, formato ISO-8601 UTC).</param>
    /// <param name="to">Fecha y hora final del período (opcional, formato ISO-8601 UTC).</param>
    /// <returns>Reporte consolidado con órdenes totales, importe global, desglose por cajero y métodos de pago.</returns>
    /// <response code="200">Reporte generado exitosamente.</response>
    /// <response code="400">Rango de fechas inválido (fecha inicial posterior a fecha final).</response>
    /// <response code="401">No autenticado. Token JWT ausente o inválido.</response>
    /// <response code="403">Acceso denegado. Se requiere rol Administrador.</response>
    /// <response code="502">Error al consultar datos en Odoo.</response>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(SalesReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<SalesReport>> SalesReport(
        CancellationToken ct,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null)
        => Ok(await report.ExecuteAsync(from, to, ct));
}
