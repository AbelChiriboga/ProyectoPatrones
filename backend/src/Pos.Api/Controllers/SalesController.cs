using Microsoft.AspNetCore.Mvc;
using Pos.Application.Sales;

namespace Pos.Api.Controllers;

public sealed record PreviewSaleRequest(IReadOnlyList<SaleLineRequest>? Lines, string? DiscountCode);

[ApiController]
[Route("api/sales")]
public sealed class SalesController(PreviewSaleUseCase previewSale) : ControllerBase
{
    /// <summary>Total provisional del carrito con precios de Odoo y descuento autorizado (aún no registra la venta).</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<SalePreviewDto>> Preview([FromBody] PreviewSaleRequest request, CancellationToken cancellationToken) =>
        Ok(await previewSale.ExecuteAsync(request.Lines, request.DiscountCode, cancellationToken));
}
