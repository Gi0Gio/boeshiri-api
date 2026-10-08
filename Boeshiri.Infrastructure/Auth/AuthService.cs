using System.Security.Cryptography;
using System.Text;
using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Auth;
using Boeshiri.Application.Common;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Email;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boeshiri.Infrastructure.Auth;

/// <summary>
/// Implementación de los casos de uso de autenticación (SDD §7). Usa el
/// PasswordHasher de Identity para el hash, tokens de verificación propios
/// (RF-PUB-13b) y emite JWT con los permisos efectivos.
/// </summary>
public class AuthService(
    BoeshiriDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtTokenGenerator jwtGenerator,
    IOptions<JwtOptions> jwtOptions,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly AppOptions _app = appOptions.Value;
    private readonly TimeSpan _refreshLifetime = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenDays);

    /// <summary>
    /// Margen en que presentar un token recién rotado no cuenta como robo. Dos
    /// pestañas que cargan a la vez mandan la misma cookie; la segunda llega con un
    /// token que la primera acaba de rotar, y sin este margen se cerrarían todas
    /// las sesiones del usuario cada vez que abre dos pestañas.
    /// </summary>
    private static readonly TimeSpan RotationGrace = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// Espera mínima entre reenvíos. Sin ella, el endpoint (anónimo por necesidad)
    /// sería un cañón para inundar el buzón de cualquiera que tenga cuenta aquí.
    /// </summary>
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(2);

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw AppException.Conflict("Ya existe una cuenta con ese correo.");

        var user = new User
        {
            Email = email,
            PasswordHash = string.Empty,
            FullName = request.FullName.Trim(),
            Phone = request.Phone,
            Discipline = request.Discipline,
            ApplicationReason = request.ApplicationReason,
            Status = MemberStatus.Applicant,
            EmailVerified = false,
            RegisteredAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var token = NewToken();
        db.Users.Add(user);
        db.VerificationTokens.Add(new VerificationToken
        {
            User = user,
            UserId = user.Id,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(TokenLifetime),
            Used = false
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // La comprobación de arriba y este insert no son atómicos: dos registros
            // simultáneos con el mismo correo pasan ambos el AnyAsync y el segundo
            // choca con el índice único. Sin esto sería un 500 en vez del 409 que el
            // formulario ya sabe manejar.
            if (await db.Users.AsNoTracking().AnyAsync(u => u.Email == email, ct))
                throw AppException.Conflict("Ya existe una cuenta con ese correo.");
            throw;
        }

        // Apunta al front (ruta /verificar), que llama al endpoint por debajo y muestra
        // el resultado con la identidad del sitio.
        await SendVerificationEmailAsync(user, token, ct);

        logger.LogInformation("Nuevo postulante registrado: {Email}", user.Email);
        return new RegisterResult(user.Id, "Cuenta creada. Revisa tu correo para verificar la dirección.");
    }

    public async Task ResendVerificationAsync(string email, CancellationToken ct = default)
    {
        var normalizado = Normalize(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizado, ct);

        // Salidas en silencio: distinguirlas en la respuesta convertiría el endpoint
        // en un oráculo para averiguar quién tiene cuenta (enumeración de correos).
        if (user is null)
        {
            logger.LogInformation("Reenvío pedido para un correo sin cuenta: {Email}", normalizado);
            return;
        }

        if (user.EmailVerified)
        {
            logger.LogInformation("Reenvío pedido para un correo ya verificado: {Email}", normalizado);
            return;
        }

        var ahora = DateTime.UtcNow;
        var reciente = await db.VerificationTokens
            .AnyAsync(t => t.UserId == user.Id && !t.Used && t.CreatedAt > ahora - ResendCooldown, ct);

        if (reciente)
        {
            logger.LogInformation("Reenvío ignorado por espera mínima: {Email}", normalizado);
            return;
        }

        // Los enlaces anteriores se anulan: si no, cada reenvío dejaría otro token
        // vivo y bastaría con interceptar cualquiera de ellos.
        var previos = await db.VerificationTokens
            .Where(t => t.UserId == user.Id && !t.Used)
            .ToListAsync(ct);
        foreach (var t in previos) t.Used = true;

        var token = NewToken();
        db.VerificationTokens.Add(new VerificationToken
        {
            UserId = user.Id,
            Token = token,
            CreatedAt = ahora,
            ExpiresAt = ahora.Add(TokenLifetime),
            Used = false
        });
        await db.SaveChangesAsync(ct);

        await SendVerificationEmailAsync(user, token, ct);
        logger.LogInformation("Enlace de verificación reenviado a {Email}", normalizado);
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private Task SendVerificationEmailAsync(User user, string token, CancellationToken ct)
    {
        var link = $"{_app.PublicBaseUrl.TrimEnd('/')}/verificar?token={token}";
        return emailSender.SendAsync(
            user.Email,
            "Confirma tu correo — Boesh Irí",
            EmailTemplates.VerificationHtml(user.FullName, link),
            EmailTemplates.VerificationText(user.FullName, link),
            ct);
    }

    public async Task VerifyEmailAsync(string token, CancellationToken ct = default)
    {
        var verification = await db.VerificationTokens
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Token == token, ct);

        if (verification is null || verification.Used || verification.ExpiresAt < DateTime.UtcNow)
            throw AppException.BadRequest("Enlace de verificación inválido o expirado.");

        verification.Used = true;
        verification.User.EmailVerified = true;
        verification.User.VerifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Correo verificado: {Email}", verification.User.Email);
    }

    public async Task<SessionResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);
        var user = await LoadWithRolesAsync(u => u.Email == email, ct);

        if (user is null)
            throw AppException.Unauthorized("Correo o contraseña incorrectos.");

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            throw AppException.Unauthorized("Correo o contraseña incorrectos.");

        if (!user.EmailVerified)
            throw AppException.Forbidden("Debes verificar tu correo antes de iniciar sesión.");

        if (EstadoBloqueado(user))
            throw AppException.Forbidden($"Tu cuenta está en estado '{user.Status}' y no puede iniciar sesión.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        var ahora = DateTime.UtcNow;

        // Los caducados ya no sirven ni para detectar reuso: se barren aquí para que
        // la tabla no crezca con cada inicio de sesión.
        await db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresAt < ahora)
            .ExecuteDeleteAsync(ct);

        var (refreshToken, refresh) = NewRefreshToken(user.Id, ahora);
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Login exitoso: {Email}", user.Email);
        return new SessionResult(BuildAuthResult(user), refreshToken, refresh.ExpiresAt);
    }

    public async Task<SessionResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        var ahora = DateTime.UtcNow;
        var stored = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || stored.ExpiresAt <= ahora)
            throw SesionExpirada();

        if (stored.RevokedAt is not null && !EnGracia(stored, ahora))
        {
            // Un token rotado hace rato solo lo tiene quien lo copió: el navegador
            // legítimo ya recibió el sustituto. No se sabe cuál de los dos es el
            // ladrón, así que se cierran todas las sesiones del usuario.
            await RevokeAllAsync(stored.UserId, ahora, ct);
            logger.LogWarning("Reuso de token de renovación para {UserId}: sesiones revocadas", stored.UserId);
            throw SesionExpirada();
        }

        // Dentro del margen solo vale si la sesión sigue viva: tras un cierre de
        // sesión, la cookie anterior a la última rotación tampoco debe servir.
        if (stored.RevokedAt is not null && !await CadenaVigenteAsync(stored, ahora, ct))
            throw SesionExpirada();

        // Se relee el usuario: así un rol retirado o una suspensión surten efecto en
        // la siguiente renovación, sin esperar a que caduque la cookie.
        var user = await LoadWithRolesAsync(u => u.Id == stored.UserId, ct);
        if (user is null || !user.EmailVerified || EstadoBloqueado(user))
        {
            await RevokeAllAsync(stored.UserId, ahora, ct);
            throw SesionExpirada();
        }

        if (stored.RevokedAt is not null)
            return new SessionResult(BuildAuthResult(user), null, stored.ExpiresAt);

        // La revocación es condicional para que dos renovaciones simultáneas no
        // roten ambas: la que pierde recibe el JWT pero deja la cookie de la otra.
        var (nuevoToken, nuevo) = NewRefreshToken(user.Id, ahora);
        var rotado = await db.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, ahora)
                .SetProperty(t => t.ReplacedById, nuevo.Id), ct);

        if (rotado == 0)
            return new SessionResult(BuildAuthResult(user), null, stored.ExpiresAt);

        db.RefreshTokens.Add(nuevo);
        await db.SaveChangesAsync(ct);
        return new SessionResult(BuildAuthResult(user), nuevoToken, nuevo.ExpiresAt);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = HashToken(refreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
    }

    private AuthResult BuildAuthResult(User user)
    {
        var (token, expiresAt) = jwtGenerator.Generate(user);
        return new AuthResult(
            token, expiresAt, user.Id, user.Email, user.FullName, user.Status.ToString(),
            user.UserRoles.Select(ur => ur.Role.Name).ToArray(),
            user.EffectivePermissions().ToArray());
    }

    private (string Value, RefreshToken Entity) NewRefreshToken(Guid userId, DateTime ahora)
    {
        var value = NewToken();
        return (value, new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(value),
            CreatedAt = ahora,
            ExpiresAt = ahora.Add(_refreshLifetime)
        });
    }

    private Task RevokeAllAsync(Guid userId, DateTime ahora, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, ahora), ct);

    /// <summary>¿Algún sucesor del token rotado sigue activo?</summary>
    private async Task<bool> CadenaVigenteAsync(RefreshToken token, DateTime ahora, CancellationToken ct)
    {
        // Pocos saltos bastan: dentro del margen de un minuto apenas hay rotaciones.
        var actual = token;
        for (var i = 0; i < 5 && actual.ReplacedById is Guid siguiente; i++)
        {
            actual = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == siguiente, ct);
            if (actual is null)
                return false;
            if (actual.RevokedAt is null)
                return actual.ExpiresAt > ahora;
        }
        return false;
    }

    private static bool EnGracia(RefreshToken token, DateTime ahora) =>
        token.ReplacedById is not null && token.RevokedAt > ahora - RotationGrace;

    private static bool EstadoBloqueado(User user) =>
        user.Status is MemberStatus.Suspended or MemberStatus.Expelled or MemberStatus.Retired;

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static AppException SesionExpirada() =>
        AppException.Unauthorized("Tu sesión expiró. Inicia sesión de nuevo.");

    public async Task<MeResult> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await LoadWithRolesAsync(u => u.Id == userId, ct)
            ?? throw AppException.Unauthorized("Usuario no encontrado.");

        return new MeResult(
            user.Id, user.Email, user.FullName, user.Status.ToString(), user.EmailVerified,
            SolicitudEstado(user),
            user.UserRoles.Select(ur => ur.Role.Name).ToArray(),
            user.EffectivePermissions().ToArray());
    }

    /// <summary>Estado de la solicitud para mostrar al iniciar sesión (RF-PUB-16).</summary>
    private static string SolicitudEstado(User user) => user switch
    {
        { EmailVerified: false } => "PendienteVerificacion",
        { Status: MemberStatus.Applicant, RejectedAt: not null } => "Rechazada",
        { Status: MemberStatus.Applicant } => "EnRevision",
        { Status: MemberStatus.Active } => "Aceptada",
        _ => user.Status.ToString()
    };

    private Task<User?> LoadWithRolesAsync(
        System.Linq.Expressions.Expression<Func<User, bool>> predicate, CancellationToken ct) =>
        db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(predicate, ct);

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
