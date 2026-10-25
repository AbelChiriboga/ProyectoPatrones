using System.Text.Json;
using Microsoft.Extensions.Options;
using Pos.Application.Common;
using Pos.Application.Sessions;
using Pos.Domain.Sessions;

namespace Pos.Infrastructure.Odoo;

/// <summary>Adaptador de <see cref="IPosSessionGateway"/> sobre pos.session de Odoo.</summary>
public sealed class OdooPosSessionGateway(OdooJson2Client odoo, IOptions<OdooOptions> options) : IPosSessionGateway
{
    public async Task<PosSession?> FindActiveAsync(CancellationToken cancellationToken)
    {
        var domain = new List<object> { new object[] { "state", "=", "opened" } };
        if (options.Value.PosConfigId is { } configId)
            domain.Add(new object[] { "config_id", "=", configId });

        var result = await odoo.CallAsync("pos.session", "search_read", new
        {
            domain,
            fields = new[] { "id", "name", "state", "config_id", "currency_id", "payment_method_ids" },
            limit = 1,
            order = "id desc",
        }, cancellationToken);

        if (result.ValueKind != JsonValueKind.Array)
            throw new IntegrationException(IntegrationFailureKind.InvalidResponse, "Odoo devolvió sesiones con formato inesperado.");

        return result.EnumerateArray().Select(ToSession).FirstOrDefault();
    }

    internal static PosSession ToSession(JsonElement row) => new(
        row.GetProperty("id").GetInt32(),
        row.GetProperty("name").GetString() ?? string.Empty,
        ParseState(row.GetProperty("state").GetString()),
        ManyToOneId(row.GetProperty("config_id")),
        ManyToOneName(row.GetProperty("currency_id")),
        row.GetProperty("payment_method_ids").EnumerateArray().Select(p => p.GetInt32()).ToList());

    private static PosSessionState ParseState(string? state) => state switch
    {
        "opening_control" => PosSessionState.OpeningControl,
        "opened" => PosSessionState.Opened,
        "closing_control" => PosSessionState.ClosingControl,
        "closed" => PosSessionState.Closed,
        _ => throw new IntegrationException(IntegrationFailureKind.InvalidResponse, $"Estado de sesión desconocido: {state}."),
    };

    // Los many2one llegan como [id, "nombre"].
    private static int ManyToOneId(JsonElement value) => value[0].GetInt32();

    private static string ManyToOneName(JsonElement value) => value[1].GetString() ?? string.Empty;
}
