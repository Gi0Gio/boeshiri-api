using Boeshiri.Domain.Entities;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Notifications;

public class NotificationServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private static NotificationService NewService(BoeshiriDbContext ctx) => new(ctx);

    [Fact]
    public async Task MarkAllReadAsync_OnlyTouchesOwnUnread()
    {
        var yo = await AddUserAsync("yo@ex.com");
        var otro = await AddUserAsync("otro@ex.com");

        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            svc.Notify(yo, "a", "mía 1");
            svc.Notify(yo, "a", "mía 2");
            svc.Notify(otro, "a", "ajena");
            await ctx.SaveChangesAsync();
        }

        int total;
        await using (var ctx = _db.CreateContext())
            total = await NewService(ctx).MarkAllReadAsync(yo);

        await using var check = _db.CreateContext();
        Assert.Equal(2, total);
        Assert.Equal(0, await check.Notifications.CountAsync(n => n.UserId == yo && !n.Read));
        // El aviso del otro usuario sigue sin leer: la operación es por dueño.
        Assert.Equal(1, await check.Notifications.CountAsync(n => n.UserId == otro && !n.Read));
    }

    [Fact]
    public async Task MarkAllReadAsync_NothingPending_ReturnsZero()
    {
        var yo = await AddUserAsync("yo@ex.com");

        await using var ctx = _db.CreateContext();
        Assert.Equal(0, await NewService(ctx).MarkAllReadAsync(yo));
    }

    [Fact]
    public async Task UnreadCount_DropsAfterMarkingAll()
    {
        var yo = await AddUserAsync("yo@ex.com");
        await using (var ctx = _db.CreateContext())
        {
            NewService(ctx).Notify(yo, "a", "pendiente");
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = _db.CreateContext())
            Assert.Equal(1, await NewService(ctx).UnreadCountAsync(yo));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).MarkAllReadAsync(yo);

        await using var check = _db.CreateContext();
        Assert.Equal(0, await NewService(check).UnreadCountAsync(yo));
    }

    private async Task<Guid> AddUserAsync(string email)
    {
        await using var ctx = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    public void Dispose() => _db.Dispose();
}
