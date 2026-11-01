using Pos.Application.Common;
using Pos.Domain.Catalog;

namespace Pos.Application.Catalog;

/// <summary>Puerto de salida: catálogo de productos vendibles en el POS (implementado contra Odoo).</summary>
public interface IProductCatalog
{
    Task<IReadOnlyList<Product>> SearchAsync(string? term, int limit, CancellationToken cancellationToken);

    /// <summary>Productos vendibles en el POS con esos identificadores; los que no existan simplemente no se devuelven.</summary>
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
}

public sealed record ProductDto(int Id, string? Code, string Name, decimal UnitPrice, IReadOnlyList<int> TaxIds, decimal? QuantityAvailable)
{
    public static ProductDto From(Product product) =>
        new(product.Id, product.Code, product.Name, product.UnitPrice, product.TaxIds, product.QuantityAvailable);
}

/// <summary>RF-01 / RF-02: consultar y buscar productos disponibles por nombre o código.</summary>
public sealed class SearchProductsUseCase(IProductCatalog catalog)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;
    public const int MaxTermLength = 100;

    public async Task<IReadOnlyList<ProductDto>> ExecuteAsync(string? term, int? limit, CancellationToken cancellationToken)
    {
        var normalizedTerm = string.IsNullOrWhiteSpace(term) ? null : term.Trim();
        if (normalizedTerm is { Length: > MaxTermLength })
            throw new ValidationException("products.search_too_long", $"El texto de búsqueda admite como máximo {MaxTermLength} caracteres.");

        var effectiveLimit = limit ?? DefaultLimit;
        if (effectiveLimit is < 1 or > MaxLimit)
            throw new ValidationException("products.invalid_limit", $"El límite debe estar entre 1 y {MaxLimit}.");

        var products = await catalog.SearchAsync(normalizedTerm, effectiveLimit, cancellationToken);
        return products.Select(ProductDto.From).ToList();
    }
}
