using Boeshiri.Application.Common;
using Boeshiri.Application.Events;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Events;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Events;

public class EventServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly Guid _admin = Guid.NewGuid();

    private EventService NewService(BoeshiriDbContext ctx) =>
        new(ctx, new NotificationService(ctx), new AuditLogger(ctx), new FakeFileStorage());

    private static CreateEventRequest Req(string title = "Evento", Visibility vis = Visibility.Public, DateTime? date = null, List<string>? images = null) =>
        new()
        {
            Category = "Música",
            Title = title,
            Description = "desc",
            Date = date ?? DateTime.UtcNow.AddDays(7),
            Location = "David",
            Cost = 10m,
            Visibility = vis,
            Images = images
        };

    [Fact]
    public async Task ListPublicAsync_Anonymous_ExcludesMembersOnly()
    {
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(_admin, Req("Publico"));
            await svc.CreateAsync(_admin, Req("Exclusivo", Visibility.Members));
        }

        await using var ctx2 = _db.CreateContext();
        var list = await NewService(ctx2).ListPublicAsync(EventWhen.All, includeMembersOnly: false);

        Assert.Single(list);
        Assert.Equal("Publico", list[0].Title);
    }

    [Fact]
    public async Task ListPublicAsync_Upcoming_ReturnsOnlyFutureEvents()
    {
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(_admin, Req("Futuro", date: DateTime.UtcNow.AddDays(3)));
            await svc.CreateAsync(_admin, Req("Pasado", date: DateTime.UtcNow.AddDays(-3)));
        }

        await using var ctx2 = _db.CreateContext();
        var list = await NewService(ctx2).ListPublicAsync(EventWhen.Upcoming, includeMembersOnly: true);

        Assert.Single(list);
        Assert.Equal("Futuro", list[0].Title);
    }

    [Fact]
    public async Task GetDetailAsync_MembersOnlyAnonymous_ThrowsUnauthorized()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(_admin, Req(vis: Visibility.Members));

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).GetDetailAsync(id, authenticated: false));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task GetDetailAsync_Deleted_ThrowsNotFound()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(_admin, Req());
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, EventStatusAction.Delete, _admin);

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).GetDetailAsync(id, authenticated: true));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_MoreThanFourImages_ThrowsBadRequest()
    {
        await using var ctx = _db.CreateContext();
        var req = Req(images: ["a", "b", "c", "d", "e"]);

        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).CreateAsync(_admin, req));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_Valid_CreatesAndAudits()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(_admin, Req("Noche Raíz"));

        await using var check = _db.CreateContext();
        Assert.True(await check.Events.AnyAsync(e => e.Id == id && e.Status == ContentStatus.Published));
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "evento.creado" && a.ActorId == _admin));
    }

    [Fact]
    public async Task RecordAttendanceAsync_AddsAttendeesNotifiesAndFeedsHistory()
    {
        var member = await AddUserAsync("part@ex.com");
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(_admin, Req("Jam"));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).RecordAttendanceAsync(id, new RecordAttendanceRequest { Count = 90, MemberIds = [member] }, _admin);

        await using var check = _db.CreateContext();
        var ev = await check.Events.SingleAsync(e => e.Id == id);
        Assert.Equal(90, ev.AttendanceCount);
        Assert.True(await check.EventAttendees.AnyAsync(a => a.EventId == id && a.UserId == member));
        Assert.Equal(1, await check.Notifications.CountAsync(n => n.UserId == member && n.Type == "evento.asistencia"));

        var history = await NewService(check).ListMyHistoryAsync(member);
        Assert.Single(history);
        Assert.Equal("Jam", history[0].Title);
    }

    // ── Planeación: fecha y costo pueden esperar ─────────────────
    [Fact]
    public async Task CreateAsync_Planning_WithoutDateOrCost_IsListedAsUpcomingAfterDated()
    {
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(_admin, new CreateEventRequest { Category = "Taller", Title = "ARCANA", Planning = true, Visibility = Visibility.Public });
            await svc.CreateAsync(_admin, Req("Con fecha"));
        }

        await using var read = _db.CreateContext();
        var proximos = await NewService(read).ListPublicAsync(EventWhen.Upcoming, includeMembersOnly: false);
        Assert.Equal(["Con fecha", "ARCANA"], proximos.Select(e => e.Title));
        var arcana = proximos.Single(e => e.Title == "ARCANA");
        Assert.True(arcana.Planning);
        Assert.Null(arcana.Date);
        Assert.Null(arcana.Cost);
        Assert.Empty(await NewService(read).ListPublicAsync(EventWhen.Past, includeMembersOnly: false));
    }

    [Theory]
    [InlineData(false, true)]   // confirmado sin costo
    [InlineData(true, false)]   // confirmado sin fecha
    public async Task CreateAsync_Confirmed_MissingDateOrCost_ThrowsBadRequest(bool conFecha, bool conCosto)
    {
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).CreateAsync(_admin, new CreateEventRequest
        {
            Category = "Taller", Title = "x", Planning = false,
            Date = conFecha ? DateTime.UtcNow.AddDays(3) : null,
            Cost = conCosto ? 12m : null,
        }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_EndBeforeStart_ThrowsBadRequest()
    {
        var inicio = DateTime.UtcNow.AddDays(3);
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).CreateAsync(_admin,
            Req("x") with { Date = inicio, EndsAt = inicio.AddHours(-1) }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesImagesAndConfirms()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(_admin, new CreateEventRequest { Category = "Taller", Title = "ARCANA", Planning = true, Images = ["https://cdn.test/publicaciones/a.webp"] });

        var inicio = DateTime.UtcNow.AddDays(30);
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).UpdateAsync(id, new UpdateEventRequest
            {
                Category = "Taller", Title = "ARCANA", Planning = false,
                Date = inicio, EndsAt = inicio.AddHours(3), Cost = 12m,
                Images = ["https://cdn.test/publicaciones/b.webp", "https://cdn.test/publicaciones/c.webp"],
            });

        await using var check = _db.CreateContext();
        var ev = await check.Events.Include(e => e.Images).SingleAsync(e => e.Id == id);
        Assert.False(ev.Planning);
        Assert.Equal(12m, ev.Cost);
        Assert.Equal(["https://cdn.test/publicaciones/b.webp", "https://cdn.test/publicaciones/c.webp"],
            ev.Images.OrderBy(i => i.Order).Select(i => i.Url));
    }

    [Fact]
    public async Task RecordAttendanceAsync_WithoutDate_ThrowsBadRequest()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(_admin, new CreateEventRequest { Category = "Taller", Title = "ARCANA", Planning = true });
        await using var c2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(c2).RecordAttendanceAsync(id, new RecordAttendanceRequest { Count = 10 }, _admin));
        Assert.Equal(400, ex.StatusCode);
    }

    private async Task<Guid> AddUserAsync(string email)
    {
        await using var ctx = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email, EmailVerified = true, Status = MemberStatus.Active };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    public void Dispose() => _db.Dispose();
}
