using Microsoft.AspNetCore.Mvc;
using Pos.Application.Discounts;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/discounts")]
public sealed class DiscountsController(ListDiscountsUseCase listDiscounts) : ControllerBase
{
    /// <summary>Políticas de descuento autorizadas.</summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<DiscountDto>> List() => Ok(listDiscounts.Execute());
}
