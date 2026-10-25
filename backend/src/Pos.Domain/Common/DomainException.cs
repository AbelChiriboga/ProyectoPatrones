namespace Pos.Domain.Common;

/// <summary>Violación de una regla de negocio del POS.</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
