using Microsoft.AspNetCore.Mvc;
using Pos.Application.Discounts;

namespace Pos.Api.Controllers;

/// <summary>
/// Políticas de descuento comerciales autorizadas para el POS.
/// </summary>
[ApiController]
[Route("api/v1/discounts")]
[Route("api/discounts")]
public sealed class DiscountsController(ListDiscountsUseCase listDiscounts) : ControllerBase
{
    /// <summary>
    /// Lista todas las políticas de descuento autorizadas en el sistema (Strategy GoF).
    /// </summary>
    /// <returns>Lista de políticas de descuento activas con código y descripción.</returns>
    /// <response code="200">Políticas recuperadas exitosamente.</response>
    /// <response code="401">No autorizado. Requiere token Bearer JWT válido.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DiscountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<IReadOnlyList<DiscountDto>> List() => Ok(listDiscounts.Execute());
}
