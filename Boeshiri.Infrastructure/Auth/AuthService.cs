using System.Security.Cryptography;
using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Auth;
using Boeshiri.Application.Common;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
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
            ApplicationReason = request.ApplicationReason,
            Status = MemberStatus.Applicant,
            EmailVerified = false,
            RegisteredAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.Users.Add(user);
        db.VerificationTokens.Add(new VerificationToken
        {
            User = user,
            UserId = user.Id,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Used = false
        });
        await db.SaveChangesAsync(ct);

        var link = $"{_app.PublicBaseUrl}/auth/verificar?token={token}";
        await emailSender.SendAsync(
            user.Email,
            "Verifica tu correo — Boesh Irí",
            $"<p>Hola {user.FullName}, confirma tu correo para completar tu postulación:</p>" +
            $"<p><a href=\"{link}\">Verificar correo</a></p>",
            ct);

        logger.LogInformation("Nuevo postulante registrado: {Email}", user.Email);
        return new RegisterResult(user.Id, "Cuenta creada. Revisa tu correo para verificar la dirección.");
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
