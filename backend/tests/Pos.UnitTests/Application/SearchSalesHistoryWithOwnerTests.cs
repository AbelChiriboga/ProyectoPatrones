using Pos.Application.Common;
using Pos.Application.Sales;

namespace Pos.UnitTests.Application;

/// <summary>Verifica el filtro por propietario (owner) en SearchSalesHistoryUseCase.</summary>
public sealed class SearchSalesHistoryWithOwnerTests
{
    private sealed class CapturingReader : ISaleHistoryReader
    {
        public SaleHistoryQuery? LastQuery { get; private set; }
        public Task<SaleHistoryPage> SearchAsync(SaleHistoryQuery query, CancellationToken ct)
        {
            LastQuery = query;
            return Task.FromResult(new SaleHistoryPage([], false, null));
        }
    }

    [Fact]
    public async Task Cashier_history_passes_owner_filter_to_reader()
    {
        var reader = new CapturingReader();
        var useCase = new SearchSalesHistoryUseCase(reader);

        await useCase.ExecuteAsync(null, 0, 20, "cajero1", default);

        Assert.Equal("cajero1", reader.LastQuery!.Owner);
    }

    [Fact]
    public async Task Admin_history_passes_null_owner_to_reader()
    {
        var reader = new CapturingReader();
        var useCase = new SearchSalesHistoryUseCase(reader);

        await useCase.ExecuteAsync(null, 0, 20, null, default);

        Assert.Null(reader.LastQuery!.Owner);
    }

    [Fact]
    public async Task Legacy_overload_passes_null_owner()
    {
        var reader = new CapturingReader();
        var useCase = new SearchSalesHistoryUseCase(reader);

        await useCase.ExecuteAsync(null, 0, 20, default);

        Assert.Null(reader.LastQuery!.Owner);
    }

    [Fact]
    public async Task Owner_filter_does_not_bypass_pagination_validation()
    {
        var reader = new CapturingReader();
        var useCase = new SearchSalesHistoryUseCase(reader);

        await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(null, -1, 20, "cajero1", default));
        Assert.Null(reader.LastQuery);
    }
}
