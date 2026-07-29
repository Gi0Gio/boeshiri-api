using Boeshiri.Application.Common;
using Boeshiri.Application.Transparency;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Transparency;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Transparency;

public class TransparencyServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private TransparencyService NewService(BoeshiriDbContext ctx) =>
        new(ctx, new NotificationService(ctx), new AuditLogger(ctx));

    private static CreateTransparencyRequest Req(string title = "Informe") =>
        new() { Title = title, Body = "cuerpo", Category = "Informe" };

    [Fact]
    public async Task CreateAsync_NotifiesAllActiveMembersExceptAuthor()
    {
        var author = await AddUserAsync("junta@ex.com", MemberStatus.Active);
        await AddUserAsync("m1@ex.com", MemberStatus.Active);
        await AddUserAsync("m2@ex.com", MemberStatus.Active);
        await AddUserAsync("postulante@ex.com", MemberStatus.Applicant); // no recibe

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).CreateAsync(author, Req());

        await using var check = _db.CreateContext();
        var notifs = await check.Notifications.Where(n => n.Type == "transparencia.publicada").ToListAsync();
        Assert.Equal(2, notifs.Count); // m1 y m2; ni el autor ni el postulante
        Assert.DoesNotContain(notifs, n => n.UserId == author);
    }

    [Fact]
    public async Task CreateAsync_Audits()
    {
        var author = await AddUserAsync("junta@ex.com", MemberStatus.Active);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).CreateAsync(author, Req("Resultados 2026"));

        await using var check = _db.CreateContext();
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "transparencia.publicada"));
    }

    [Fact]
    public async Task ListAsync_MembersSeeOnlyPublished_ManagersCanIncludeHidden()
    {
        var author = await AddUserAsync("junta@ex.com", MemberStatus.Active);
        Guid hiddenId;
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(author, Req("Publicado"));
            hiddenId = await svc.CreateAsync(author, Req("Oculto"));
            await svc.ChangeStatusAsync(hiddenId, TransparencyStatusAction.Hide, author);
        }

        await using var ctx2 = _db.CreateContext();
        var published = await NewService(ctx2).ListAsync(includeHidden: false);
        var all = await NewService(ctx2).ListAsync(includeHidden: true);

        Assert.Single(published);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetDetailAsync_Hidden_ThrowsNotFound()
    {
        var author = await AddUserAsync("junta@ex.com", MemberStatus.Active);
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, Req());
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, TransparencyStatusAction.Hide, author);

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).GetDetailAsync(id));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ThrowsNotFound()
    {
        var author = await AddUserAsync("junta@ex.com", MemberStatus.Active);
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).UpdateAsync(Guid.NewGuid(), author,
                new UpdateTransparencyRequest { Title = "t", Body = "b", Category = "c" }));
        Assert.Equal(404, ex.StatusCode);
    }

    private async Task<Guid> AddUserAsync(string email, MemberStatus status)
    {
        await using var ctx = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email, EmailVerified = true, Status = status };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    public void Dispose() => _db.Dispose();
}
