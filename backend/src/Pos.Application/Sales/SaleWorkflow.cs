using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pos.Application.Common;
using Pos.Application.Sessions;
using Pos.Domain.Common;

namespace Pos.Application.Sales;

public sealed record CheckoutRequest(int SessionId, IReadOnlyList<SaleLineRequest> Lines, string? DiscountCode);
public sealed record RegisterSaleRequest(Guid OperationId, CheckoutRequest Sale, int PaymentMethodId, decimal ExpectedTotal);
public sealed record PaymentMethodDto(int Id, string Name);
public sealed record CheckoutLine(int ProductId, int Quantity, decimal UnitPrice, decimal DiscountPercent, decimal Net, decimal Tax, IReadOnlyList<int> TaxIds);
public sealed record CheckoutDto(int SessionId, string Currency, decimal Total, IReadOnlyList<CheckoutLine> Lines, IReadOnlyList<PaymentMethodDto> Methods);
public sealed record SaleDetail(int Id, Guid Uuid, string Reference, string State, string Currency, decimal Total, decimal Tax, decimal Paid, int SessionId, IReadOnlyList<CheckoutLine> Lines, IReadOnlyList<PaymentMethodDto> Methods);
public sealed record OperationResult(Guid OperationId, string Status, SaleDetail? Sale = null);
public sealed record SaleOperation(Guid Id, string Owner, string Fingerprint, RegisterSaleRequest Request);
public sealed class SaleConflictException(string message, string code = "sale.conflict") : Exception(message) { public string Code { get; } = code; }
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

public sealed class SaleWorkflow(PreviewSaleUseCase preview, IPosSessionGateway sessions, ISaleGateway gateway, ISaleOperationStore store)
{
    public async Task<CheckoutDto> QuoteAsync(CheckoutRequest request, CancellationToken ct)
    {
        if (request.Lines is null || request.Lines.Count == 0)
            throw new DomainException("sale.empty", "La venta debe contener productos.");
        if (request.Lines.Any(l => l.ProductId <= 0 || l.Quantity <= 0 || l.Quantity > 100000) ||
            request.Lines.GroupBy(l => l.ProductId).Any(g => g.Sum(l => (long)l.Quantity) > 100000))
            throw new ValidationException("sale.invalid_lines", "Productos o cantidades inválidos.");
        var session = await sessions.FindActiveAsync(ct) ?? throw new DomainException("session.none_open", "Abra una sesión POS.");
        session.EnsureAcceptsSales();
        if (session.Id != request.SessionId) throw new DomainException("session.changed", "La sesión seleccionada no está abierta.");
        return await gateway.QuoteAsync(await preview.ExecuteAsync(request.Lines, request.DiscountCode, ct), session.Id, ct);
    }
    public async Task<OperationResult> RegisterAsync(string owner, RegisterSaleRequest request, CancellationToken ct)
    {
        if (request.OperationId == Guid.Empty || request.Sale is null || request.PaymentMethodId <= 0)
            throw new ValidationException("sale.invalid_request", "Operación, venta y método de pago requeridos.");
        var canonical = request with { Sale = request.Sale with {
            Lines = request.Sale.Lines?.OrderBy(l => l.ProductId).ThenBy(l => l.Quantity).ToArray()!,
            DiscountCode = request.Sale.DiscountCode?.Trim().ToLowerInvariant() ?? "none" } };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
        var existing = store.Read(request.OperationId);
        if (existing is not null) {
            if (existing.Owner != owner || existing.Fingerprint != fingerprint)
                throw new SaleConflictException("El identificador ya está vinculado a otra intención de venta.");
            return await StatusAsync(owner, request.OperationId, ct);
        }
        var quote = await QuoteAsync(request.Sale, ct);
        if (!quote.Methods.Any(m => m.Id == request.PaymentMethodId))
            throw new DomainException("payment.invalid_method", "Seleccione efectivo habilitado en esta sesión.");
        if (quote.Total != request.ExpectedTotal)
            throw new SaleConflictException("El total cambió; vuelva a consultar la cotización.", "sale.total_changed");
        if (!store.TryCreate(new(request.OperationId, owner, fingerprint, canonical)))
            return await RegisterAsync(owner, request, ct);
        // Durable claim precedes any network write. An abandoned claim is never sent again.
        try {
            await gateway.SendAsync(request.OperationId, quote, request.PaymentMethodId, ct);
            return await StatusAsync(owner, request.OperationId, ct);
        } catch (Exception e) when (e is IntegrationException or OperationCanceledException) {
            return new(request.OperationId, "uncertain");
        }
    }
    public async Task<OperationResult> StatusAsync(string owner, Guid id, CancellationToken ct)
    {
        var op = store.Read(id);
        if (op is null || op.Owner != owner) throw new NotFoundException("operation.not_found", "Operación no encontrada.");
        if (store.Result(id) is { } saved) return saved;
        try {
            var sale = await gateway.FindAsync(id, ct);
            if (sale is not null && sale.Uuid == id && sale.SessionId == op.Request.Sale.SessionId &&
                sale.State is "paid" or "done" && sale.Total == op.Request.ExpectedTotal && sale.Paid == sale.Total &&
                sale.Methods.Count == 1 && sale.Methods[0].Id == op.Request.PaymentMethodId && sale.Lines.Count > 0 &&
                sale.Lines.GroupBy(l=>l.ProductId).Select(g=>(g.Key,Qty:g.Sum(l=>(long)l.Quantity))).OrderBy(l=>l.Key)
                    .SequenceEqual(op.Request.Sale.Lines.GroupBy(l=>l.ProductId).Select(g=>(g.Key,Qty:g.Sum(l=>(long)l.Quantity))).OrderBy(l=>l.Key))) {
                var result = new OperationResult(id, "confirmed", sale);
                store.Confirm(result);
                return result;
            }
        } catch (IntegrationException) { }
        return new(id, "uncertain");
    }
    public Task<IReadOnlyList<SaleDetail>> HistoryAsync(int beforeId, int limit, CancellationToken ct) {
        if (beforeId < 0 || limit is < 1 or > 100) throw new ValidationException("sales.pagination", "Paginación inválida.");
        return gateway.HistoryAsync(beforeId, limit, ct);
    }
    public async Task<SaleDetail> DetailAsync(int id, CancellationToken ct) =>
        id <= 0 ? throw new ValidationException("sales.id", "Identificador inválido.") :
        await gateway.DetailAsync(id, ct) ?? throw new NotFoundException("sales.not_found", "Venta no encontrada.");
}
