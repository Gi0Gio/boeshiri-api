using System.ComponentModel.DataAnnotations;

namespace Boeshiri.Infrastructure.Auth;

/// <summary>Configuración del token JWT (sección "Jwt"). La clave va en secrets.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Clave de firma HMAC (mín. 32 bytes). En dev: user-secrets.</summary>
    [Required, MinLength(32)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int AccessTokenMinutes { get; set; } = 120;
}
