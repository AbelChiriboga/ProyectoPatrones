using Microsoft.AspNetCore.Mvc;
using Pos.Application.Catalog;

namespace Pos.Api.Controllers;

/// <summary>
/// Catálogo de productos disponibles para la venta en el POS.
/// </summary>
[ApiController]
[Route("api/v1/products")]
[Route("api/products")]
public sealed class ProductsController(SearchProductsUseCase searchProducts) : ControllerBase
{
    /// <summary>
    /// Consulta el catálogo de productos disponibles en el POS desde Odoo.
    /// </summary>
    /// <param name="search">Término de búsqueda opcional por nombre o código interno.</param>
    /// <param name="limit">Límite máximo de productos a retornar (por defecto 50).</param>
    /// <param name="cancellationToken">Token de cancelación de la petición HTTP.</param>
    /// <returns>Lista de productos vendibles con precios base e impuestos asociados.</returns>
    /// <response code="200">Catálogo recuperado exitosamente.</response>
    /// <response code="400">Parámetros de búsqueda o paginación no válidos.</response>
    /// <response code="401">No autorizado. Requiere token Bearer JWT válido.</response>
    /// <response code="502">Error de comunicación o respuesta inválida del servidor Odoo.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> Search(
        [FromQuery] string? search, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        Ok(await searchProducts.ExecuteAsync(search, limit, cancellationToken));
}
