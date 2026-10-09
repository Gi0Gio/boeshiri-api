namespace Boeshiri.Application.Auth;

/// <summary>
/// Casos de uso de autenticación: registro con verificación de correo, login y
/// consulta de la sesión actual (§4.6, §7 del SDD).
/// </summary>
public interface IAuthService
{
    /// <summary>Registra un postulante y dispara el correo de verificación.</summary>
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Confirma el correo a partir del token de verificación.</summary>
    Task VerifyEmailAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Reemite el enlace de verificación (RF-PUB-13b). No revela si la dirección
    /// existe ni si ya estaba verificada: siempre termina en silencio.
    /// </summary>
    Task ResendVerificationAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Valida credenciales y emite un JWT con los permisos efectivos, más el token
    /// de renovación que mantiene la sesión abierta entre visitas.
    /// </summary>
    Task<SessionResult> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>
    /// Canjea un token de renovación por un JWT nuevo, con los permisos releídos de
    /// la base. Rota el token; reusar uno ya rotado cierra todas las sesiones.
    /// </summary>
    Task<SessionResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revoca el token de renovación. No falla si ya no existía.</summary>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Devuelve el usuario actual, su estado y sus roles/permisos.</summary>
    Task<MeResult> GetMeAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Cambia la contraseña verificando la actual. Cierra todas las sesiones y abre
    /// una nueva en este dispositivo: si alguien conocía la contraseña anterior, su
    /// sesión deja de servir.
    /// </summary>
    Task<SessionResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>Envía el enlace para restablecer. Silencioso si el correo no tiene cuenta.</summary>
    Task RequestPasswordResetAsync(string email, CancellationToken ct = default);

    /// <summary>Fija la contraseña nueva con el token del enlace y cierra todas las sesiones.</summary>
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
}
