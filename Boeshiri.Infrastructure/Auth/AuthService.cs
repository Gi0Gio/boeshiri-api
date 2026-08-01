using System.Security.Cryptography;
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
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly AppOptions _app = appOptions.Value;

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

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
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

        if (user.Status is MemberStatus.Suspended or MemberStatus.Expelled or MemberStatus.Retired)
            throw AppException.Forbidden($"Tu cuenta está en estado '{user.Status}' y no puede iniciar sesión.");

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        var (token, expiresAt) = jwtGenerator.Generate(user);
        logger.LogInformation("Login exitoso: {Email}", user.Email);

        return new AuthResult(
            token, expiresAt, user.Id, user.Email, user.FullName, user.Status.ToString(),
            user.UserRoles.Select(ur => ur.Role.Name).ToArray(),
            user.EffectivePermissions().ToArray());
    }

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
