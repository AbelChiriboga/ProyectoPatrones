using Microsoft.AspNetCore.Mvc;
using Pos.Application.Catalog;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(SearchProductsUseCase searchProducts) : ControllerBase
{
    /// <summary>Consulta del catálogo POS en Odoo, opcionalmente filtrado por nombre o código.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> Search(
        [FromQuery] string? search, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        Ok(await searchProducts.ExecuteAsync(search, limit, cancellationToken));
}
