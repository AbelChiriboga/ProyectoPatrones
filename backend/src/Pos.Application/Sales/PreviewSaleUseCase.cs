using Pos.Application.Catalog;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Discounts;
using Pos.Domain.Sales;

namespace Pos.Application.Sales;

public sealed record SaleLineRequest(int ProductId, int Quantity);

public sealed record SaleLineDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal);

public sealed record SalePreviewDto(
    IReadOnlyList<SaleLineDto> Lines, decimal Subtotal, string DiscountCode, decimal Discount, decimal Total);

/// <summary>
/// RF-03 a RF-08: calcula el total provisional del carrito con los precios reales de Odoo (RN-03)
/// y aplica una política de descuento autorizada (RN-04).
/// </summary>
public sealed class PreviewSaleUseCase(IProductCatalog catalog, DiscountPolicyRegistry discounts)
{
    public const int MaxLines = 100;

    public async Task<SalePreviewDto> ExecuteAsync(
        IReadOnlyList<SaleLineRequest>? requestedLines, string? discountCode, CancellationToken cancellationToken)
    {
        if (requestedLines is null)
            throw new ValidationException("sale.lines_required", "Debe enviar la lista de productos de la venta.");
        if (requestedLines.Count > MaxLines)
            throw new ValidationException("sale.too_many_lines", $"Una venta admite como máximo {MaxLines} líneas.");

        var policy = discounts.Get(discountCode);

        var ids = requestedLines.Select(l => l.ProductId).Distinct().ToList();
        var products = (await catalog.GetByIdsAsync(ids, cancellationToken)).ToDictionary(p => p.Id);

        var sale = new Sale();
        foreach (var requested in requestedLines)
        {
            if (!products.TryGetValue(requested.ProductId, out var product))
                throw new NotFoundException("products.not_found", $"El producto {requested.ProductId} no existe o no está disponible en el POS.");

            sale.AddProduct(product, requested.Quantity);
        }

        var discount = policy.Calculate(sale.Subtotal);
        if (discount < 0 || discount > sale.Subtotal)
            throw new DomainException("discount.invalid_amount", "El descuento calculado no es válido para esta venta.");

        var lines = sale.Lines
            .Select(l => new SaleLineDto(l.ProductId, l.ProductName, l.UnitPrice, l.Quantity, l.Subtotal))
            .ToList();
        return new SalePreviewDto(lines, sale.Subtotal, policy.Code, discount, sale.Subtotal - discount);
    }
}
