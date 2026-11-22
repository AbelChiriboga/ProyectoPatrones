using System.Globalization;
using System.Text.Json;
using Pos.Application.Common;
using Pos.Application.Reports;

namespace Pos.Infrastructure.Odoo;

/// <summary>
/// Adaptador de <see cref="ISalesReportReader"/> sobre Odoo JSON-2.
/// Consulta pos.order con filtro de fechas y el POS configurado; agrupa por cajero y método de pago.
/// No ejecuta ventas ni modifica datos.
/// </summary>
public sealed class OdooSalesReportGateway(OdooJson2Client client, Microsoft.Extensions.Options.IOptions<OdooOptions> options) : ISalesReportReader
{
    private int ConfigId => options.Value.PosConfigId
        ?? throw new ValidationException("report.scope_required", "Configure Odoo:PosConfigId para habilitar reportes.");

    public async Task<SalesReport> GetReportAsync(SalesReportQuery query, CancellationToken ct)
    {
        var domain = BuildDomain(query);

        // Cabeceras de órdenes: necesitamos cajero (employee_id o user_id), total y moneda.
        var orders = await ReadAsync("pos.order", domain,
            ["id", "name", "amount_total", "currency_id", "employee_id", "user_id"], ct);

        if (orders.Length == 0)
        {
            var emptyCurrency = await ReadDefaultCurrencyAsync(ct);
            return new SalesReport(query.From, query.To, 0, 0m, emptyCurrency, [], []);
        }

        // Pagos de las órdenes del rango
        var orderIds = orders.Select(o => o.GetProperty("id").GetInt32()).ToArray();
        var payments = await ReadAsync("pos.payment",
            [new object[] { "pos_order_id", "in", orderIds }],
            ["pos_order_id", "payment_method_id", "amount"], ct);

        try
        {
            return BuildReport(query, orders, payments);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new IntegrationException(IntegrationFailureKind.InvalidResponse,
                "Respuesta del reporte POS incompleta o inconsistente.");
        }
    }

    private object[] BuildDomain(SalesReportQuery query)
    {
        var domain = new List<object> { new object[] { "config_id", "=", ConfigId } };
        if (query.From.HasValue)
            domain.Add(new object[] { "date_order", ">=", query.From.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) });
        if (query.To.HasValue)
            domain.Add(new object[] { "date_order", "<=", query.To.Value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) });
        return domain.ToArray();
    }

    private static SalesReport BuildReport(SalesReportQuery query, JsonElement[] orders, JsonElement[] payments)
    {
        var currency = orders[0].GetProperty("currency_id")[1].GetString() ?? string.Empty;
        var grandTotal = orders.Sum(o => o.GetProperty("amount_total").GetDecimal());

        // Agrupación por cajero
        var byCashier = orders
            .GroupBy(o => GetCashierName(o))
            .Select(g => new CashierSummary(
                g.Key,
                g.Count(),
                g.Sum(o => o.GetProperty("amount_total").GetDecimal())))
            .OrderBy(s => s.Cashier)
            .ToArray();

        // Agrupación por método de pago
        var byMethod = payments
            .GroupBy(p => p.GetProperty("payment_method_id")[1].GetString() ?? "Desconocido")
            .Select(g => new PaymentMethodSummary(
                g.Key,
                g.Count(),
                g.Sum(p => p.GetProperty("amount").GetDecimal())))
            .OrderBy(s => s.Method)
            .ToArray();

        return new SalesReport(query.From, query.To, orders.Length, grandTotal, currency, byCashier, byMethod);
    }

    private static string GetCashierName(JsonElement order)
    {
        // Odoo puede usar employee_id o user_id según configuración del POS
        if (order.TryGetProperty("employee_id", out var emp) &&
            emp.ValueKind == JsonValueKind.Array && emp.GetArrayLength() == 2)
            return emp[1].GetString() ?? "Desconocido";
        if (order.TryGetProperty("user_id", out var user) &&
            user.ValueKind == JsonValueKind.Array && user.GetArrayLength() == 2)
            return user[1].GetString() ?? "Desconocido";
        return "Desconocido";
    }

    private async Task<string> ReadDefaultCurrencyAsync(CancellationToken ct)
    {
        // Intenta obtener la moneda de la configuración del POS para devolver un reporte vacío coherente.
        try
        {
            var configs = await ReadAsync("pos.config",
                [new object[] { "id", "=", ConfigId }],
                ["currency_id"], ct, 1);
            if (configs.Length > 0)
                return configs[0].GetProperty("currency_id")[1].GetString() ?? string.Empty;
        }
        catch { /* No bloquear el reporte vacío por esto */ }
        return string.Empty;
    }

    private async Task<JsonElement[]> ReadAsync(string model, object[] domain, string[] fields, CancellationToken ct, int limit = 1000)
    {
        var json = await client.CallAsync(model, "search_read", new { domain, fields, limit, order = "id desc" }, ct);
        if (json.ValueKind != JsonValueKind.Array)
            throw new IntegrationException(IntegrationFailureKind.InvalidResponse, "Respuesta POS incompleta o inconsistente.");
        return json.EnumerateArray().ToArray();
    }
}
