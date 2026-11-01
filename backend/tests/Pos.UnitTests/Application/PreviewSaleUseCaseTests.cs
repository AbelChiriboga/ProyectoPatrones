using Pos.Application.Catalog;
using Pos.Application.Common;
using Pos.Application.Sales;
using Pos.Domain.Catalog;
using Pos.Domain.Common;
using Pos.Domain.Discounts;

namespace Pos.UnitTests.Application;

public sealed class PreviewSaleUseCaseTests
{
    private sealed class FakeCatalog(params Product[] products) : IProductCatalog
    {
        public Task<IReadOnlyList<Product>> SearchAsync(string? term, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Product>>(products.Where(p => ids.Contains(p.Id)).ToList());
    }

    /// <summary>Política defectuosa: descuenta más que el subtotal.</summary>
    private sealed class GreedyPolicy : IDiscountPolicy
    {
        public string Code => "greedy";
        public string Name => "Defectuosa";
        public decimal Calculate(decimal subtotal) => subtotal + 1m;
    }

    private static PreviewSaleUseCase CreateUseCase() => new(
        new FakeCatalog(
            new Product(11, "CAFE-001", "Café americano", 1.50m, [1], null),
            new Product(12, "SAND-001", "Sánduche de pollo", 3.75m, [1], null)),
        new DiscountPolicyRegistry(
        [
            new NoDiscountPolicy(),
            new PercentageDiscountPolicy("promo-10", "Promoción 10 %", 10m),
            new ThresholdPercentageDiscountPolicy("bulk-15", "15 % desde 20 USD", 20m, 15m),
            new GreedyPolicy(),
        ]));

    [Fact]
    public async Task Calculates_lines_and_subtotal_with_catalog_prices()
    {
        var result = await CreateUseCase().ExecuteAsync(
            [new SaleLineRequest(11, 2), new SaleLineRequest(12, 1)], null, CancellationToken.None);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(3.00m, result.Lines[0].Subtotal);
        Assert.Equal(6.75m, result.Subtotal);
        Assert.Equal("none", result.DiscountCode);
        Assert.Equal(0m, result.Discount);
        Assert.Equal(6.75m, result.Total);
    }

    [Fact]
    public async Task Merges_repeated_products_into_one_line()
    {
        var result = await CreateUseCase().ExecuteAsync(
            [new SaleLineRequest(11, 1), new SaleLineRequest(11, 2)], null, CancellationToken.None);

        var line = Assert.Single(result.Lines);
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public async Task Empty_cart_returns_an_empty_preview()
    {
        var result = await CreateUseCase().ExecuteAsync([], null, CancellationToken.None);

        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.Total);
    }

    [Fact]
    public async Task Unknown_product_is_reported_as_not_found()
    {
        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateUseCase().ExecuteAsync([new SaleLineRequest(99, 1)], null, CancellationToken.None));

        Assert.Equal("products.not_found", error.Code);
    }

    [Fact]
    public async Task Quantity_zero_is_rejected_by_the_domain()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().ExecuteAsync([new SaleLineRequest(11, 0)], null, CancellationToken.None));

        Assert.Equal("sale.invalid_quantity", error.Code);
    }

    [Fact]
    public async Task Missing_list_is_rejected()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateUseCase().ExecuteAsync(null, null, CancellationToken.None));

        Assert.Equal("sale.lines_required", error.Code);
    }

    [Fact]
    public async Task Too_many_lines_are_rejected()
    {
        var lines = Enumerable.Range(1, PreviewSaleUseCase.MaxLines + 1)
            .Select(_ => new SaleLineRequest(11, 1))
            .ToList();

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            CreateUseCase().ExecuteAsync(lines, null, CancellationToken.None));

        Assert.Equal("sale.too_many_lines", error.Code);
    }

    [Fact]
    public async Task Applies_an_authorized_percentage_discount()
    {
        var result = await CreateUseCase().ExecuteAsync(
            [new SaleLineRequest(11, 2), new SaleLineRequest(12, 1)], "PROMO-10", CancellationToken.None);

        Assert.Equal("promo-10", result.DiscountCode);
        Assert.Equal(0.68m, result.Discount);
        Assert.Equal(6.07m, result.Total);
    }

    [Fact]
    public async Task Threshold_discount_does_not_apply_below_the_minimum()
    {
        var result = await CreateUseCase().ExecuteAsync(
            [new SaleLineRequest(12, 1)], "bulk-15", CancellationToken.None);

        Assert.Equal(0m, result.Discount);
        Assert.Equal(3.75m, result.Total);
    }

    [Fact]
    public async Task Threshold_discount_applies_when_the_minimum_is_reached()
    {
        var result = await CreateUseCase().ExecuteAsync(
            [new SaleLineRequest(12, 6)], "bulk-15", CancellationToken.None);

        Assert.Equal(22.50m, result.Subtotal);
        Assert.Equal(3.38m, result.Discount);
        Assert.Equal(19.12m, result.Total);
    }

    [Fact]
    public async Task Rejects_a_discount_that_is_not_authorized()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().ExecuteAsync([new SaleLineRequest(11, 1)], "vip-100", CancellationToken.None));

        Assert.Equal("discount.not_authorized", error.Code);
    }

    [Fact]
    public async Task Rejects_a_policy_that_discounts_more_than_the_subtotal()
    {
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().ExecuteAsync([new SaleLineRequest(11, 1)], "greedy", CancellationToken.None));

        Assert.Equal("discount.invalid_amount", error.Code);
    }
}
