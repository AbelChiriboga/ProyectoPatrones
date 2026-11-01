using Microsoft.Extensions.Options;
using Pos.Infrastructure.Security;

namespace Pos.UnitTests.Infrastructure;

public sealed class ConfiguredOperatorAuthenticatorTests
{
    private static ConfiguredOperatorAuthenticator CreateAuthenticator() => new(Options.Create(new OperatorsOptions
    {
        Operators =
        [
            new OperatorCredential { Username = "cajero1", Password = "clave-segura" },
            new OperatorCredential { Username = "sin-clave", Password = "" },
        ],
    }));

    [Fact]
    public void Accepts_correct_credentials_ignoring_username_case()
    {
        Assert.True(CreateAuthenticator().IsValid("CAJERO1", "clave-segura"));
    }

    [Fact]
    public void Rejects_a_wrong_password()
    {
        Assert.False(CreateAuthenticator().IsValid("cajero1", "otra"));
    }

    [Fact]
    public void Rejects_an_unknown_user()
    {
        Assert.False(CreateAuthenticator().IsValid("nadie", "clave-segura"));
    }

    [Fact]
    public void Rejects_an_operator_without_password()
    {
        Assert.False(CreateAuthenticator().IsValid("sin-clave", ""));
    }
}
