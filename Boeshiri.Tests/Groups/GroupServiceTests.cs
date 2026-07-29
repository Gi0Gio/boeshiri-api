using Boeshiri.Application.Common;
using Boeshiri.Application.Groups;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Groups;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Groups;

public class GroupServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private GroupService NewService(BoeshiriDbContext ctx) =>
        new(ctx, new NotificationService(ctx), new AuditLogger(ctx));

    [Fact]
    public async Task RequestJoinAsync_CreatesPendingRequest()
    {
        var commission = await AddCommissionAsync("Tecnología");
        var user = await AddUserAsync("aspirante@ex.com");

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RequestJoinAsync(commission, user);

        await using var check = _db.CreateContext();
        Assert.Equal(1, await check.JoinRequests.CountAsync(r => r.CommissionId == commission && r.Status == JoinRequestStatus.Pending));
    }

    [Fact]
    public async Task RequestJoinAsync_AlreadyMember_ThrowsConflict()
    {
        var commission = await AddCommissionAsync("Tecnología");
        var user = await AddUserAsync("miembro@ex.com");
        await AddMembershipAsync(commission, user, GroupRole.Member);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).RequestJoinAsync(commission, user));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task ListJoinRequestsAsync_Coordinator_ReturnsRequests()
    {
        var commission = await AddCommissionAsync("Tecnología");
        var coordinator = await AddUserAsync("coord@ex.com");
        await AddMembershipAsync(commission, coordinator, GroupRole.Coordinator);
        var applicant = await AddUserAsync("aspirante@ex.com");
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RequestJoinAsync(commission, applicant);

        await using var ctx2 = _db.CreateContext();
        var requests = await NewService(ctx2).ListJoinRequestsAsync(commission, coordinator, canManageGlobally: false);

        Assert.Single(requests);
        Assert.Equal("aspirante@ex.com", requests[0].UserEmail);
    }

    [Fact]
    public async Task ListJoinRequestsAsync_NonCoordinatorWithoutGlobal_ThrowsForbidden()
    {
        var commission = await AddCommissionAsync("Tecnología");
        var stranger = await AddUserAsync("ajeno@ex.com");

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).ListJoinRequestsAsync(commission, stranger, canManageGlobally: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ListJoinRequestsAsync_GlobalManager_Allowed()
    {
        var commission = await AddCommissionAsync("Tecnología");
        var junta = await AddUserAsync("junta@ex.com");

        await using var ctx = _db.CreateContext();
        var requests = await NewService(ctx).ListJoinRequestsAsync(commission, junta, canManageGlobally: true);

        Assert.Empty(requests); // permitido (no lanza), sin pendientes
    }

    [Fact]
    public async Task DecideJoinAsync_Accept_AddsMembershipNotifiesAndAudits()
    {
        var commission = await AddCommissionAsync("Tecnología");
        var coordinator = await AddUserAsync("coord@ex.com");
        await AddMembershipAsync(commission, coordinator, GroupRole.Coordinator);
        var applicant = await AddUserAsync("aspirante@ex.com");
        Guid requestId;
        await using (var ctx = _db.CreateContext())
        {
            await NewService(ctx).RequestJoinAsync(commission, applicant);
        }
        await using (var q = _db.CreateContext())
            requestId = (await q.JoinRequests.SingleAsync(r => r.UserId == applicant)).Id;

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).DecideJoinAsync(requestId, JoinDecision.Accept, coordinator, canManageGlobally: false);

        await using var check = _db.CreateContext();
        Assert.True(await check.GroupMemberships.AnyAsync(m => m.GroupId == commission && m.UserId == applicant));
        Assert.Equal(1, await check.Notifications.CountAsync(n => n.UserId == applicant && n.Type == "comision.ingreso_aceptado"));
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "comision.ingreso_aceptado"));
    }

    [Fact]
    public async Task DecideJoinAsync_NonCoordinator_ThrowsForbidden()
    {
        var commission = await AddCommissionAsync("Tecnología");
        var applicant = await AddUserAsync("aspirante@ex.com");
        var stranger = await AddUserAsync("ajeno@ex.com");
        Guid requestId;
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RequestJoinAsync(commission, applicant);
        await using (var q = _db.CreateContext())
            requestId = (await q.JoinRequests.SingleAsync()).Id;

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx2).DecideJoinAsync(requestId, JoinDecision.Accept, stranger, canManageGlobally: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task CreateTeamAsync_Coordinator_CreatesTeamWithLeader()
    {
        var commission = await AddCommissionAsync("Tecnología");
        var coordinator = await AddUserAsync("coord@ex.com");
        await AddMembershipAsync(commission, coordinator, GroupRole.Coordinator);
        var leader = await AddUserAsync("lider@ex.com");

        Guid teamId;
        await using (var ctx = _db.CreateContext())
            teamId = await NewService(ctx).CreateTeamAsync(commission, new CreateTeamRequest { Name = "Equipo A", LeaderUserId = leader }, coordinator, canManageGlobally: false);

        await using var check = _db.CreateContext();
        var team = await check.Groups.SingleAsync(g => g.Id == teamId);
        Assert.Equal(GroupType.Team, team.Type);
        Assert.Equal(commission, team.ParentCommissionId);
        Assert.True(await check.GroupMemberships.AnyAsync(m => m.GroupId == teamId && m.UserId == leader && m.Role == GroupRole.Leader));
    }

    // ── Helpers ──────────────────────────────────────────────────
    private async Task<Guid> AddUserAsync(string email)
    {
        await using var ctx = _db.CreateContext();
        var user = new User { Email = email, PasswordHash = "x", FullName = email, EmailVerified = true, Status = MemberStatus.Active };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    private async Task<Guid> AddCommissionAsync(string name)
    {
        await using var ctx = _db.CreateContext();
        var g = new Group { Name = name, Type = GroupType.Commission, Permanent = true };
        ctx.Groups.Add(g);
        await ctx.SaveChangesAsync();
        return g.Id;
    }

    private async Task AddMembershipAsync(Guid groupId, Guid userId, GroupRole role)
    {
        await using var ctx = _db.CreateContext();
        ctx.GroupMemberships.Add(new GroupMembership { GroupId = groupId, UserId = userId, Role = role });
        await ctx.SaveChangesAsync();
    }

    public void Dispose() => _db.Dispose();
}
