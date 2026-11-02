using Pos.Application.Common;

namespace Pos.Application.Sales;

public sealed record SaleHistoryItem(int Id, string Reference, DateTimeOffset? DateUtc, decimal Total, string Currency, string State);
public sealed record SaleHistoryPage(IReadOnlyList<SaleHistoryItem> Items, bool HasMore, int? NextBeforeId);
public sealed record SaleHistoryQuery(string Search, int BeforeId, int Limit);
public interface ISaleHistoryReader
{
    Task<SaleHistoryPage> SearchAsync(SaleHistoryQuery query, CancellationToken ct);
}

/// <summary>Bounded, scoped header queries; legacy sale/detail contracts remain unchanged.</summary>
public sealed class SearchSalesHistoryUseCase(ISaleHistoryReader reader)
{
    public Task<SaleHistoryPage> ExecuteAsync(string? search, int beforeId, int limit, CancellationToken ct)
    {
        if (beforeId < 0 || limit is < 1 or > 100)
            throw new ValidationException("sales.pagination", "El cursor debe ser positivo y el límite entre 1 y 100.");
        if (search is { Length: > 100 } || search?.Any(c => char.IsControl(c) || char.GetUnicodeCategory(c) is
                System.Globalization.UnicodeCategory.Format or System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator) == true)
            throw new ValidationException("sales.search_invalid", "La búsqueda admite hasta 100 caracteres, sin controles ni texto invisible.");
        return reader.SearchAsync(new(search?.Trim() ?? string.Empty, beforeId, limit), ct);
    }
}
