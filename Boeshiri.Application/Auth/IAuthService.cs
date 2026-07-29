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

    /// <summary>Valida credenciales y emite un JWT con los permisos efectivos.</summary>
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Devuelve el usuario actual, su estado y sus roles/permisos.</summary>
    Task<MeResult> GetMeAsync(Guid userId, CancellationToken ct = default);
}
