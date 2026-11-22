using Microsoft.Extensions.Options;
using Pos.Application.Auth;
using Pos.Infrastructure.Security;

namespace Pos.UnitTests.Infrastructure;

/// <summary>Verifica que ConfiguredOperatorAuthenticator expone el rol correcto por usuario.</summary>
public sealed class OperatorRoleTests
{
    private static ConfiguredOperatorAuthenticator CreateAuthenticator() => new(Options.Create(new OperatorsOptions
    {
        Operators =
        [
            new OperatorCredential { Username = "admin1", Password = "admin-pass", Role = OperatorRoles.Admin },
            new OperatorCredential { Username = "cajero1", Password = "clave-segura", Role = OperatorRoles.Cashier },
            new OperatorCredential { Username = "sin-rol", Password = "x" }, // debería defaultear a Cashier
        ],
    }));

    [Fact]
    public void Admin_operator_returns_Admin_role()
    {
        Assert.Equal(OperatorRoles.Admin, CreateAuthenticator().GetRole("admin1"));
    }

    [Fact]
    public void Cashier_operator_returns_Cashier_role()
    {
        Assert.Equal(OperatorRoles.Cashier, CreateAuthenticator().GetRole("cajero1"));
    }

    [Fact]
    public void Operator_without_explicit_role_defaults_to_Cashier()
    {
        Assert.Equal(OperatorRoles.Cashier, CreateAuthenticator().GetRole("sin-rol"));
    }

    [Fact]
    public void Unknown_user_returns_Cashier_as_default()
    {
        // Por seguridad: usuario inexistente no falla, devuelve Cashier (el rol de menor privilegio).
        Assert.Equal(OperatorRoles.Cashier, CreateAuthenticator().GetRole("nadie"));
    }

    [Fact]
    public void Role_lookup_is_case_insensitive()
    {
        Assert.Equal(OperatorRoles.Admin, CreateAuthenticator().GetRole("ADMIN1"));
    }
}
