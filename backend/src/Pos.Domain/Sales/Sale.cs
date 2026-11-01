using Pos.Domain.Catalog;
using Pos.Domain.Common;

namespace Pos.Domain.Sales;

/// <summary>Venta del POS: carrito en preparación que luego se confirma.</summary>
public sealed class Sale
{
    private readonly List<SaleLine> _lines = [];

    public SaleStatus Status { get; private set; } = SaleStatus.Draft;
    public IReadOnlyList<SaleLine> Lines => _lines;

    /// <summary>Total provisional sin impuestos; Odoo calcula el IVA y el total final.</summary>
    public decimal Subtotal => _lines.Sum(line => line.Subtotal);

    public void AddProduct(Product product, int quantity)
    {
        EnsureDraft();
        SaleLine.EnsureValidQuantity(quantity);

        var line = _lines.FirstOrDefault(l => l.ProductId == product.Id);
        if (line is null)
            _lines.Add(new SaleLine(product.Id, product.Name, product.UnitPrice, quantity));
        else
            line.SetQuantity(line.Quantity + quantity);
    }

    public void ChangeQuantity(int productId, int quantity)
    {
        EnsureDraft();
        GetLine(productId).SetQuantity(quantity);

    }

    public void RemoveProduct(int productId)
    {
        EnsureDraft();
        _lines.Remove(GetLine(productId));
    }

    /// <summary>RN-01 y RN-05: no se confirma una venta vacía.</summary>
    public void Confirm()
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new DomainException("sale.empty", "La venta debe contener al menos un producto.");

        Status = SaleStatus.Confirmed;
    }

    /// <summary>RN-06: una venta confirmada no se edita como venta en preparación.</summary>
    private void EnsureDraft()
    {
        if (Status != SaleStatus.Draft)
            throw new DomainException("sale.not_editable", "Una venta confirmada ya no se puede modificar.");
    }

    private SaleLine GetLine(int productId) =>
        _lines.FirstOrDefault(l => l.ProductId == productId)
        ?? throw new DomainException("sale.line_not_found", "El producto no está en la venta.");
}