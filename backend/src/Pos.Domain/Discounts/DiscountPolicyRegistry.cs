using Pos.Domain.Common;

namespace Pos.Domain.Discounts;

/// <summary>Conjunto de políticas autorizadas. Un código que no esté aquí se rechaza (RF-08).</summary>
public sealed class DiscountPolicyRegistry(IEnumerable<IDiscountPolicy> policies)
{
    private readonly Dictionary<string, IDiscountPolicy> _byCode =
        policies.ToDictionary(p => p.Code, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IDiscountPolicy> All => _byCode.Values.ToList();

    /// <summary>Devuelve la política del código indicado; sin código se usa "sin descuento".</summary>
    public IDiscountPolicy Get(string? code)
    {
        var requested = string.IsNullOrWhiteSpace(code) ? NoDiscountPolicy.PolicyCode : code.Trim();

        return _byCode.TryGetValue(requested, out var policy)
            ? policy
            : throw new DomainException("discount.not_authorized", $"El descuento '{requested}' no está autorizado.");
    }
}
