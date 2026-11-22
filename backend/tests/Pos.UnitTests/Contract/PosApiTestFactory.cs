using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Auth;
using Pos.Application.Catalog;
using Pos.Application.Reports;
using Pos.Application.Sales;
using Pos.Application.Sessions;
using Pos.Domain.Catalog;
using Pos.Domain.Sessions;

namespace Pos.UnitTests.Contract;

public class PosApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestJwtKey = "a-very-secret-test-jwt-key-with-sufficient-length-2026!";
    public const string AdminUser = "admin";
    public const string AdminPass = "admin123";
    public const string CashierUser = "cajero1";
    public const string CashierPass = "cajero123";
    public const string Cashier2User = "cajero2";
    public const string Cashier2Pass = "cajero2_pass";

    private readonly string _testJournalPath = Path.Combine(Path.GetTempPath(), "pos-test-journal-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = TestJwtKey,
                ["Jwt:Issuer"] = "pos-api",
                ["Jwt:Audience"] = "pos-frontend",
                ["Jwt:ExpirationMinutes"] = "60",
                ["Odoo:BaseUrl"] = "http://localhost:8069",
                ["Odoo:Database"] = "pos_db",
                ["Odoo:ApiKey"] = "test-odoo-key",
                ["Sales:JournalPath"] = _testJournalPath,
                ["Auth:Operators:0:Username"] = AdminUser,
                ["Auth:Operators:0:Password"] = AdminPass,
                ["Auth:Operators:0:Role"] = OperatorRoles.Admin,
                ["Auth:Operators:1:Username"] = CashierUser,
                ["Auth:Operators:1:Password"] = CashierPass,
                ["Auth:Operators:1:Role"] = OperatorRoles.Cashier,
                ["Auth:Operators:2:Username"] = Cashier2User,
                ["Auth:Operators:2:Password"] = Cashier2Pass,
                ["Auth:Operators:2:Role"] = OperatorRoles.Cashier,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Catalog stub
            services.AddScoped<IProductCatalog, FakeProductCatalog>();

            // Session stub
            services.AddScoped<IPosSessionGateway, FakePosSessionGateway>();

            // Report stub
            services.AddScoped<ISalesReportReader, FakeSalesReportReader>();

            // Sale history stub
            services.AddScoped<ISaleHistoryReader, FakeSaleHistoryReader>();

            // Sale gateway stub
            services.AddScoped<ISaleGateway, FakeSaleGateway>();
        });
    }

    private sealed class FakeProductCatalog : IProductCatalog
    {
        public Task<IReadOnlyList<Product>> SearchAsync(string? term, int limit, CancellationToken cancellationToken)
        {
            var list = new List<Product>
            {
                new(1, "PROD-01", "Mesa de Roble", 120.00m, [1], 15m),
                new(2, "PROD-02", "Silla Confort", 45.50m, [1], 30m),
            };

            if (!string.IsNullOrWhiteSpace(term))
            {
                list = list.Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                       (p.Code != null && p.Code.Contains(term, StringComparison.OrdinalIgnoreCase))).ToList();
            }

            return Task.FromResult<IReadOnlyList<Product>>(list.Take(limit).ToList());
        }

        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
        {
            var all = new List<Product>
            {
                new(1, "PROD-01", "Mesa de Roble", 120.00m, [1], 15m),
                new(2, "PROD-02", "Silla Confort", 45.50m, [1], 30m),
            };
            return Task.FromResult<IReadOnlyList<Product>>(all.Where(p => ids.Contains(p.Id)).ToList());
        }
    }

    private sealed class FakePosSessionGateway : IPosSessionGateway
    {
        public Task<PosSession?> FindActiveAsync(CancellationToken cancellationToken)
        {
            var session = new PosSession(10, "POS/2026/001", PosSessionState.Opened, 1, "USD", [1, 2]);
            return Task.FromResult<PosSession?>(session);
        }
    }

    private sealed class FakeSalesReportReader : ISalesReportReader
    {
        public Task<SalesReport> GetReportAsync(SalesReportQuery query, CancellationToken ct)
        {
            var report = new SalesReport(
                query.From,
                query.To,
                TotalOrders: 10,
                GrandTotal: 1500.00m,
                Currency: "USD",
                ByCashier: [new CashierSummary("cajero1", 10, 1500.00m)],
                ByPaymentMethod: [new PaymentMethodSummary("Cash", 10, 1500.00m)]
            );
            return Task.FromResult(report);
        }
    }

    private sealed class FakeSaleHistoryReader : ISaleHistoryReader
    {
        public Task<SaleHistoryPage> SearchAsync(SaleHistoryQuery query, CancellationToken ct)
        {
            var items = new List<SaleHistoryItem>
            {
                new(101, "ORDER/2026/001", DateTimeOffset.UtcNow, 120.00m, "USD", "paid")
            };
            return Task.FromResult(new SaleHistoryPage(items, false, null));
        }
    }

    private sealed class FakeSaleGateway : ISaleGateway
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SaleDetail> _sentSales = new();

        public Task<CheckoutDto> QuoteAsync(SalePreviewDto preview, int sessionId, CancellationToken ct)
        {
            var quote = new CheckoutDto(
                sessionId,
                "USD",
                preview.Total,
                preview.Lines.Select(l => new CheckoutLine(l.ProductId, l.Quantity, l.UnitPrice, 0m, l.Subtotal, 0m, [1])).ToList(),
                [new PaymentMethodDto(1, "Cash")]
            );
            return Task.FromResult(quote);
        }

        public Task SendAsync(Guid uuid, CheckoutDto quote, int methodId, CancellationToken ct)
        {
            var detail = new SaleDetail(
                100 + _sentSales.Count,
                uuid,
                $"ORDER/{100 + _sentSales.Count}",
                "paid",
                quote.Currency,
                quote.Total,
                0m,
                quote.Total,
                quote.SessionId,
                quote.Lines,
                quote.Methods.Where(m => m.Id == methodId).ToList()
            );
            _sentSales[uuid] = detail;
            return Task.CompletedTask;
        }

        public Task<SaleDetail?> FindAsync(Guid uuid, CancellationToken ct) =>
            Task.FromResult(_sentSales.TryGetValue(uuid, out var s) ? s : null);

        public Task<SaleDetail?> DetailAsync(int id, CancellationToken ct)
        {
            if (id == 999999)
                return Task.FromResult<SaleDetail?>(null);

            var detail = new SaleDetail(
                id,
                Guid.NewGuid(),
                $"ORDER/{id}",
                "paid",
                "USD",
                120.00m,
                15.00m,
                120.00m,
                10,
                [new CheckoutLine(1, 1, 120.00m, 0m, 120.00m, 15.00m, [1])],
                [new PaymentMethodDto(1, "Cash")]
            );
            return Task.FromResult<SaleDetail?>(detail);
        }

        public Task<IReadOnlyList<SaleDetail>> HistoryAsync(int beforeId, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SaleDetail>>([]);
    }
}
