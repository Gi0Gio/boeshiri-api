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
        _email,
        Options.Create(new AppOptions { PublicBaseUrl = "http://test" }),
        NullLogger<AuthService>.Instance);

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
        Assert.Contains("/auth/verificar", _email.Sent[0].Body);
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

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Contains("Miembro", result.Roles);
        Assert.Equal(8, result.Permissions.Count);
        Assert.Contains("perfil.editar", result.Permissions);
    }

    // ── Helpers ──────────────────────────────────────────────────
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

    public void Dispose() => _db.Dispose();
}
