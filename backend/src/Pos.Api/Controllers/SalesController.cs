using Microsoft.AspNetCore.Mvc;
using Pos.Application.Sales;

namespace Pos.Api.Controllers;

public sealed record PreviewSaleRequest(IReadOnlyList<SaleLineRequest>? Lines, string? DiscountCode);

[ApiController]
[Route("api/sales")]
public sealed class SalesController(PreviewSaleUseCase previewSale, SaleWorkflow sales, SearchSalesHistoryUseCase historyPage) : ControllerBase
{
    private string Owner => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException();

    [HttpPost("quote")]
    public Task<CheckoutDto> Quote(CheckoutRequest request, CancellationToken ct) => sales.QuoteAsync(request, ct);

    [HttpPost]
    public async Task<IActionResult> Register(RegisterSaleRequest request, CancellationToken ct) {
        var result = await sales.RegisterAsync(Owner, request, ct);
        return result.Status == "confirmed" ? Ok(result) : Accepted($"/api/sales/operations/{result.OperationId}", result);
    }
    [HttpGet("operations/{id:guid}")]
    public Task<OperationResult> Status(Guid id, CancellationToken ct) => sales.StatusAsync(Owner, id, ct);
    [HttpGet]
    public Task<IReadOnlyList<SaleDetail>> History(CancellationToken ct, int beforeId = 0, int limit = 20) => sales.HistoryAsync(beforeId, limit, ct);
    [HttpGet("history")]
    public Task<SaleHistoryPage> SearchHistory(CancellationToken ct, string? search = null, int beforeId = 0, int limit = 20) =>
        historyPage.ExecuteAsync(search, beforeId, limit, ct);
    [HttpGet("{id:int}")]
    public Task<SaleDetail> Detail(int id, CancellationToken ct) => sales.DetailAsync(id, ct);

    /// <summary>Total provisional del carrito con precios de Odoo y descuento autorizado (aún no registra la venta).</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<SalePreviewDto>> Preview([FromBody] PreviewSaleRequest request, CancellationToken cancellationToken) =>
        Ok(await previewSale.ExecuteAsync(request.Lines, request.DiscountCode, cancellationToken));
}
