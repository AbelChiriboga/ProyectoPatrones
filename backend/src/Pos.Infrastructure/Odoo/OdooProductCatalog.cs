using System.Text.Json;
using Pos.Application.Catalog;
using Pos.Application.Common;
using Pos.Domain.Catalog;

namespace Pos.Infrastructure.Odoo;

/// <summary>Adaptador de <see cref="IProductCatalog"/> sobre product.product de Odoo.</summary>
public sealed class OdooProductCatalog(OdooJson2Client odoo) : IProductCatalog
{
    private static readonly string[] Fields =
        ["id", "name", "default_code", "lst_price", "taxes_id", "is_storable", "qty_available"];

    public async Task<IReadOnlyList<Product>> SearchAsync(string? term, int limit, CancellationToken cancellationToken)
    {
        var result = await odoo.CallAsync("product.product", "search_read", new
        {
            domain = BuildDomain(term),
            fields = Fields,
            limit,
            order = "name asc",
        }, cancellationToken);

        if (result.ValueKind != JsonValueKind.Array)
            throw new IntegrationException(IntegrationFailureKind.InvalidResponse, "Odoo devolvió un catálogo con formato inesperado.");

        return result.EnumerateArray().Select(ToProduct).ToList();
    }

        public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return [];

        var domain = new List<object>(BuildDomain(null))
        {
            new object[] { "id", "in", ids.ToArray() },
        };

        var result = await odoo.CallAsync("product.product", "search_read", new
        {
            domain = domain.ToArray(),
            fields = Fields,
        }, cancellationToken);

        if (result.ValueKind != JsonValueKind.Array)
            throw new IntegrationException(IntegrationFailureKind.InvalidResponse, "Odoo devolvió un catálogo con formato inesperado.");

        return result.EnumerateArray().Select(ToProduct).ToList();
    }

        /// <summary>
    /// Solo productos vendibles y habilitados en el POS; búsqueda por nombre, código interno o código de barras.
    /// Se excluye "Service Fee" (código FEE), un producto auxiliar que crea el módulo POS y no es vendible.
    /// </summary>
    internal static object[] BuildDomain(string? term)
    {
        var domain = new List<object>
        {
            new object[] { "sale_ok", "=", true },
            new object[] { "available_in_pos", "=", true },
            new object[] { "default_code", "!=", "FEE" },
        };
        if (term is not null)
        {
            domain.Add("|");
            domain.Add("|");
            domain.Add(new object[] { "name", "ilike", term });
            domain.Add(new object[] { "default_code", "ilike", term });
            domain.Add(new object[] { "barcode", "=", term });
        }
        return domain.ToArray();
    }

    private static Product ToProduct(JsonElement row)
    {
        // Odoo representa los campos vacíos como false; se traducen a null en este límite (anti-corrupción).
        var code = row.GetProperty("default_code").ValueKind == JsonValueKind.String
            ? row.GetProperty("default_code").GetString()
            : null;
        var taxIds = row.GetProperty("taxes_id").EnumerateArray().Select(t => t.GetInt32()).ToList();
        decimal? quantity = row.TryGetProperty("is_storable", out var storable) && storable.ValueKind == JsonValueKind.True
            ? row.GetProperty("qty_available").GetDecimal()
            : null;

        return new Product(
            row.GetProperty("id").GetInt32(),
            code,
            row.GetProperty("name").GetString() ?? string.Empty,
            row.GetProperty("lst_price").GetDecimal(),
            taxIds,
            quantity);
    }
}
