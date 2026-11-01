using Pos.Domain.Discounts;

namespace Pos.Application.Discounts;

public sealed record DiscountDto(string Code, string Name);

/// <summary>Lista las políticas de descuento autorizadas para que la interfaz las ofrezca.</summary>
public sealed class ListDiscountsUseCase(DiscountPolicyRegistry discounts)
{
    public IReadOnlyList<DiscountDto> Execute() =>
        discounts.All.Select(p => new DiscountDto(p.Code, p.Name)).ToList();
}
