using Boeshiri.Application.Admin;
using Boeshiri.Application.Common;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Admin;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Persistence.Seed;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boeshiri.Tests.Admin;

public class PostulantesServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    // Servicio con notificaciones y auditoría reales, todos sobre el MISMO contexto
    // (así la decisión + aviso + auditoría se guardan de forma atómica).
    private PostulantesService NewService(BoeshiriDbContext ctx) => new(
        ctx,
        new NotificationService(ctx),
        new AuditLogger(ctx),
        Microsoft.Extensions.Options.Options.Create(new Boeshiri.Infrastructure.Auth.AppOptions { PublicBaseUrl = "http://test" }),
        NullLogger<PostulantesService>.Instance);

    [Fact]
    public async Task ListPendingAsync_ReturnsUndecidedApplicantsIncludingUnverified()
    {
        await CreateUserAsync("pendiente@ex.com", MemberStatus.Applicant, verified: true);
        await CreateUserAsync("sin-verificar@ex.com", MemberStatus.Applicant, verified: false);
        await CreateUserAsync("activo@ex.com", MemberStatus.Active, verified: true);
        await CreateUserAsync("rechazado@ex.com", MemberStatus.Applicant, verified: true, rejected: true);

        await using var ctx = _db.CreateContext();
        var pending = await NewService(ctx).ListPendingAsync();

        // Los no verificados se listan (no se pueden decidir, pero deben verse);
        // los ya decididos y los que no son postulantes quedan fuera.
        Assert.Equal(2, pending.Count);
        Assert.Contains(pending, p => p.Email == "sin-verificar@ex.com" && !p.EmailVerified);
        // Los verificados van primero: son los accionables.
        Assert.Equal("pendiente@ex.com", pending[0].Email);
        Assert.DoesNotContain(pending, p => p.Email is "activo@ex.com" or "rechazado@ex.com");
    }

    [Fact]
    public async Task IssueVerificationLink_ReturnsFreshLinkAndInvalidatesPrevious()
    {
        var id = await CreateUserAsync("sinverificar@ex.com", MemberStatus.Applicant, verified: false);
        await using (var ctx = _db.CreateContext())
        {
            ctx.VerificationTokens.Add(new VerificationToken
            {
                UserId = id, Token = "VIEJO", CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(24), Used = false
            });
            await ctx.SaveChangesAsync();
        }

        var actor = Guid.NewGuid();
        VerificationLinkDto dto;
        await using (var ctx = _db.CreateContext())
            dto = await NewService(ctx).IssueVerificationLinkAsync(id, actor);

        await using var check = _db.CreateContext();
        var viejo = await check.VerificationTokens.SingleAsync(t => t.Token == "VIEJO");
        var nuevo = await check.VerificationTokens.SingleAsync(t => !t.Used);

        Assert.True(viejo.Used);                        // el repartido antes deja de servir
        Assert.Contains(nuevo.Token, dto.Link);
        Assert.Contains("/verificar?token=", dto.Link);
        // Entregar el enlace es entregar una credencial: tiene que quedar rastro.
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "verificacion.enlace_emitido" && a.ActorId == actor));
    }

    [Fact]
    public async Task IssueVerificationLink_AlreadyVerified_ThrowsConflict()
    {
        var id = await CreateUserAsync("yaverificado@ex.com", MemberStatus.Applicant, verified: true);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).IssueVerificationLinkAsync(id, Guid.NewGuid()));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task DecideAsync_UnverifiedApplicant_ThrowsConflict()
    {
        await SeedRolesAsync();
        var id = await CreateUserAsync("sin-verificar@ex.com", MemberStatus.Applicant, verified: false);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).DecideAsync(id, new DecisionRequest { Decision = DecisionType.Aceptar }, Guid.NewGuid()));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task DecideAsync_Accept_ActivatesAssignsMemberRoleNotifiesAndAudits()
    {
        await SeedRolesAsync();
        var id = await CreateUserAsync("nuevo@ex.com", MemberStatus.Applicant, verified: true);
        var adminId = Guid.NewGuid();

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).DecideAsync(id, new DecisionRequest { Decision = DecisionType.Aceptar }, adminId);

        await using var check = _db.CreateContext();
        var user = await check.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .SingleAsync(u => u.Id == id);

        Assert.Equal(MemberStatus.Active, user.Status);
        Assert.Contains(user.UserRoles, ur => ur.Role.Name == "Miembro");
        Assert.Equal(1, await check.Notifications.CountAsync(n => n.UserId == id && n.Type == "solicitud.aceptada"));
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "postulante.aceptado" && a.ActorId == adminId));
    }

    [Fact]
    public async Task DecideAsync_Reject_SetsRejectedAtNotifiesAndAudits()
    {
        var id = await CreateUserAsync("rech@ex.com", MemberStatus.Applicant, verified: true);
        var adminId = Guid.NewGuid();

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).DecideAsync(id, new DecisionRequest { Decision = DecisionType.Rechazar }, adminId);

        await using var check = _db.CreateContext();
        var user = await check.Users.SingleAsync(u => u.Id == id);

        Assert.NotNull(user.RejectedAt);
        Assert.Equal(MemberStatus.Applicant, user.Status);
        Assert.Equal(1, await check.Notifications.CountAsync(n => n.UserId == id && n.Type == "solicitud.rechazada"));
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "postulante.rechazado"));
    }

    [Fact]
    public async Task DecideAsync_AlreadyDecided_ThrowsConflict()
    {
        await SeedRolesAsync();
        var id = await CreateUserAsync("doble@ex.com", MemberStatus.Applicant, verified: true);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).DecideAsync(id, new DecisionRequest { Decision = DecisionType.Aceptar }, Guid.NewGuid());

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx2).DecideAsync(id, new DecisionRequest { Decision = DecisionType.Aceptar }, Guid.NewGuid()));
        Assert.Equal(409, ex.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────
    private async Task<Guid> CreateUserAsync(string email, MemberStatus status, bool verified, bool rejected = false)
    {
        await using var ctx = _db.CreateContext();
        var user = new User
        {
            Email = email,
            PasswordHash = "x",
            FullName = "Postulante",
            Status = status,
            EmailVerified = verified,
            RejectedAt = rejected ? DateTime.UtcNow : null
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    private async Task SeedRolesAsync()
    {
        await using var ctx = _db.CreateContext();
        await DatabaseSeeder.SeedAsync(ctx);
    }

    public void Dispose() => _db.Dispose();
}
