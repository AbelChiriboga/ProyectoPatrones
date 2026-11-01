namespace Pos.Domain.Discounts;

/// <summary>Política por defecto: no se descuenta nada.</summary>
public sealed class NoDiscountPolicy : IDiscountPolicy
{
    public const string PolicyCode = "none";

    public string Code => PolicyCode;
    public string Name => "Sin descuento";

    public decimal Calculate(decimal subtotal) => 0m;
}

/// <summary>Porcentaje fijo sobre el subtotal.</summary>
public sealed class PercentageDiscountPolicy(string code, string name, decimal percent) : IDiscountPolicy
{
    public string Code => code;
    public string Name => name;

    public decimal Calculate(decimal subtotal) =>
        Math.Round(subtotal * percent / 100m, 2, MidpointRounding.AwayFromZero);
}

/// <summary>Porcentaje que solo aplica cuando el subtotal alcanza un mínimo.</summary>
public sealed class ThresholdPercentageDiscountPolicy(string code, string name, decimal minimumSubtotal, decimal percent) : IDiscountPolicy
{
    public string Code => code;
    public string Name => name;

    public decimal Calculate(decimal subtotal) =>
        subtotal >= minimumSubtotal ? Math.Round(subtotal * percent / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
}
