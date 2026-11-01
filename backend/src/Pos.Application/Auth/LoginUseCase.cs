using Pos.Application.Common;

namespace Pos.Application.Auth;

/// <summary>Puerto de salida: verifica las credenciales de un operador del POS.</summary>
public interface IOperatorAuthenticator
{
    bool IsValid(string username, string password);
}

/// <summary>Puerto de salida: emite el token de acceso del operador.</summary>
public interface ITokenIssuer
{
    LoginResult Issue(string username);
}

public sealed record LoginRequest(string? Username, string? Password);

public sealed record LoginResult(string Token, DateTimeOffset ExpiresAt, string Username);

/// <summary>Autenticación del operador: POST /api/auth/login.</summary>
public sealed class LoginUseCase(IOperatorAuthenticator authenticator, ITokenIssuer tokens)
{
    public LoginResult Execute(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrEmpty(request.Password))
            throw new ValidationException("auth.credentials_required", "Ingrese usuario y contraseña.");

        var username = request.Username.Trim();
        if (!authenticator.IsValid(username, request.Password))
            throw new AuthenticationFailedException("auth.invalid_credentials", "Usuario o contraseña incorrectos.");

        return tokens.Issue(username);
    }
}
