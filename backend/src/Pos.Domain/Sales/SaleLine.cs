using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>Línea de una venta en preparación: un producto con su cantidad.</summary>
public sealed class SaleLine
{
    internal SaleLine(int productId, string productName, decimal unitPrice, int quantity)
    {
        EnsureValidQuantity(quantity);

        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    public int ProductId { get; }
    public string ProductName { get; }
    public decimal UnitPrice { get; }
    public int Quantity { get; private set; }

    public decimal Subtotal => UnitPrice * Quantity;

    internal void SetQuantity(int quantity)
    {
        EnsureValidQuantity(quantity);
        Quantity = quantity;
    }

    /// <summary>RN-02: las cantidades son mayores que cero.</summary>
    internal static void EnsureValidQuantity(int quantity)

    {
        if (quantity <= 0)
            throw new DomainException("sale.invalid_quantity", "La cantidad debe ser mayor que cero.");
    }
}