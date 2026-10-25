using Pos.Application.Common;
using Pos.Domain.Sessions;

namespace Pos.Application.Sessions;

/// <summary>Puerto de salida: sesiones de caja del sistema POS (implementado contra Odoo).</summary>
public interface IPosSessionGateway
{
    Task<PosSession?> FindActiveAsync(CancellationToken cancellationToken);
}

public sealed record PosSessionDto(int Id, string Name, string State, int ConfigId, string CurrencyCode, IReadOnlyList<int> PaymentMethodIds)
{
    public static PosSessionDto From(PosSession session) =>
        new(session.Id, session.Name, session.State.ToString(), session.ConfigId, session.CurrencyCode, session.PaymentMethodIds);
}

/// <summary>Consulta la sesión de caja abierta en la que se registrarán las ventas.</summary>
public sealed class GetActiveSessionUseCase(IPosSessionGateway sessions)
{
    public async Task<PosSessionDto> ExecuteAsync(CancellationToken cancellationToken)
    {
        var session = await sessions.FindActiveAsync(cancellationToken)
            ?? throw new NotFoundException("session.none_open", "No hay una sesión POS abierta en Odoo. Abra la caja antes de vender.");

        session.EnsureAcceptsSales();
        return PosSessionDto.From(session);
    }
}
