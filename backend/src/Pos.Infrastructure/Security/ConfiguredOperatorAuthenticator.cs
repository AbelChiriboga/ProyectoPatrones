using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Pos.Application.Auth;

namespace Pos.Infrastructure.Security;

public sealed class OperatorCredential
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Operadores del POS definidos por configuración (sección "Auth"). Las contraseñas viven en
/// user-secrets o variables de entorno, nunca en el repositorio.
/// </summary>
public sealed class OperatorsOptions
{
    public const string SectionName = "Auth";

    public List<OperatorCredential> Operators { get; set; } = [];
}

/// <summary>Adaptador de <see cref="IOperatorAuthenticator"/> sobre la configuración.</summary>
public sealed class ConfiguredOperatorAuthenticator(IOptions<OperatorsOptions> options) : IOperatorAuthenticator
{
    public bool IsValid(string username, string password)
    {
        var found = options.Value.Operators.FirstOrDefault(o =>
            string.Equals(o.Username, username, StringComparison.OrdinalIgnoreCase));
        if (found is null || found.Password.Length == 0)
            return false;

        // Comparación de tiempo constante para no filtrar información por la duración de la respuesta.
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(found.Password), Encoding.UTF8.GetBytes(password));
    }
}
