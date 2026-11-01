using Pos.Application.Catalog;
using Pos.Application.Common;
using Pos.Domain.Catalog;

namespace Pos.UnitTests.Application;

public sealed class SearchProductsUseCaseTests
{
        private sealed class FakeCatalog(params Product[] products) : IProductCatalog
    {
        public string? ReceivedTerm { get; private set; }
        public int ReceivedLimit { get; private set; }

        public Task<IReadOnlyList<Product>> SearchAsync(string? term, int limit, CancellationToken cancellationToken)
        {
            ReceivedTerm = term;
            ReceivedLimit = limit;
            return Task.FromResult<IReadOnlyList<Product>>(products);
        }

        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Product>>(products.Where(p => ids.Contains(p.Id)).ToList());
    }

    [Fact]
    public async Task Returns_products_from_catalog_as_dtos()
    {
        var catalog = new FakeCatalog(new Product(11, "CAFE-001", "Café americano", 1.50m, [1], null));
        var useCase = new SearchProductsUseCase(catalog);

        var result = await useCase.ExecuteAsync("café", null, CancellationToken.None);

        var product = Assert.Single(result);
        Assert.Equal(11, product.Id);
        Assert.Equal("CAFE-001", product.Code);
        Assert.Equal(1.50m, product.UnitPrice);
        Assert.Null(product.QuantityAvailable);
    }

    [Fact]
    public async Task Normalizes_blank_term_and_applies_default_limit()
    {
        var catalog = new FakeCatalog();
        var useCase = new SearchProductsUseCase(catalog);

        await useCase.ExecuteAsync("   ", null, CancellationToken.None);

        Assert.Null(catalog.ReceivedTerm);
        Assert.Equal(SearchProductsUseCase.DefaultLimit, catalog.ReceivedLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(SearchProductsUseCase.MaxLimit + 1)]
    public async Task Rejects_invalid_limit(int limit)
    {
        var useCase = new SearchProductsUseCase(new FakeCatalog());

        var error = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(null, limit, CancellationToken.None));
        Assert.Equal("products.invalid_limit", error.Code);
    }

    [Fact]
    public async Task Rejects_too_long_search_term()
    {
        var useCase = new SearchProductsUseCase(new FakeCatalog());
        var term = new string('x', SearchProductsUseCase.MaxTermLength + 1);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(term, null, CancellationToken.None));
    }
}
