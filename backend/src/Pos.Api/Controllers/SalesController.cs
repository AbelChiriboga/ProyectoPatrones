using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Auth;
using Pos.Application.Sales;

namespace Pos.Api.Controllers;

/// <summary>Solicitud para previsualizar el cálculo provisional de la venta.</summary>
/// <param name="Lines">Líneas de venta solicitadas con ID de producto y cantidad.</param>
/// <param name="DiscountCode">Código de política de descuento autorizada o null.</param>
public sealed record PreviewSaleRequest(IReadOnlyList<SaleLineRequest>? Lines, string? DiscountCode);

/// <summary>
/// Flujo de cotización, cobro en efectivo exacto, conciliación e historial de ventas del POS.
/// </summary>
[ApiController]
[Route("api/v1/sales")]
[Route("api/sales")]
public sealed class SalesController(PreviewSaleUseCase previewSale, SaleWorkflow sales, SearchSalesHistoryUseCase historyPage) : ControllerBase
{
    private string Owner => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    private bool IsAdmin => User.IsInRole(OperatorRoles.Admin);

    /// <summary>
    /// Cotiza formalmente la venta contra Odoo aplicando reglas fiscales de la sesión activa sin registrarla.
    /// </summary>
    /// <param name="request">Datos del carrito, sesión y descuento.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Cotización detallada con líneas calculadas, impuestos exactos y métodos de pago.</returns>
    /// <response code="200">Cotización calculada exitosamente.</response>
    /// <response code="400">Datos del carrito o sesión inválidos.</response>
    /// <response code="401">No autorizado. Requiere token Bearer JWT.</response>
    /// <response code="422">Regla de negocio o sesión cerrada en Odoo.</response>
    [HttpPost("quote")]
    [Authorize(Policy = OperatorRoles.Cashier)]
    [ProducesResponseType(typeof(CheckoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<CheckoutDto> Quote([FromBody] CheckoutRequest request, CancellationToken ct) => sales.QuoteAsync(request, ct);

    /// <summary>
    /// Registra la venta y el cobro en efectivo exacto con garantía de idempotencia durable por UUID.
    /// </summary>
    /// <param name="request">Datos de la operación, cotización confirmada y método de pago.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Resultado de la operación con estado confirmed o uncertain.</returns>
    /// <response code="200">Venta confirmada y registrada en Odoo exitosamente.</response>
    /// <response code="202">Operación en estado incierto (timeout o desconexión). Requiere conciliación.</response>
    /// <response code="400">Petición inválida o faltan campos obligatorios.</response>
    /// <response code="401">No autorizado. Requiere token Bearer JWT.</response>
    /// <response code="409">Conflicto de concurrencia o el total esperado cambió.</response>
    /// <response code="422">Regla de negocio no cumplida (ej. método de pago inhabilitado).</response>
    [HttpPost]
    [Authorize(Policy = OperatorRoles.Cashier)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register([FromBody] RegisterSaleRequest request, CancellationToken ct)
    {
        var result = await sales.RegisterAsync(Owner, request, ct);
        return result.Status == "confirmed" ? Ok(result) : Accepted($"/api/sales/operations/{result.OperationId}", result);
    }

    /// <summary>
    /// Consulta el estado y conciliación de una operación de venta por su UUID de idempotencia.
    /// </summary>
    /// <param name="id">UUID único de la operación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Estado de la operación y detalle si ya fue confirmada.</returns>
    /// <response code="200">Operación encontrada.</response>
    /// <response code="401">No autorizado.</response>
    /// <response code="404">Operación no encontrada para el operador actual.</response>
    [HttpGet("operations/{id:guid}")]
    [Authorize(Policy = OperatorRoles.Cashier)]
    [ProducesResponseType(typeof(OperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<OperationResult> Status(Guid id, CancellationToken ct) => sales.StatusAsync(Owner, id, ct);

    /// <summary>
    /// Historial legacy compatible. Devuelve un arreglo simple de órdenes detalladas.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <param name="beforeId">Cursor de paginación para órdenes anteriores.</param>
    /// <param name="limit">Cantidad máxima de órdenes a retornar.</param>
    /// <response code="200">Historial de órdenes recuperado.</response>
    /// <response code="400">Paginación inválida.</response>
    /// <response code="401">No autorizado.</response>
    [HttpGet]
    [Authorize(Policy = OperatorRoles.Cashier)]
    [ProducesResponseType(typeof(IReadOnlyList<SaleDetail>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<IReadOnlyList<SaleDetail>> History(CancellationToken ct, [FromQuery] int beforeId = 0, [FromQuery] int limit = 20)
        => sales.HistoryAsync(beforeId, limit, ct);

    /// <summary>
    /// Historial paginado keyset con búsqueda remota por nombre o referencia.
    /// Los cajeros ven exclusivamente sus órdenes; el Administrador puede ver todas.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <param name="search">Término de búsqueda opcional por referencia o nombre.</param>
    /// <param name="beforeId">Cursor de orden anterior para paginación eficiente keyset.</param>
    /// <param name="limit">Cantidad de registros por página (1 a 100).</param>
    /// <response code="200">Página de historial con cursor para la siguiente página.</response>
    /// <response code="400">Parámetros de búsqueda o paginación inválidos.</response>
    /// <response code="401">No autorizado.</response>
    [HttpGet("history")]
    [Authorize(Policy = OperatorRoles.Cashier)]
    [ProducesResponseType(typeof(SaleHistoryPage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public Task<SaleHistoryPage> SearchHistory(
        CancellationToken ct, [FromQuery] string? search = null, [FromQuery] int beforeId = 0, [FromQuery] int limit = 20)
    {
        string? ownerFilter = IsAdmin ? null : Owner;
        return historyPage.ExecuteAsync(search, beforeId, limit, ownerFilter, ct);
    }

    /// <summary>
    /// Consulta el detalle completo de una orden por su identificador numérico.
    /// </summary>
    /// <param name="id">Identificador entero de la orden de venta.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Detalle de la orden encontrado.</response>
    /// <response code="400">Identificador inválido.</response>
    /// <response code="401">No autorizado.</response>
    /// <response code="404">Venta no encontrada.</response>
    [HttpGet("{id:int}")]
    [Authorize(Policy = OperatorRoles.Cashier)]
    [ProducesResponseType(typeof(SaleDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<SaleDetail> Detail(int id, CancellationToken ct) => sales.DetailAsync(id, ct);

    /// <summary>
    /// Cálculo provisional del carrito con precios de catálogo y política de descuento (sin impuestos de Odoo).
    /// </summary>
    /// <param name="request">Líneas del carrito y código de descuento opcional.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Total provisional calculado.</response>
    /// <response code="400">Líneas o cantidades inválidas.</response>
    /// <response code="401">No autorizado.</response>
    /// <response code="422">Regla de negocio o producto inhabilitado.</response>
    [HttpPost("preview")]
    [Authorize(Policy = OperatorRoles.Cashier)]
    [ProducesResponseType(typeof(SalePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SalePreviewDto>> Preview([FromBody] PreviewSaleRequest request, CancellationToken cancellationToken) =>
        Ok(await previewSale.ExecuteAsync(request.Lines, request.DiscountCode, cancellationToken));
}
