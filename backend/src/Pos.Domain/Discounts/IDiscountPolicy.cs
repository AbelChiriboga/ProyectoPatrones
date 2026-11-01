namespace Pos.Domain.Discounts;

/// <summary>
/// Estrategia de descuento (patrón Strategy). Cada política decide cuánto se descuenta
/// de un subtotal; agregar una política nueva no obliga a modificar las existentes (RF-07).
/// </summary>
public interface IDiscountPolicy
{
    string Code { get; }
    string Name { get; }

    /// <summary>Monto a descontar del subtotal; nunca negativo ni mayor que el subtotal.</summary>
    decimal Calculate(decimal subtotal);
}
