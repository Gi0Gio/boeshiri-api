using Boeshiri.Application.Common;
using Boeshiri.Application.Marketplace;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Auth;
using Boeshiri.Infrastructure.Marketplace;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Boeshiri.Tests.Marketplace;

public class MarketplaceServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private MarketplaceService NewService(BoeshiriDbContext ctx) =>
        new(ctx, new AuditLogger(ctx), Options.Create(new AppOptions { PublicBaseUrl = "http://test" }));

    private static CreateProductRequest Req(string name = "Lamina", string category = "Arte", decimal price = 25m) =>
        new() { Name = name, Category = category, Price = price, Description = "d", DeliveryLocation = "David" };

    [Fact]
    public async Task CreateAsync_NotEnrolled_ThrowsForbidden()
    {
        var seller = await AddUserAsync("s@ex.com", enrolled: false);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).CreateAsync(seller, Req()));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_Enrolled_Creates()
    {
        var seller = await AddUserAsync("s@ex.com", enrolled: true);

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(seller, Req());

        await using var check = _db.CreateContext();
        Assert.True(await check.Products.AnyAsync(p => p.Id == id && p.Status == ProductStatus.Published));
    }

    [Fact]
    public async Task ListPublicAsync_ExcludesNonPublishedAndFiltersByCategory()
    {
        var seller = await AddUserAsync("s@ex.com", enrolled: true);
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(seller, Req("Lamina", "Arte"));
            var musicaId = await svc.CreateAsync(seller, Req("Vinilo", "Musica"));
            await svc.ChangeStatusAsync(musicaId, ProductStatusAction.Hide, seller, canModerate: false);
        }

        await using var ctx2 = _db.CreateContext();
        var arte = await NewService(ctx2).ListPublicAsync(name: null, category: "Arte");
        var musica = await NewService(ctx2).ListPublicAsync(name: null, category: "Musica");

        Assert.Single(arte);
        Assert.Empty(musica); // el de Música quedó oculto
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsSellerContactRespectingPrivacy()
    {
        var seller = await AddUserAsync("vendedor@ex.com", enrolled: true, cfg: u =>
        {
            u.Phone = "+50760000000";
            u.ShowEmail = true;
            u.ShowPhone = false;
        });
        await using (var ctx = _db.CreateContext())
        {
            ctx.SocialLinks.Add(new SocialLink { UserId = seller, Type = SocialNetworkType.Instagram, Value = "@art", Visible = true });
            await ctx.SaveChangesAsync();
        }
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(seller, Req());

        await using var check = _db.CreateContext();
        var detail = await NewService(check).GetDetailAsync(id);

        Assert.Equal("vendedor@ex.com", detail.Contact.Email);
        Assert.Null(detail.Contact.Phone);
        Assert.Single(detail.Contact.SocialLinks);
    }

    [Fact]
    public async Task GetDetailAsync_Hidden_ThrowsNotFound()
    {
        var seller = await AddUserAsync("s@ex.com", enrolled: true);
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(seller, Req());
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, ProductStatusAction.Hide, seller, canModerate: false);

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).GetDetailAsync(id));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeStatusAsync_NonOwnerWithoutModerate_ThrowsForbidden()
    {
        var seller = await AddUserAsync("s@ex.com", enrolled: true);
        var stranger = await AddUserAsync("x@ex.com", enrolled: false);
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(seller, Req());

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx2).ChangeStatusAsync(id, ProductStatusAction.Hide, stranger, canModerate: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeStatusAsync_ModeratorHidesOthers_SetsHiddenAndAudits()
    {
        var seller = await AddUserAsync("s@ex.com", enrolled: true);
        var moderator = await AddUserAsync("mod@ex.com", enrolled: false);
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(seller, Req());

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, ProductStatusAction.Hide, moderator, canModerate: true);

        await using var check = _db.CreateContext();
        Assert.Equal(ProductStatus.Hidden, (await check.Products.SingleAsync(p => p.Id == id)).Status);
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "producto.moderado"));
    }

    [Fact]
    public async Task ChangeStatusAsync_ModeratorCannotMarkOthersSold()
    {
        var seller = await AddUserAsync("s@ex.com", enrolled: true);
        var moderator = await AddUserAsync("mod@ex.com", enrolled: false);
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(seller, Req());

        // "Vendido" es acción del dueño; un moderador no puede aunque tenga canModerate.
        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx2).ChangeStatusAsync(id, ProductStatusAction.Sold, moderator, canModerate: true));
        Assert.Equal(403, ex.StatusCode);
    }

    private async Task<Guid> AddUserAsync(string email, bool enrolled, Action<User>? cfg = null)
    {
        await using var ctx = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email, EmailVerified = true, Status = MemberStatus.Active, MarketplaceActive = enrolled };
        cfg?.Invoke(u);
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    public void Dispose() => _db.Dispose();
}
