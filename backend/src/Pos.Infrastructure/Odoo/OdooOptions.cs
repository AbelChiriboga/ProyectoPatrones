using System.ComponentModel.DataAnnotations;

namespace Pos.Infrastructure.Odoo;

/// <summary>
/// Configuración de la conexión con Odoo. La API key se inyecta por variable de entorno
/// (Odoo__ApiKey) o user-secrets; nunca se guarda en el repositorio ni llega al navegador.
/// </summary>
public sealed class OdooOptions
{
    public const string SectionName = "Odoo";

    [Required, Url]
    public string BaseUrl { get; set; } = "http://localhost:8069";

    [Required]
    public string Database { get; set; } = string.Empty;

    [Required(ErrorMessage = "Falta Odoo:ApiKey. En Development use 'dotnet user-secrets set \"Odoo:ApiKey\" \"<clave>\" --project backend/src/Pos.Api' " +
        "(los user-secrets solo se cargan con ASPNETCORE_ENVIRONMENT=Development); en otro entorno defina la variable Odoo__ApiKey.")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Punto de venta (pos.config) que atiende este backend; null para aceptar cualquiera.</summary>
    public int? PosConfigId { get; set; }

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 15;
}
