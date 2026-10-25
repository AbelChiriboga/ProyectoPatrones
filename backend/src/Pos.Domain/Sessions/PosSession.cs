using Pos.Domain.Common;

namespace Pos.Domain.Sessions;

public enum PosSessionState
{
    OpeningControl,
    Opened,
    ClosingControl,
    Closed,
}

/// <summary>Sesión de caja de Odoo en la que se registran las ventas.</summary>
public sealed class PosSession
{
    public PosSession(int id, string name, PosSessionState state, int configId, string currencyCode, IReadOnlyList<int> paymentMethodIds)
    {
        if (id <= 0)
            throw new DomainException("session.invalid_id", "El identificador de la sesión debe ser positivo.");

        Id = id;
        Name = name;
        State = state;
        ConfigId = configId;
        CurrencyCode = currencyCode;
        PaymentMethodIds = paymentMethodIds;
    }

    public int Id { get; }
    public string Name { get; }
    public PosSessionState State { get; }
    public int ConfigId { get; }
    public string CurrencyCode { get; }
    public IReadOnlyList<int> PaymentMethodIds { get; }

    public bool AcceptsSales => State == PosSessionState.Opened;

    /// <summary>Garantiza que la sesión admite ventas (base de la prueba P03).</summary>
    public void EnsureAcceptsSales()
    {
        if (!AcceptsSales)
            throw new DomainException("session.not_open", $"La sesión {Name} no está abierta para registrar ventas.");
    }
}
