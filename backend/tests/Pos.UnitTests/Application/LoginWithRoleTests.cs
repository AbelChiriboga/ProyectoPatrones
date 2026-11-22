using Pos.Application.Auth;
using Pos.Application.Common;

namespace Pos.UnitTests.Application;

/// <summary>Verifica que LoginUseCase incluye el rol en el resultado y que ITokenIssuer recibe el rol correcto.</summary>
public sealed class LoginWithRoleTests
{
    private sealed class FakeAuthenticatorWithRoles : IOperatorAuthenticator
    {
        private readonly Dictionary<string, string> _roles = new(StringComparer.OrdinalIgnoreCase)
        {
            { "admin1", OperatorRoles.Admin },
            { "cajero1", OperatorRoles.Cashier },
        };
        public bool IsValid(string username, string password) => _roles.ContainsKey(username) && password == "pass";
        public string? GetRole(string username) => _roles.TryGetValue(username, out var r) ? r : OperatorRoles.Cashier;
    }

    private sealed record IssuedCall(string Username, string Role);

    private sealed class CapturingTokenIssuer : ITokenIssuer
    {
        public IssuedCall? LastCall { get; private set; }
        public LoginResult Issue(string username, string role)
        {
            LastCall = new(username, role);
            return new("tok", DateTimeOffset.UnixEpoch, username, role);
        }
    }

    [Theory]
    [InlineData("admin1", OperatorRoles.Admin)]
    [InlineData("cajero1", OperatorRoles.Cashier)]
    public void Login_passes_correct_role_to_token_issuer(string username, string expectedRole)
    {
        var issuer = new CapturingTokenIssuer();
        var useCase = new LoginUseCase(new FakeAuthenticatorWithRoles(), issuer);

        var result = useCase.Execute(new LoginRequest(username, "pass"));

        Assert.Equal(expectedRole, result.Role);
        Assert.Equal(expectedRole, issuer.LastCall!.Role);
    }

    [Fact]
    public void Invalid_credentials_do_not_issue_token()
    {
        var issuer = new CapturingTokenIssuer();
        var useCase = new LoginUseCase(new FakeAuthenticatorWithRoles(), issuer);

        Assert.Throws<AuthenticationFailedException>(() => useCase.Execute(new LoginRequest("admin1", "wrong")));
        Assert.Null(issuer.LastCall);
    }
}
