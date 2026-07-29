using Boeshiri.Application.Common;
using Boeshiri.Application.Groups;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Groups;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Groups;

public class KanbanServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private KanbanService NewService(BoeshiriDbContext ctx) => new(ctx);

    [Fact]
    public async Task GetBoardAsync_NonMember_ThrowsForbidden()
    {
        var group = await AddGroupAsync();
        var stranger = await AddUserAsync("ajeno@ex.com");

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).GetBoardAsync(group, stranger));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task CreateTaskAsync_Leader_Creates()
    {
        var group = await AddGroupAsync();
        var leader = await AddMemberAsync(group, GroupRole.Leader);

        Guid taskId;
        await using (var ctx = _db.CreateContext())
            taskId = await NewService(ctx).CreateTaskAsync(group, leader, new CreateTaskRequest { Title = "Tarea 1" });

        await using var check = _db.CreateContext();
        Assert.True(await check.KanbanTasks.AnyAsync(t => t.Id == taskId && t.Status == KanbanStatus.Pending));
    }

    [Fact]
    public async Task CreateTaskAsync_PlainMember_ThrowsForbidden()
    {
        var group = await AddGroupAsync();
        var member = await AddMemberAsync(group, GroupRole.Member);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateTaskAsync(group, member, new CreateTaskRequest { Title = "X" }));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task MoveTaskAsync_Leader_MovesToAnyColumn()
    {
        var group = await AddGroupAsync();
        var leader = await AddMemberAsync(group, GroupRole.Leader);
        var taskId = await AddTaskAsync(group, leader);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).MoveTaskAsync(taskId, KanbanStatus.InProgress, leader);

        await using var check = _db.CreateContext();
        Assert.Equal(KanbanStatus.InProgress, (await check.KanbanTasks.SingleAsync(t => t.Id == taskId)).Status);
    }

    [Fact]
    public async Task MoveTaskAsync_AssigneeCanMarkDone_ButNotArbitraryColumn()
    {
        var group = await AddGroupAsync();
        var leader = await AddMemberAsync(group, GroupRole.Leader);
        var assignee = await AddMemberAsync(group, GroupRole.Member, "resp@ex.com");
        var taskId = await AddTaskAsync(group, leader, assignee);

        // Puede marcar Completado (RF-KAN-03)
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).MoveTaskAsync(taskId, KanbanStatus.Done, assignee);
        await using (var check = _db.CreateContext())
            Assert.Equal(KanbanStatus.Done, (await check.KanbanTasks.SingleAsync(t => t.Id == taskId)).Status);

        // Pero NO puede moverla a En proceso
        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx2).MoveTaskAsync(taskId, KanbanStatus.InProgress, assignee));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task MoveTaskAsync_NonAssigneeMember_ThrowsForbidden()
    {
        var group = await AddGroupAsync();
        var leader = await AddMemberAsync(group, GroupRole.Leader);
        var other = await AddMemberAsync(group, GroupRole.Member, "otro@ex.com");
        var taskId = await AddTaskAsync(group, leader);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).MoveTaskAsync(taskId, KanbanStatus.Done, other));
        Assert.Equal(403, ex.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────
    private async Task<Guid> AddGroupAsync()
    {
        await using var ctx = _db.CreateContext();
        var g = new Group { Name = "Equipo", Type = GroupType.Team };
        ctx.Groups.Add(g);
        await ctx.SaveChangesAsync();
        return g.Id;
    }

    private async Task<Guid> AddUserAsync(string email)
    {
        await using var ctx = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email, EmailVerified = true, Status = MemberStatus.Active };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private async Task<Guid> AddMemberAsync(Guid groupId, GroupRole role, string email = "u@ex.com")
    {
        var userId = await AddUserAsync(email);
        await using var ctx = _db.CreateContext();
        ctx.GroupMemberships.Add(new GroupMembership { GroupId = groupId, UserId = userId, Role = role });
        await ctx.SaveChangesAsync();
        return userId;
    }

    private async Task<Guid> AddTaskAsync(Guid groupId, Guid createdBy, Guid? assignee = null)
    {
        await using var ctx = _db.CreateContext();
        var task = new KanbanTask { GroupId = groupId, Title = "T", CreatedBy = createdBy };
        if (assignee is not null)
            task.Assignees.Add(new KanbanTaskAssignee { UserId = assignee.Value });
        ctx.KanbanTasks.Add(task);
        await ctx.SaveChangesAsync();
        return task.Id;
    }

    public void Dispose() => _db.Dispose();
}
