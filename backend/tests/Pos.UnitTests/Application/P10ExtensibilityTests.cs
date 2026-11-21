using Pos.Application.Catalog;
using Pos.Application.Sales;
using Pos.Application.Sessions;
using Pos.Domain.Catalog;
using Pos.Domain.Discounts;
using Pos.Domain.Sessions;

namespace Pos.UnitTests.Application;

/// <summary>
/// P10: demuestra cambios controlados (nueva política de descuento y adaptador de integración alternativo)
/// sin modificar casos de uso, dominio ni controladores existentes.
/// </summary>
public sealed class P10ExtensibilityTests
{
    private sealed class Vip20DiscountPolicy : IDiscountPolicy
    {
        public string Code => "vip-20";
        public string Name => "Descuento VIP 20 %";
        public decimal Calculate(decimal subtotal) => Math.Round(subtotal * 0.20m, 2, MidpointRounding.AwayFromZero);
    }

    private sealed class StubCatalog : IProductCatalog
    {
        public Task<IReadOnlyList<Product>> SearchAsync(string? term, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Product>>([]);

        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Product>>([new Product(11, "CAFE-001", "Café americano", 10.00m, [1], null)]);
    }

    private sealed class OpenSessionGateway : IPosSessionGateway
    {
        public Task<PosSession?> FindActiveAsync(CancellationToken cancellationToken) =>
            Task.FromResult<PosSession?>(new PosSession(1, "POS/0001", PosSessionState.Opened, 1, "USD", [1]));
    }

    private sealed class AlternativeErpGateway : ISaleGateway
    {
        public int SentCount { get; private set; }

        public Task<CheckoutDto> QuoteAsync(SalePreviewDto preview, int sessionId, CancellationToken ct) =>
            Task.FromResult(new CheckoutDto(
                sessionId,
                "USD",
                preview.Total,
                preview.Lines.Select(l => new CheckoutLine(l.ProductId, l.Quantity, l.UnitPrice, 0m, l.Subtotal, 0m, [])).ToArray(),
                [new PaymentMethodDto(1, "Efectivo alternativo")]));

        public Task SendAsync(Guid uuid, CheckoutDto quote, int methodId, CancellationToken ct)
        {
            SentCount++;
            return Task.CompletedTask;
        }

        public Task<SaleDetail?> FindAsync(Guid uuid, CancellationToken ct) =>
            Task.FromResult<SaleDetail?>(new SaleDetail(
                99, uuid, "ALT-00099", "paid", "USD", 10.00m, 0m, 10.00m, 1,
                [new CheckoutLine(11, 1, 10.00m, 0m, 10.00m, 0m, [])],
                [new PaymentMethodDto(1, "Efectivo alternativo")]));

        public Task<SaleDetail?> DetailAsync(int id, CancellationToken ct) => Task.FromResult<SaleDetail?>(null);
        public Task<IReadOnlyList<SaleDetail>> HistoryAsync(int beforeId, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SaleDetail>>([]);
    }

    private sealed class MemoryOperationStore : ISaleOperationStore
    {
        private readonly Dictionary<Guid, SaleOperation> _ops = [];
        private readonly Dictionary<Guid, OperationResult> _results = [];

        public bool TryCreate(SaleOperation operation) => _ops.TryAdd(operation.Id, operation);
        public SaleOperation? Read(Guid id) => _ops.GetValueOrDefault(id);
        public OperationResult? Result(Guid id) => _results.GetValueOrDefault(id);
        public void Confirm(OperationResult result) => _results[result.OperationId] = result;
    }

    [Fact]
    public async Task Adding_new_discount_policy_works_in_preview_and_workflow_without_modifying_use_cases()
    {
        var registry = new DiscountPolicyRegistry([new NoDiscountPolicy(), new Vip20DiscountPolicy()]);
        var previewUseCase = new PreviewSaleUseCase(new StubCatalog(), registry);
        var gateway = new AlternativeErpGateway();
        var workflow = new SaleWorkflow(previewUseCase, new OpenSessionGateway(), gateway, new MemoryOperationStore());

        var preview = await previewUseCase.ExecuteAsync([new SaleLineRequest(11, 2)], "vip-20", CancellationToken.None);
        var quote = await workflow.QuoteAsync(new CheckoutRequest(1, [new SaleLineRequest(11, 2)], "vip-20"), CancellationToken.None);

        Assert.Equal("vip-20", preview.DiscountCode);
        Assert.Equal(4.00m, preview.Discount);
        Assert.Equal(16.00m, preview.Total);
        Assert.Equal(16.00m, quote.Total);
    }

    [Fact]
    public async Task Swapping_sale_gateway_adapter_works_without_modifying_workflow_or_domain()
    {
        var previewUseCase = new PreviewSaleUseCase(new StubCatalog(), new DiscountPolicyRegistry([new NoDiscountPolicy()]));
        var alternativeGateway = new AlternativeErpGateway();
        var workflow = new SaleWorkflow(previewUseCase, new OpenSessionGateway(), alternativeGateway, new MemoryOperationStore());

        var operationId = Guid.NewGuid();
        var result = await workflow.RegisterAsync(
            "cajero1",
            new RegisterSaleRequest(operationId, new CheckoutRequest(1, [new SaleLineRequest(11, 1)], null), 1, 10.00m),
            CancellationToken.None);

        Assert.Equal("confirmed", result.Status);
        Assert.Equal("ALT-00099", result.Sale?.Reference);
        Assert.Equal(1, alternativeGateway.SentCount);
    }
}
