using System.ComponentModel.DataAnnotations;

namespace Pos.Api.Security;

/// <summary>Configuración del token de acceso. La clave se inyecta por user-secrets o variable de entorno (Jwt__Key).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Falta Jwt:Key (mínimo 32 caracteres). En Development use 'dotnet user-secrets set \"Jwt:Key\" \"<clave larga>\" --project backend/src/Pos.Api'; " +
        "en otro entorno defina la variable Jwt__Key."), MinLength(32, ErrorMessage = "Jwt:Key debe tener al menos 32 caracteres.")]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "pos-api";

    [Required]
    public string Audience { get; set; } = "pos-frontend";

    [Range(1, 1440)]
    public int ExpirationMinutes { get; set; } = 480;
}
