using Boeshiri.Application.Auth;
using Boeshiri.Application.Common;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Auth;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Persistence.Seed;
using Boeshiri.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Boeshiri.Tests.Auth;

public class AuthServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeEmailSender _email = new();

    private AuthService NewService(BoeshiriDbContext ctx) => new(
        ctx,
        new PasswordHasher<User>(),
        new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            Key = "test-signing-key-of-at-least-32-bytes!!",
            Issuer = "test",
            Audience = "test",
            AccessTokenMinutes = 60
        })),
        Options.Create(new JwtOptions { RefreshTokenDays = 30 }),
        _email,
        Options.Create(new AppOptions { PublicBaseUrl = "http://test" }),
        _throttle,
        NullLogger<AuthService>.Instance);

    private readonly LoginThrottle _throttle = new(new Microsoft.Extensions.Caching.Memory.MemoryCache(
        new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions { SizeLimit = 10_000 }));

    private static RegisterRequest Reg(string email) => new()
    {
        Email = email,
        Password = "Secreta123",
        FullName = "Test User",
        Phone = "+50760000000",
        ApplicationReason = "Quiero unirme"
    };

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesUnverifiedApplicantAndSendsEmail()
    {
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RegisterAsync(Reg("nuevo@ex.com"));

        await using var verify = _db.CreateContext();
        var user = await verify.Users.SingleAsync(u => u.Email == "nuevo@ex.com");

        Assert.Equal(MemberStatus.Applicant, user.Status);
        Assert.False(user.EmailVerified);
        Assert.Single(verify.VerificationTokens);
        Assert.Single(_email.Sent);

        // El enlace apunta al FRONT (/verificar), no al endpoint de la API: quien lo
        // abre debe aterrizar en una página de la marca, no en el JSON del endpoint.
        var token = verify.VerificationTokens.Single().Token;
        Assert.Contains($"/verificar?token={token}", _email.Sent[0].Body);
        Assert.DoesNotContain("/auth/verificar", _email.Sent[0].Body);

        // Y viaja también la alternativa en texto plano, con el mismo enlace.
        Assert.NotNull(_email.Sent[0].Text);
        Assert.Contains($"/verificar?token={token}", _email.Sent[0].Text!);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsConflict()
    {
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RegisterAsync(Reg("dup@ex.com"));

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).RegisterAsync(Reg("DUP@ex.com")));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_MarksVerified()
    {
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RegisterAsync(Reg("v@ex.com"));

        string token;
        await using (var q = _db.CreateContext())
            token = (await q.VerificationTokens.SingleAsync()).Token;

        await using (var ctx2 = _db.CreateContext())
            await NewService(ctx2).VerifyEmailAsync(token);

        await using var check = _db.CreateContext();
        var user = await check.Users.SingleAsync(u => u.Email == "v@ex.com");
        Assert.True(user.EmailVerified);
        Assert.NotNull(user.VerifiedAt);
    }

    [Fact]
    public async Task VerifyEmailAsync_InvalidToken_ThrowsBadRequest()
    {
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).VerifyEmailAsync("no-existe"));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_UnverifiedEmail_ThrowsForbidden()
    {
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RegisterAsync(Reg("u@ex.com"));

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx2).LoginAsync(new LoginRequest { Email = "u@ex.com", Password = "Secreta123" }));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorized()
    {
        await RegisterVerifiedActiveAsync("w@ex.com");

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).LoginAsync(new LoginRequest { Email = "w@ex.com", Password = "ClaveMala" }));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_SuspendedUser_ThrowsForbidden()
    {
        await RegisterVerifiedActiveAsync("s@ex.com");
        await MutateUserAsync("s@ex.com", u => u.Status = MemberStatus.Suspended);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).LoginAsync(new LoginRequest { Email = "s@ex.com", Password = "Secreta123" }));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_ActiveMember_ReturnsTokenWithEffectivePermissions()
    {
        await SeedRolesAsync();
        await RegisterVerifiedActiveAsync("m@ex.com");
        await AssignRoleAsync("m@ex.com", "Miembro");

        await using var ctx = _db.CreateContext();
        var result = await NewService(ctx).LoginAsync(new LoginRequest { Email = "m@ex.com", Password = "Secreta123" });

        Assert.False(string.IsNullOrWhiteSpace(result.Auth.Token));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Contains("Miembro", result.Auth.Roles);
        Assert.Equal(9, result.Auth.Permissions.Count);
        Assert.Contains("perfil.editar", result.Auth.Permissions);
        Assert.Contains("gritos.publicar", result.Auth.Permissions);
    }

    // ── Renovación de sesión ─────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_ValidToken_RotatesAndIssuesNewJwt()
    {
        var login = await LoginActiveAsync("r@ex.com");

        await using var ctx = _db.CreateContext();
        var result = await NewService(ctx).RefreshAsync(login.RefreshToken!);

        Assert.False(string.IsNullOrWhiteSpace(result.Auth.Token));
        Assert.NotNull(result.RefreshToken);
        Assert.NotEqual(login.RefreshToken, result.RefreshToken);

        // Y el nuevo sirve para la siguiente renovación.
        await using var ctx2 = _db.CreateContext();
        await NewService(ctx2).RefreshAsync(result.RefreshToken!);
    }

    [Fact]
    public async Task RefreshAsync_RotatedTokenReusedAfterGrace_RevokesEverySession()
    {
        var login = await LoginActiveAsync("robo@ex.com");

        string sustituto;
        await using (var ctx = _db.CreateContext())
            sustituto = (await NewService(ctx).RefreshAsync(login.RefreshToken!)).RefreshToken!;

        // Se envejece la rotación para salir del margen de pestañas simultáneas.
        await using (var ctx = _db.CreateContext())
        {
            var rotado = await ctx.RefreshTokens.SingleAsync(t => t.RevokedAt != null);
            rotado.RevokedAt = DateTime.UtcNow.AddMinutes(-10);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).RefreshAsync(login.RefreshToken!));
            Assert.Equal(401, ex.StatusCode);
        }

        // El sustituto también cae: no se sabe si lo tiene el usuario o el ladrón.
        await using (var ctx = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).RefreshAsync(sustituto));
            Assert.Equal(401, ex.StatusCode);
        }
    }

    [Fact]
    public async Task RefreshAsync_RotatedTokenWithinGrace_ReturnsJwtWithoutNewCookie()
    {
        var login = await LoginActiveAsync("pestanas@ex.com");

        string sustituto;
        await using (var ctx = _db.CreateContext())
            sustituto = (await NewService(ctx).RefreshAsync(login.RefreshToken!)).RefreshToken!;

        // La segunda pestaña llega con la cookie vieja justo después.
        await using (var ctx = _db.CreateContext())
        {
            var result = await NewService(ctx).RefreshAsync(login.RefreshToken!);
            Assert.False(string.IsNullOrWhiteSpace(result.Auth.Token));
            Assert.Null(result.RefreshToken);
        }

        // Y no se revocó nada: el sustituto sigue vivo.
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RefreshAsync(sustituto);
    }

    [Fact]
    public async Task RefreshAsync_RotatedTokenWithinGraceAfterLogout_ThrowsUnauthorized()
    {
        var login = await LoginActiveAsync("cerro@ex.com");

        string sustituto;
        await using (var ctx = _db.CreateContext())
            sustituto = (await NewService(ctx).RefreshAsync(login.RefreshToken!)).RefreshToken!;

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).LogoutAsync(sustituto);

        // La cookie anterior a la rotación, aún dentro del margen, no reabre la sesión.
        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).RefreshAsync(login.RefreshToken!));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task RefreshAsync_SuspendedUser_ThrowsUnauthorized()
    {
        var login = await LoginActiveAsync("susp@ex.com");
        await MutateUserAsync("susp@ex.com", u => u.Status = MemberStatus.Suspended);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).RefreshAsync(login.RefreshToken!));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task RefreshAsync_ExpiredToken_ThrowsUnauthorized()
    {
        var login = await LoginActiveAsync("viejo@ex.com");
        await using (var ctx = _db.CreateContext())
        {
            var t = await ctx.RefreshTokens.SingleAsync();
            t.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await ctx.SaveChangesAsync();
        }

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).RefreshAsync(login.RefreshToken!));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task LogoutAsync_RevokesToken()
    {
        var login = await LoginActiveAsync("salir@ex.com");

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).LogoutAsync(login.RefreshToken!);

        // Revocado por cierre de sesión (sin sustituto): reusarlo no tiene margen.
        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).RefreshAsync(login.RefreshToken!));
        Assert.Equal(401, ex.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────
    /// <summary>
    /// Tras 5 contraseñas incorrectas la cuenta deja de admitir intentos un rato,
    /// aunque el siguiente intento traiga la contraseña buena: si no, el freno solo
    /// retrasaría a quien prueba contraseñas, no lo detendría.
    /// </summary>
    [Fact]
    public async Task LoginAsync_TrasCincoFallos_SeFrenaAunqueLaClaveSeaBuena()
    {
        await RegisterVerifiedActiveAsync("bf@ex.com");

        for (var i = 0; i < LoginThrottle.MaxFallos; i++)
        {
            await using var ctx = _db.CreateContext();
            var fallo = await Assert.ThrowsAsync<AppException>(() =>
                NewService(ctx).LoginAsync(new LoginRequest { Email = "bf@ex.com", Password = $"Mala{i}" }));
            Assert.Equal(401, fallo.StatusCode);
        }

        await using var c2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(c2).LoginAsync(new LoginRequest { Email = "bf@ex.com", Password = "Secreta123" }));
        Assert.Equal(429, ex.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_ExitoTrasAlgunFallo_ReiniciaElContador()
    {
        await RegisterVerifiedActiveAsync("ok@ex.com");
        for (var i = 0; i < LoginThrottle.MaxFallos - 1; i++)
        {
            await using var ctx = _db.CreateContext();
            await Assert.ThrowsAsync<AppException>(() =>
                NewService(ctx).LoginAsync(new LoginRequest { Email = "ok@ex.com", Password = "Mala" }));
        }
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).LoginAsync(new LoginRequest { Email = "ok@ex.com", Password = "Secreta123" });

        // El contador volvió a cero: un fallo más no bloquea.
        await using var c3 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(c3).LoginAsync(new LoginRequest { Email = "ok@ex.com", Password = "Mala" }));
        Assert.Equal(401, ex.StatusCode);
    }

    /// <summary>Un correo sin cuenta cuenta como fallo igual: el freno no delata quién existe.</summary>
    [Fact]
    public async Task LoginAsync_CorreoSinCuenta_TambienSeFrena()
    {
        for (var i = 0; i < LoginThrottle.MaxFallos; i++)
        {
            await using var ctx = _db.CreateContext();
            await Assert.ThrowsAsync<AppException>(() =>
                NewService(ctx).LoginAsync(new LoginRequest { Email = "nadie@ex.com", Password = "x" }));
        }
        await using var c2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(c2).LoginAsync(new LoginRequest { Email = "nadie@ex.com", Password = "x" }));
        Assert.Equal(429, ex.StatusCode);
    }

    // ── Contraseña ───────────────────────────────────────────────

    private string TokenDelUltimoCorreo()
    {
        var texto = _email.Sent[^1].Text!;
        var i = texto.IndexOf("token=", StringComparison.Ordinal) + "token=".Length;
        return new string(texto[i..].TakeWhile(char.IsLetterOrDigit).ToArray());
    }

    private async Task<SessionResult> Login(string email, string clave)
    {
        await using var ctx = _db.CreateContext();
        return await NewService(ctx).LoginAsync(new LoginRequest { Email = email, Password = clave });
    }

    [Fact]
    public async Task ChangePassword_CierraLasDemasSesionesYDejaUnaNueva()
    {
        var otra = await LoginActiveAsync("cp@ex.com");
        var userId = otra.Auth.UserId;

        SessionResult nueva;
        await using (var ctx = _db.CreateContext())
            nueva = await NewService(ctx).ChangePasswordAsync(userId, new ChangePasswordRequest { CurrentPassword = "Secreta123", NewPassword = "OtraClave456" });

        Assert.NotNull(nueva.RefreshToken);
        await using (var ctx = _db.CreateContext())
            await Assert.ThrowsAsync<AppException>(() => NewService(ctx).RefreshAsync(otra.RefreshToken!));
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RefreshAsync(nueva.RefreshToken!);
        await Login("cp@ex.com", "OtraClave456");
        await Assert.ThrowsAsync<AppException>(() => Login("cp@ex.com", "Secreta123"));
    }

    [Fact]
    public async Task ChangePassword_ClaveActualIncorrecta_400()
    {
        var s = await LoginActiveAsync("cp2@ex.com");
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).ChangePasswordAsync(s.Auth.UserId, new ChangePasswordRequest { CurrentPassword = "Mala", NewPassword = "OtraClave456" }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Recuperar_FlujoCompleto_CambiaLaClaveYCierraSesiones()
    {
        var sesion = await LoginActiveAsync("rc@ex.com");
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RequestPasswordResetAsync("RC@ex.com ");

        var token = TokenDelUltimoCorreo();
        Assert.Equal(64, token.Length);
        await using (var ctx = _db.CreateContext())
            Assert.False(await ctx.PasswordResetTokens.AnyAsync(t => t.TokenHash == token)); // se guarda el hash, no el token

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ResetPasswordAsync(new ResetPasswordRequest { Token = token, NewPassword = "Recuperada789" });

        await Login("rc@ex.com", "Recuperada789");
        await using (var ctx = _db.CreateContext())
            await Assert.ThrowsAsync<AppException>(() => NewService(ctx).RefreshAsync(sesion.RefreshToken!));

        // Un solo uso.
        await using (var ctx = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<AppException>(() =>
                NewService(ctx).ResetPasswordAsync(new ResetPasswordRequest { Token = token, NewPassword = "OtraMas000" }));
            Assert.Equal(400, ex.StatusCode);
        }
    }

    [Fact]
    public async Task Recuperar_CorreoSinCuenta_NoEnviaNadaNiFalla()
    {
        await using var ctx = _db.CreateContext();
        await NewService(ctx).RequestPasswordResetAsync("nadie@ex.com");
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Recuperar_EnlaceVencido_400()
    {
        await LoginActiveAsync("ven@ex.com");
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RequestPasswordResetAsync("ven@ex.com");
        var token = TokenDelUltimoCorreo();
        await using (var ctx = _db.CreateContext())
        {
            foreach (var t in ctx.PasswordResetTokens) t.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await ctx.SaveChangesAsync();
        }

        await using var c2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(c2).ResetPasswordAsync(new ResetPasswordRequest { Token = token, NewPassword = "Nueva12345" }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Recuperar_PedirDosVecesSeguidas_SoloEnviaUno()
    {
        await LoginActiveAsync("dos@ex.com");
        var antes = _email.Sent.Count;
        for (var i = 0; i < 2; i++)
        {
            await using var ctx = _db.CreateContext();
            await NewService(ctx).RequestPasswordResetAsync("dos@ex.com");
        }
        Assert.Equal(antes + 1, _email.Sent.Count);
    }

    [Fact]
    public async Task Reapply_AntesDe30Dias_409_Despues_VuelveARevision()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
        {
            var u = new User { Email = "rp@ex.com", PasswordHash = "x", FullName = "R", Status = MemberStatus.Applicant, EmailVerified = true, RejectedAt = DateTime.UtcNow.AddDays(-5) };
            ctx.Users.Add(u);
            await ctx.SaveChangesAsync();
            id = u.Id;
        }

        await using (var ctx = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).ReapplyAsync(id));
            Assert.Equal(409, ex.StatusCode);
            Assert.NotNull((await NewService(ctx).GetMeAsync(id)).PuedePostularseDesde);
        }

        await using (var ctx = _db.CreateContext())
        {
            (await ctx.Users.FindAsync(id))!.RejectedAt = DateTime.UtcNow.AddDays(-31);
            await ctx.SaveChangesAsync();
        }
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ReapplyAsync(id);

        await using var check = _db.CreateContext();
        Assert.Equal("EnRevision", (await NewService(check).GetMeAsync(id)).SolicitudEstado);
    }

    private async Task<SessionResult> LoginActiveAsync(string email)
    {
        await RegisterVerifiedActiveAsync(email);
        await using var ctx = _db.CreateContext();
        return await NewService(ctx).LoginAsync(new LoginRequest { Email = email, Password = "Secreta123" });
    }

    private async Task RegisterVerifiedActiveAsync(string email)
    {
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RegisterAsync(Reg(email));
        await MutateUserAsync(email, u => { u.EmailVerified = true; u.Status = MemberStatus.Active; });
    }

    private async Task MutateUserAsync(string email, Action<User> mutate)
    {
        await using var ctx = _db.CreateContext();
        var user = await ctx.Users.SingleAsync(u => u.Email == email);
        mutate(user);
        await ctx.SaveChangesAsync();
    }

    private async Task AssignRoleAsync(string email, string roleName)
    {
        await using var ctx = _db.CreateContext();
        var user = await ctx.Users.SingleAsync(u => u.Email == email);
        var role = await ctx.Roles.SingleAsync(r => r.Name == roleName);
        ctx.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedRolesAsync()
    {
        await using var ctx = _db.CreateContext();
        await DatabaseSeeder.SeedAsync(ctx);
    }

    // ── Reenvío de verificación (RF-PUB-13b) ─────────────────────

    [Fact]
    public async Task ResendVerification_SendsNewLinkAndInvalidatesPrevious()
    {
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RegisterAsync(Reg("reenvio@ex.com"));

        // El registro deja el token dentro de la ventana de espera; se envejece
        // para poder ejercitar el reenvío sin esperar en el test.
        await using (var ctx = _db.CreateContext())
        {
            var t = await ctx.VerificationTokens.SingleAsync();
            t.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ResendVerificationAsync("reenvio@ex.com");

        await using var check = _db.CreateContext();
        var tokens = await check.VerificationTokens.OrderBy(t => t.CreatedAt).ToListAsync();

        Assert.Equal(2, tokens.Count);
        Assert.True(tokens[0].Used);    // el anterior queda anulado
        Assert.False(tokens[1].Used);   // solo vale el nuevo
        Assert.Equal(2, _email.Sent.Count);
        Assert.Contains(tokens[1].Token, _email.Sent[1].Body);
    }

    [Fact]
    public async Task ResendVerification_WithinCooldown_DoesNotSend()
    {
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RegisterAsync(Reg("rapido@ex.com"));

        // Sin envejecer el token: el reenvío cae dentro de la espera mínima. Sin
        // esta guarda, el endpoint anónimo permitiría inundar un buzón ajeno.
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ResendVerificationAsync("rapido@ex.com");

        Assert.Single(_email.Sent);
    }

    [Fact]
    public async Task ResendVerification_AlreadyVerified_DoesNotSend()
    {
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RegisterAsync(Reg("verificado@ex.com"));

        await using (var ctx = _db.CreateContext())
        {
            var u = await ctx.Users.SingleAsync(u => u.Email == "verificado@ex.com");
            u.EmailVerified = true;
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ResendVerificationAsync("verificado@ex.com");

        Assert.Single(_email.Sent);   // solo el del registro
    }

    [Fact]
    public async Task ResendVerification_UnknownEmail_SucceedsSilently()
    {
        await using var ctx = _db.CreateContext();

        // No lanza: si distinguiera el caso, el endpoint revelaría quién tiene cuenta.
        await NewService(ctx).ResendVerificationAsync("nadie@ex.com");

        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task ResendVerification_NewTokenVerifiesAndOldOneFails()
    {
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RegisterAsync(Reg("cadena@ex.com"));

        string tokenViejo;
        await using (var ctx = _db.CreateContext())
        {
            var t = await ctx.VerificationTokens.SingleAsync();
            tokenViejo = t.Token;
            t.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ResendVerificationAsync("cadena@ex.com");

        // El enlace viejo ya no sirve...
        await using (var ctx = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).VerifyEmailAsync(tokenViejo));
            Assert.Equal(400, ex.StatusCode);
        }

        // ...y el nuevo sí.
        string tokenNuevo;
        await using (var ctx = _db.CreateContext())
            tokenNuevo = await ctx.VerificationTokens.Where(t => !t.Used).Select(t => t.Token).SingleAsync();

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).VerifyEmailAsync(tokenNuevo);

        await using var check = _db.CreateContext();
        Assert.True((await check.Users.SingleAsync(u => u.Email == "cadena@ex.com")).EmailVerified);
    }

    public void Dispose() => _db.Dispose();
}
