using System.Globalization;
using System.Text.Json;
using Pos.Application.Sales;

namespace Pos.Infrastructure.Odoo;

public sealed partial class OdooSaleGateway : ISaleHistoryReader
{
    public async Task<SaleHistoryPage> SearchAsync(SaleHistoryQuery query, CancellationToken ct)
    {
        var domain = Scope().ToList();
        if (query.BeforeId > 0) domain.Add(new object[] { "id", "<", query.BeforeId });
        if (query.Search.Length > 0)
        {
            domain.Add("|");
            domain.Add(new object[] { "name", "ilike", query.Search });
            domain.Add(new object[] { "pos_reference", "ilike", query.Search });
        }
        var rows = await Read("pos.order", domain.ToArray(),
            ["id", "name", "date_order", "amount_total", "currency_id", "state"], ct, query.Limit + 1);
        try
        {
            var items = rows.Take(query.Limit).Select(ToHistoryItem).ToArray();
            var hasMore = rows.Length > query.Limit;
            return new(items, hasMore, hasMore && items.Length > 0 ? items[^1].Id : null);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw Invalid();
        }
    }

    private static SaleHistoryItem ToHistoryItem(JsonElement row)
    {
        DateTimeOffset? date = null;
        if (row.TryGetProperty("date_order", out var value) && value.ValueKind is not (JsonValueKind.False or JsonValueKind.Null))
        {
            if (value.ValueKind != JsonValueKind.String || !DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                throw Invalid();
            date = parsed;
        }
        return new(row.GetProperty("id").GetInt32(), row.GetProperty("name").GetString() ?? string.Empty,
            date, row.GetProperty("amount_total").GetDecimal(), row.GetProperty("currency_id")[1].GetString() ?? string.Empty,
            row.GetProperty("state").GetString() ?? string.Empty);
    }
}
