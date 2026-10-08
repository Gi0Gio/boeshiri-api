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

    /// <summary>
    /// Vida del JWT. Corta a propósito: los permisos viajan dentro del token, así que
    /// un cambio de rol o una suspensión solo surten efecto cuando se renueva.
    /// </summary>
    [Range(5, 1440)]
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>
    /// Vida de la cookie de renovación. Se reinicia con cada renovación: quien entra
    /// al menos una vez en este plazo no vuelve a ver el login.
    /// </summary>
    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 30;
}
