using Pos.Domain.Common;

namespace Pos.Domain.Catalog;

/// <summary>
/// Producto vendible tal como lo ve el POS. Los datos maestros pertenecen a Odoo;
/// esta clase solo representa la vista que necesitan los casos de uso.
/// </summary>
public sealed class Product
{
    public Product(int id, string? code, string name, decimal unitPrice, IReadOnlyList<int> taxIds, decimal? quantityAvailable)
    {
        if (id <= 0)
            throw new DomainException("product.invalid_id", "El identificador del producto debe ser positivo.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("product.invalid_name", "El producto debe tener nombre.");
        if (unitPrice < 0)
            throw new DomainException("product.invalid_price", "El precio de referencia no puede ser negativo.");

        Id = id;
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        Name = name.Trim();
        UnitPrice = unitPrice;
        TaxIds = taxIds;
        QuantityAvailable = quantityAvailable;
    }

    public int Id { get; }
    public string? Code { get; }
    public string Name { get; }
    public decimal UnitPrice { get; }
    public IReadOnlyList<int> TaxIds { get; }

    /// <summary>Existencias informadas por Odoo; null cuando no hay información fiable (producto no inventariable).</summary>
    public decimal? QuantityAvailable { get; }
}
