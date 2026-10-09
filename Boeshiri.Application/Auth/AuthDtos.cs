using System.ComponentModel.DataAnnotations;

namespace Boeshiri.Application.Auth;

/// <summary>Datos de registro/postulación (RF-PUB-13).</summary>
public record RegisterRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string Password { get; init; }

    [Required, MaxLength(160)]
    public required string FullName { get; init; }

    [Phone, MaxLength(32)]
    public string? Phone { get; init; }

    /// <summary>Disciplina principal declarada al postularse; editable luego en el perfil.</summary>
    [MaxLength(80)]
    public string? Discipline { get; init; }

    [MaxLength(1000)]
    public string? ApplicationReason { get; init; }
}

/// <summary>Petición de reenvío del enlace de verificación (RF-PUB-13b).</summary>
public record ResendVerificationRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }
}

/// <summary>Credenciales de inicio de sesión.</summary>
public record LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

/// <summary>Cambio de contraseña con sesión iniciada.</summary>
public record ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>Pedido de enlace para restablecer la contraseña.</summary>
public record ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }
}

/// <summary>Nueva contraseña con el token del enlace.</summary>
public record ResetPasswordRequest
{
    [Required, MaxLength(128)]
    public required string Token { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>Resultado del registro.</summary>
public record RegisterResult(Guid UserId, string Message);

/// <summary>Resultado del login: token JWT + resumen del usuario.</summary>
public record AuthResult(
    string Token,
    DateTime ExpiresAt,
    Guid UserId,
    string Email,
    string FullName,
    string Status,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);

/// <summary>
/// Sesión abierta o renovada. <see cref="Auth"/> va en el cuerpo de la respuesta;
/// <see cref="RefreshToken"/> va a la cookie y nunca al JSON. Es null cuando la
/// renovación no rotó el token (otra pestaña acababa de hacerlo) y la cookie que
/// ya tiene el navegador sigue siendo la buena.
/// </summary>
public record SessionResult(AuthResult Auth, string? RefreshToken, DateTime RefreshExpiresAt);

/// <summary>Estado de la sesión y de la solicitud (RF-PUB-16).</summary>
public record MeResult(
    Guid Id,
    string Email,
    string FullName,
    string Status,
    bool EmailVerified,
    string SolicitudEstado,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions);
