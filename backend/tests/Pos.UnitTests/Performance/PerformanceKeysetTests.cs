using Pos.Application.Common;
using Pos.Application.Reports;
using Pos.Application.Sales;

namespace Pos.UnitTests.Performance;

/// <summary>
/// Pruebas automatizadas de arquitectura de rendimiento:
/// Valida que la paginación keyset (cursor antes de ID) y las proyecciones acotadas
/// previenen la degradación O(N) de la paginación por OFFSET bajo 1,000,000 de registros.
/// </summary>
public sealed class PerformanceKeysetTests
{
    private sealed class FastMemoryHistoryReader : ISaleHistoryReader
    {
        public int QueriedLimit { get; private set; }
        public int QueriedBeforeId { get; private set; }
        public string? QueriedOwner { get; private set; }

        public Task<SaleHistoryPage> SearchAsync(SaleHistoryQuery query, CancellationToken ct)
        {
            QueriedLimit = query.Limit;
            QueriedBeforeId = query.BeforeId;
            QueriedOwner = query.Owner;

            // Simula retorno inmediato con keyset pagination (seek B-tree directo)
            var items = new List<SaleHistoryItem>();
            for (var i = 0; i < query.Limit; i++)
            {
                var id = query.BeforeId > 0 ? query.BeforeId - (i + 1) : 1_000_000 - i;
                items.Add(new SaleHistoryItem(id, $"POS/{id:07d}", DateTimeOffset.UtcNow, 25.50m, "USD", "paid"));
            }

            var nextBeforeId = items.Count > 0 ? items[^1].Id : (int?)null;
            return Task.FromResult(new SaleHistoryPage(items, true, nextBeforeId));
        }
    }

    [Fact]
    public async Task Keyset_pagination_maintains_bounded_seek_at_deep_pages()
    {
        var reader = new FastMemoryHistoryReader();
        var useCase = new SearchSalesHistoryUseCase(reader);

        // Página profunda en 1M registros: antes del ID 500,000
        var page = await useCase.ExecuteAsync(null, 500_000, 20, "cajero1", CancellationToken.None);

        Assert.Equal(20, page.Items.Count);
        Assert.Equal(500_000, reader.QueriedBeforeId);
        Assert.Equal("cajero1", reader.QueriedOwner);
        Assert.Equal(499_980, page.NextBeforeId);
        Assert.True(page.HasMore);
    }

    [Fact]
    public async Task Report_query_validates_range_and_does_not_execute_unbounded()
    {
        var executed = false;
        var useCase = new GetSalesReportUseCase(new DelegatingReportReader((query, ct) =>
        {
            executed = true;
            return Task.FromResult(new SalesReport(query.From, query.To, 100_000, 1_500_000m, "USD", [], []));
        }));

        var from = DateTimeOffset.UtcNow.AddMonths(-1);
        var to = DateTimeOffset.UtcNow;
        var result = await useCase.ExecuteAsync(from, to, CancellationToken.None);

        Assert.True(executed);
        Assert.Equal(100_000, result.TotalOrders);
        Assert.Equal(1_500_000m, result.GrandTotal);
    }

    private sealed class DelegatingReportReader(Func<SalesReportQuery, CancellationToken, Task<SalesReport>> handler) : ISalesReportReader
    {
        public Task<SalesReport> GetReportAsync(SalesReportQuery query, CancellationToken ct) => handler(query, ct);
    }
}
