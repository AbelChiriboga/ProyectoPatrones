using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Pos.Application.Common;
using Pos.Application.Sessions;
using Pos.Domain.Common;

namespace Pos.Application.Sales;

public sealed class SaleWorkflow(
    PreviewSaleUseCase preview,
    IPosSessionGateway sessions,
    ISaleGateway gateway,
    ISaleOperationStore store)
{
    private const int MaxQuantityPerLine = 100000;

    public async Task<CheckoutDto> QuoteAsync(CheckoutRequest request, CancellationToken ct)
    {
        ValidateCheckoutLines(request.Lines);
        var session = await sessions.FindActiveAsync(ct) ?? throw new DomainException("session.none_open", "Abra una sesión POS.");
        session.EnsureAcceptsSales();
        if (session.Id != request.SessionId)
            throw new DomainException("session.changed", "La sesión seleccionada no está abierta.");

        var salePreview = await preview.ExecuteAsync(request.Lines, request.DiscountCode, ct);
        return await gateway.QuoteAsync(salePreview, session.Id, ct);
    }

    public async Task<OperationResult> RegisterAsync(string owner, RegisterSaleRequest request, CancellationToken ct)
    {
        if (request.OperationId == Guid.Empty || request.Sale is null || request.PaymentMethodId <= 0)
            throw new ValidationException("sale.invalid_request", "Operación, venta y método de pago requeridos.");

        var (canonical, fingerprint) = Canonicalize(request);
        if (store.Read(request.OperationId) is { } existing)
            return await ResolveExistingOperationAsync(owner, fingerprint, existing, ct);

        var quote = await QuoteAsync(request.Sale, ct);
        if (!quote.Methods.Any(m => m.Id == request.PaymentMethodId))
            throw new DomainException("payment.invalid_method", "Seleccione efectivo habilitado en esta sesión.");
        if (quote.Total != request.ExpectedTotal)
            throw new SaleConflictException("El total cambió; vuelva a consultar la cotización.", "sale.total_changed");

        if (!store.TryCreate(new(request.OperationId, owner, fingerprint, canonical)))
        {
            var concurrent = store.Read(request.OperationId)
                ?? throw new SaleConflictException("No se pudo recuperar la operación concurrente.");
            return await ResolveExistingOperationAsync(owner, fingerprint, concurrent, ct);
        }

        // Durable claim precedes any network write. An abandoned claim is never sent again.
        try
        {
            await gateway.SendAsync(request.OperationId, quote, request.PaymentMethodId, ct);
            return await StatusAsync(owner, request.OperationId, ct);
        }
        catch (Exception e) when (e is IntegrationException or OperationCanceledException)
        {
            return new(request.OperationId, "uncertain");
        }
    }

    public async Task<OperationResult> StatusAsync(string owner, Guid id, CancellationToken ct)
    {
        var op = store.Read(id);
        if (op is null || op.Owner != owner)
            throw new NotFoundException("operation.not_found", "Operación no encontrada.");
        if (store.Result(id) is { } saved)
            return saved;

        try
        {
            var sale = await gateway.FindAsync(id, ct);
            if (sale is not null && MatchesOperation(sale, id, op.Request))
            {
                var result = new OperationResult(id, "confirmed", sale);
                store.Confirm(result);
                return result;
            }
        }
        catch (IntegrationException)
        {
            // Keep uncertain state on transient read failure so the operator can reconcile safely.
        }

        return new(id, "uncertain");
    }

    public Task<IReadOnlyList<SaleDetail>> HistoryAsync(int beforeId, int limit, CancellationToken ct)
    {
        if (beforeId < 0 || limit is < 1 or > 100)
            throw new ValidationException("sales.pagination", "Paginación inválida.");
        return gateway.HistoryAsync(beforeId, limit, ct);
    }

    public async Task<SaleDetail> DetailAsync(int id, CancellationToken ct) =>
        id <= 0
            ? throw new ValidationException("sales.id", "Identificador inválido.")
            : await gateway.DetailAsync(id, ct) ?? throw new NotFoundException("sales.not_found", "Venta no encontrada.");

    private static void ValidateCheckoutLines(IReadOnlyList<SaleLineRequest>? lines)
    {
        if (lines is null || lines.Count == 0)
            throw new DomainException("sale.empty", "La venta debe contener productos.");

        var hasInvalidLine = lines.Any(l => l.ProductId <= 0 || l.Quantity <= 0 || l.Quantity > MaxQuantityPerLine);
        var exceedsProductTotal = lines.GroupBy(l => l.ProductId).Any(g => g.Sum(l => (long)l.Quantity) > MaxQuantityPerLine);
        if (hasInvalidLine || exceedsProductTotal)
            throw new ValidationException("sale.invalid_lines", "Productos o cantidades inválidos.");
    }

    private static (RegisterSaleRequest Canonical, string Fingerprint) Canonicalize(RegisterSaleRequest request)
    {
        var canonical = request with
        {
            Sale = request.Sale with
            {
                Lines = request.Sale.Lines?.OrderBy(l => l.ProductId).ThenBy(l => l.Quantity).ToArray()!,
                DiscountCode = request.Sale.DiscountCode?.Trim().ToLowerInvariant() ?? "none"
            }
        };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(canonical))));
        return (canonical, fingerprint);
    }

    private Task<OperationResult> ResolveExistingOperationAsync(
        string owner, string fingerprint, SaleOperation existing, CancellationToken ct)
    {
        if (existing.Owner != owner || existing.Fingerprint != fingerprint)
            throw new SaleConflictException("El identificador ya está vinculado a otra intención de venta.");
        return StatusAsync(owner, existing.Id, ct);
    }

    private static bool MatchesOperation(SaleDetail sale, Guid id, RegisterSaleRequest request)
    {
        if (sale.Uuid != id || sale.SessionId != request.Sale.SessionId ||
            sale.State is not ("paid" or "done") ||
            sale.Total != request.ExpectedTotal || sale.Paid != sale.Total ||
            sale.Methods.Count != 1 || sale.Methods[0].Id != request.PaymentMethodId ||
            sale.Lines.Count == 0)
            return false;

        var expectedLines = AggregateQuantities(request.Sale.Lines.Select(l => (l.ProductId, l.Quantity)));
        var actualLines = AggregateQuantities(sale.Lines.Select(l => (l.ProductId, l.Quantity)));
        return actualLines.SequenceEqual(expectedLines);
    }

    private static IEnumerable<(int ProductId, long Quantity)> AggregateQuantities(IEnumerable<(int ProductId, int Quantity)> lines) =>
        lines.GroupBy(l => l.ProductId)
             .Select(g => (g.Key, g.Sum(l => (long)l.Quantity)))
             .OrderBy(item => item.Key);
}
