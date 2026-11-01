namespace Pos.Application.Common;

public enum IntegrationFailureKind
{
    /// <summary>No se obtuvo respuesta a tiempo: el resultado de la operación es incierto.</summary>
    Timeout,
    /// <summary>No se pudo establecer comunicación con el sistema externo.</summary>
    Unavailable,
    /// <summary>El sistema externo rechazó la petición (credenciales, permisos, validación).</summary>
    Rejected,
    /// <summary>La respuesta no tiene el formato esperado.</summary>
    InvalidResponse,
}

/// <summary>Fallo al comunicarse con un sistema externo (Odoo), expresado sin detalles de transporte.</summary>
public sealed class IntegrationException(IntegrationFailureKind kind, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public IntegrationFailureKind Kind { get; } = kind;
}

/// <summary>Datos de entrada de un caso de uso que no cumplen el contrato.</summary>
public sealed class ValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Credenciales incorrectas o ausentes (se traduce a HTTP 401).</summary>
public sealed class AuthenticationFailedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
