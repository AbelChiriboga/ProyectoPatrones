namespace Pos.Application.Sales;

public sealed record CheckoutRequest(int SessionId, IReadOnlyList<SaleLineRequest> Lines, string? DiscountCode);
public sealed record RegisterSaleRequest(Guid OperationId, CheckoutRequest Sale, int PaymentMethodId, decimal ExpectedTotal);
public sealed record PaymentMethodDto(int Id, string Name);
public sealed record CheckoutLine(int ProductId, int Quantity, decimal UnitPrice, decimal DiscountPercent, decimal Net, decimal Tax, IReadOnlyList<int> TaxIds);
public sealed record CheckoutDto(int SessionId, string Currency, decimal Total, IReadOnlyList<CheckoutLine> Lines, IReadOnlyList<PaymentMethodDto> Methods);
public sealed record SaleDetail(int Id, Guid Uuid, string Reference, string State, string Currency, decimal Total, decimal Tax, decimal Paid, int SessionId, IReadOnlyList<CheckoutLine> Lines, IReadOnlyList<PaymentMethodDto> Methods);
public sealed record OperationResult(Guid OperationId, string Status, SaleDetail? Sale = null);
public sealed record SaleOperation(Guid Id, string Owner, string Fingerprint, RegisterSaleRequest Request);

public sealed class SaleConflictException(string message, string code = "sale.conflict") : Exception(message)
{
    public string Code { get; } = code;
}

public interface ISaleGateway
{
    Task<CheckoutDto> QuoteAsync(SalePreviewDto preview, int sessionId, CancellationToken ct);
    Task SendAsync(Guid uuid, CheckoutDto quote, int methodId, CancellationToken ct);
    Task<SaleDetail?> FindAsync(Guid uuid, CancellationToken ct);
    Task<SaleDetail?> DetailAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<SaleDetail>> HistoryAsync(int beforeId, int limit, CancellationToken ct);
}

public interface ISaleOperationStore
{
    bool TryCreate(SaleOperation operation);
    SaleOperation? Read(Guid id);
    OperationResult? Result(Guid id);
    void Confirm(OperationResult result);
}
