using Pos.Application.Common;

namespace Pos.Application.Reports;

/// <summary>Resumen de ventas por cajero para el reporte consolidado.</summary>
public sealed record CashierSummary(string Cashier, int OrderCount, decimal Total);

/// <summary>Desglose de ventas por método de pago.</summary>
public sealed record PaymentMethodSummary(string Method, int Count, decimal Total);

/// <summary>Reporte global de ventas consolidadas (solo Administrador).</summary>
public sealed record SalesReport(
    DateTimeOffset? From,
    DateTimeOffset? To,
    int TotalOrders,
    decimal GrandTotal,
    string Currency,
    IReadOnlyList<CashierSummary> ByCashier,
    IReadOnlyList<PaymentMethodSummary> ByPaymentMethod);

/// <summary>Parámetros de consulta del reporte.</summary>
public sealed record SalesReportQuery(DateTimeOffset? From, DateTimeOffset? To);

/// <summary>Puerto de salida: obtiene datos para el reporte global de ventas.</summary>
public interface ISalesReportReader
{
    Task<SalesReport> GetReportAsync(SalesReportQuery query, CancellationToken ct);
}

/// <summary>Caso de uso: genera reporte global de ventas (RF-Admin-01).</summary>
public sealed class GetSalesReportUseCase(ISalesReportReader reader)
{
    public Task<SalesReport> ExecuteAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ValidationException("report.invalid_range", "La fecha de inicio debe ser anterior a la de fin.");
        return reader.GetReportAsync(new SalesReportQuery(from, to), ct);
    }
}
