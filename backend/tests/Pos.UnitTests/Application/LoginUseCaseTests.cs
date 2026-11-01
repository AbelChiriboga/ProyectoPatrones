using Pos.Application.Auth;
using Pos.Application.Common;

namespace Pos.UnitTests.Application;

public sealed class LoginUseCaseTests
{
    private sealed class FakeAuthenticator : IOperatorAuthenticator
    {
        public bool IsValid(string username, string password) => username == "cajero1" && password == "clave-segura";
    }

    private sealed class FakeTokenIssuer : ITokenIssuer
    {
        public LoginResult Issue(string username) => new("token-de-prueba", DateTimeOffset.UnixEpoch, username);
    }

    private static LoginUseCase CreateUseCase() => new(new FakeAuthenticator(), new FakeTokenIssuer());

    [Fact]
    public void Valid_credentials_return_a_token()
    {
        var result = CreateUseCase().Execute(new LoginRequest(" cajero1 ", "clave-segura"));

        Assert.Equal("token-de-prueba", result.Token);
        Assert.Equal("cajero1", result.Username);
    }

    [Fact]
    public void Wrong_password_is_rejected()
    {
        var error = Assert.Throws<AuthenticationFailedException>(() =>
            CreateUseCase().Execute(new LoginRequest("cajero1", "otra")));

        Assert.Equal("auth.invalid_credentials", error.Code);
    }

    [Theory]
    [InlineData(null, "clave-segura")]
    [InlineData("  ", "clave-segura")]
    [InlineData("cajero1", null)]
    [InlineData("cajero1", "")]
    public void Missing_credentials_are_rejected(string? username, string? password)
    {
        var error = Assert.Throws<ValidationException>(() =>
            CreateUseCase().Execute(new LoginRequest(username, password)));

        Assert.Equal("auth.credentials_required", error.Code);
    }
}
