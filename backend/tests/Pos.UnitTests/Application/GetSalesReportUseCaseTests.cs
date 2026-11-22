using Pos.Application.Common;
using Pos.Application.Reports;

namespace Pos.UnitTests.Application;

/// <summary>Verifica GetSalesReportUseCase: validación de rangos de fecha y delegación al reader.</summary>
public sealed class GetSalesReportUseCaseTests
{
    private static readonly SalesReport EmptyReport = new(null, null, 0, 0m, "USD", [], []);

    private sealed class StubReportReader : ISalesReportReader
    {
        public SalesReportQuery? LastQuery { get; private set; }
        public SalesReport Response { get; set; } = EmptyReport;

        public Task<SalesReport> GetReportAsync(SalesReportQuery query, CancellationToken ct)
        {
            LastQuery = query;
            return Task.FromResult(Response);
        }
    }

    private static GetSalesReportUseCase CreateUseCase(out StubReportReader reader)
    {
        reader = new StubReportReader();
        return new GetSalesReportUseCase(reader);
    }

    [Fact]
    public async Task Returns_report_when_no_date_filter()
    {
        var useCase = CreateUseCase(out var reader);
        var result = await useCase.ExecuteAsync(null, null, default);
        Assert.Equal(EmptyReport, result);
        Assert.Null(reader.LastQuery!.From);
        Assert.Null(reader.LastQuery.To);
    }

    [Fact]
    public async Task Passes_date_range_to_reader()
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var useCase = CreateUseCase(out var reader);

        await useCase.ExecuteAsync(from, to, default);

        Assert.Equal(from, reader.LastQuery!.From);
        Assert.Equal(to, reader.LastQuery.To);
    }

    [Fact]
    public async Task Rejects_inverted_date_range()
    {
        var useCase = CreateUseCase(out _);
        var from = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(from, to, default));
        Assert.Equal("report.invalid_range", ex.Code);
    }

    [Fact]
    public async Task Accepts_same_date_for_from_and_to()
    {
        var useCase = CreateUseCase(out var reader);
        var date = DateTimeOffset.UtcNow;

        await useCase.ExecuteAsync(date, date, default);
        Assert.NotNull(reader.LastQuery);
    }

    [Fact]
    public async Task Report_aggregates_cashiers_correctly()
    {
        var useCase = CreateUseCase(out var reader);
        reader.Response = new SalesReport(null, null, 3, 300m, "USD",
        [
            new CashierSummary("cajero1", 2, 200m),
            new CashierSummary("cajero2", 1, 100m),
        ],
        [
            new PaymentMethodSummary("Efectivo", 3, 300m),
        ]);

        var result = await useCase.ExecuteAsync(null, null, default);

        Assert.Equal(3, result.TotalOrders);
        Assert.Equal(300m, result.GrandTotal);
        Assert.Equal(2, result.ByCashier.Count);
        Assert.Single(result.ByPaymentMethod);
        Assert.Equal("cajero1", result.ByCashier[0].Cashier);
        Assert.Equal(200m, result.ByCashier[0].Total);
    }
}
