using Boeshiri.Application.Admin;
using Boeshiri.Application.Common;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Admin;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boeshiri.Tests.Admin;

public class MemberServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    // Notificaciones y auditoría reales sobre el MISMO contexto: el cambio de
    // estado, el aviso y la entrada de auditoría se guardan de forma atómica.
    private MemberService NewService(BoeshiriDbContext ctx) => new(
        ctx,
        new NotificationService(ctx),
        new AuditLogger(ctx),
        NullLogger<MemberService>.Instance);

    [Fact]
    public async Task ListAsync_ExcludesApplicants()
    {
        await CreateUserAsync("activo@ex.com", MemberStatus.Active);
        await CreateUserAsync("suspendido@ex.com", MemberStatus.Suspended);
        await CreateUserAsync("postulante@ex.com", MemberStatus.Applicant);

        await using var ctx = _db.CreateContext();
        var miembros = await NewService(ctx).ListAsync();

        Assert.Equal(2, miembros.Count);
        Assert.DoesNotContain(miembros, m => m.Email == "postulante@ex.com");
    }

    [Fact]
    public async Task ChangeStatusAsync_UpdatesStatusNotifiesAndAudits()
    {
        var id = await CreateUserAsync("miembro@ex.com", MemberStatus.Active);
        var adminId = Guid.NewGuid();

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(
                id, new ChangeMemberStatusRequest { Status = MemberStatus.Suspended, Motivo = "Incumplimiento" }, adminId);

        await using var check = _db.CreateContext();
        var user = await check.Users.SingleAsync(u => u.Id == id);

        Assert.Equal(MemberStatus.Suspended, user.Status);
        Assert.NotNull(user.StatusChangedAt);
        Assert.Equal(1, await check.Notifications.CountAsync(n => n.UserId == id && n.Type == "miembro.estado_cambiado"));

        var entrada = await check.AuditEntries.SingleAsync(a => a.Action == "miembro.estado_cambiado");
        Assert.Equal(adminId, entrada.ActorId);
        // La transición y el motivo quedan registrados para poder auditarla después.
        Assert.Contains("Active", entrada.Metadata);
        Assert.Contains("Suspended", entrada.Metadata);
        Assert.Contains("Incumplimiento", entrada.Metadata);
    }

    [Fact]
    public async Task ChangeStatusAsync_Reactivating_RestoresActive()
    {
        var id = await CreateUserAsync("pausa@ex.com", MemberStatus.Inactive);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(
                id, new ChangeMemberStatusRequest { Status = MemberStatus.Active }, Guid.NewGuid());

        await using var check = _db.CreateContext();
        Assert.Equal(MemberStatus.Active, (await check.Users.SingleAsync(u => u.Id == id)).Status);
    }

    [Fact]
    public async Task ChangeStatusAsync_ToApplicant_ThrowsBadRequest()
    {
        var id = await CreateUserAsync("miembro@ex.com", MemberStatus.Active);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).ChangeStatusAsync(
            id, new ChangeMemberStatusRequest { Status = MemberStatus.Applicant }, Guid.NewGuid()));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeStatusAsync_OnSelf_ThrowsForbidden()
    {
        var id = await CreateUserAsync("admin@ex.com", MemberStatus.Active);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).ChangeStatusAsync(
            id, new ChangeMemberStatusRequest { Status = MemberStatus.Suspended }, actorId: id));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeStatusAsync_OnUndecidedApplicant_ThrowsConflict()
    {
        var id = await CreateUserAsync("postulante@ex.com", MemberStatus.Applicant);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).ChangeStatusAsync(
            id, new ChangeMemberStatusRequest { Status = MemberStatus.Active }, Guid.NewGuid()));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeStatusAsync_SameStatus_ThrowsConflict()
    {
        var id = await CreateUserAsync("miembro@ex.com", MemberStatus.Active);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).ChangeStatusAsync(
            id, new ChangeMemberStatusRequest { Status = MemberStatus.Active }, Guid.NewGuid()));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeStatusAsync_UnknownMember_ThrowsNotFound()
    {
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).ChangeStatusAsync(
            Guid.NewGuid(), new ChangeMemberStatusRequest { Status = MemberStatus.Inactive }, Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────
    private async Task<Guid> CreateUserAsync(string email, MemberStatus status)
    {
        await using var ctx = _db.CreateContext();
        var user = new User
        {
            Email = email,
            PasswordHash = "x",
            FullName = "Miembro",
            Status = status,
            EmailVerified = true
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    public void Dispose() => _db.Dispose();
}
