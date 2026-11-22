namespace Pos.Application.Auth;

/// <summary>Roles reconocidos por el POS. Constantes para evitar cadenas sueltas en policies y tokens.</summary>
public static class OperatorRoles
{
    public const string Admin = "Admin";
    public const string Cashier = "Cashier";
}
