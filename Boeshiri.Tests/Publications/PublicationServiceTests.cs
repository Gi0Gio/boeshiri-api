using Boeshiri.Application.Common;
using Boeshiri.Application.Publications;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Publications;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Publications;

public class PublicationServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private readonly FakeFileStorage _storage = new();

    private PublicationService NewService(BoeshiriDbContext ctx) => new(ctx, new AuditLogger(ctx), _storage);

    private static CreatePublicationRequest Article(string title = "Título", string? body = "Cuerpo", Visibility vis = Visibility.Public, List<string>? tags = null) =>
        new() { Type = PublicationType.Article, Title = title, Body = body, Visibility = vis, Tags = tags };

    // ── Reglas de creación por tipo ──────────────────────────────
    [Fact]
    public async Task CreateAsync_ArticleWithoutBody_ThrowsBadRequest()
    {
        var author = await CreateUserAsync();
        await using var ctx = _db.CreateContext();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateAsync(author, Article(body: null), canPublishNews: true));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_NewsWithoutPermission_ThrowsForbidden()
    {
        var author = await CreateUserAsync();
        await using var ctx = _db.CreateContext();
        var req = new CreatePublicationRequest { Type = PublicationType.News, Title = "N", Body = "b" };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateAsync(author, req, canPublishNews: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_VideoWithoutExternalUrl_ThrowsBadRequest()
    {
        var author = await CreateUserAsync();
        await using var ctx = _db.CreateContext();
        var req = new CreatePublicationRequest { Type = PublicationType.Video, Title = "V" };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateAsync(author, req, canPublishNews: true));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_ArticleWithTags_ReusesExistingTagByName()
    {
        var author = await CreateUserAsync();
        await using (var ctx = _db.CreateContext())
        {
            await NewService(ctx).CreateAsync(author, Article(title: "A", tags: ["arte", "cultura"]), true);
            await NewService(ctx).CreateAsync(author, Article(title: "B", tags: ["Arte"]), true);
        }

        await using var check = _db.CreateContext();
        Assert.Equal(2, await check.Tags.CountAsync()); // "arte" reutilizado (case-insensitive)
    }

    // ── Visibilidad y estado en la lectura ───────────────────────
    [Fact]
    public async Task ListPublicAsync_Anonymous_ExcludesMembersOnlyAndNonPublished()
    {
        var author = await CreateUserAsync();
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(author, Article(title: "Pública"), true);
            await svc.CreateAsync(author, Article(title: "Exclusiva", vis: Visibility.Members), true);
        }

        await using var ctx2 = _db.CreateContext();
        var list = await NewService(ctx2).ListPublicAsync(type: null, includeMembersOnly: false);

        Assert.Single(list);
        Assert.Equal("Pública", list[0].Title);
    }

    [Fact]
    public async Task ListPublicAsync_Authenticated_IncludesMembersOnly()
    {
        var author = await CreateUserAsync();
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(author, Article(title: "Pública"), true);
            await svc.CreateAsync(author, Article(title: "Exclusiva", vis: Visibility.Members), true);
        }

        await using var ctx2 = _db.CreateContext();
        var list = await NewService(ctx2).ListPublicAsync(type: null, includeMembersOnly: true);

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task GetDetailAsync_MembersOnlyAnonymous_ThrowsUnauthorized()
    {
        var author = await CreateUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, Article(vis: Visibility.Members), true);

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).GetDetailAsync(id, authenticated: false));
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task GetDetailAsync_Hidden_ThrowsNotFound()
    {
        var author = await CreateUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, Article(), true);
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, StatusAction.Hide, author, canModerate: false);

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).GetDetailAsync(id, authenticated: true));
        Assert.Equal(404, ex.StatusCode);
    }

    // ── Moderación / cambio de estado ────────────────────────────
    [Fact]
    public async Task ChangeStatusAsync_NonOwnerWithoutModerate_ThrowsForbidden()
    {
        var author = await CreateUserAsync("autor@ex.com");
        var stranger = await CreateUserAsync("ajeno@ex.com");
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, Article(), true);

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx2).ChangeStatusAsync(id, StatusAction.Hide, stranger, canModerate: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeStatusAsync_ModeratorHidesOthers_SetsHiddenAndAudits()
    {
        var author = await CreateUserAsync("autor@ex.com");
        var moderator = await CreateUserAsync("mod@ex.com");
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, Article(), true);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, StatusAction.Hide, moderator, canModerate: true);

        await using var check = _db.CreateContext();
        var pub = await check.Publications.SingleAsync(p => p.Id == id);
        Assert.Equal(ContentStatus.Hidden, pub.Status);
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "publicacion.moderada" && a.ActorId == moderator));
    }

    // ── Edición de imágenes (RF-MEM-12, §4.3) ────────────────────

    [Fact]
    public async Task UpdateAsync_ReplacesImagesAndDeletesTheRemovedOnes()
    {
        var author = await CreateUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, new CreatePublicationRequest
            {
                Type = PublicationType.Article, Title = "Con fotos", Body = "Cuerpo",
                Images = ["https://cdn.test/a.webp", "https://cdn.test/b.webp"],
            }, canPublishNews: true);

        // Se queda la primera, se retira la segunda y entra una nueva.
        await using (var ctx = _db.CreateContext())
            await NewService(ctx).UpdateAsync(id, author, new UpdatePublicationRequest
            {
                Title = "Con fotos", Body = "Cuerpo",
                Images = ["https://cdn.test/a.webp", "https://cdn.test/c.webp"],
            });

        await using var check = _db.CreateContext();
        var imgs = await check.PublicationImages.Where(i => i.PublicationId == id).OrderBy(i => i.Order).ToListAsync();

        Assert.Equal(["https://cdn.test/a.webp", "https://cdn.test/c.webp"], imgs.Select(i => i.Url));
        // La retirada deja de estar referenciada: sin borrarla, cada edición
        // acumularía archivos que ya nadie puede ver.
        Assert.Equal(["https://cdn.test/b.webp"], _storage.Deleted);
    }

    [Fact]
    public async Task UpdateAsync_WithoutImagesField_LeavesThemUntouched()
    {
        var author = await CreateUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, new CreatePublicationRequest
            {
                Type = PublicationType.Article, Title = "T", Body = "C",
                Images = ["https://cdn.test/a.webp"],
            }, canPublishNews: true);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).UpdateAsync(id, author, new UpdatePublicationRequest { Title = "Otro", Body = "C" });

        await using var check = _db.CreateContext();
        Assert.Single(await check.PublicationImages.Where(i => i.PublicationId == id).ToListAsync());
        Assert.Empty(_storage.Deleted);
    }

    [Fact]
    public async Task UpdateAsync_MoreThanThreeImages_ThrowsBadRequest()
    {
        var author = await CreateUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, Article(), canPublishNews: true);

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).UpdateAsync(id, author, new UpdatePublicationRequest
        {
            Title = "T", Body = "C",
            Images = ["1", "2", "3", "4"],
        }));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateAsync_PhotoLeftWithoutImages_ThrowsBadRequest()
    {
        var author = await CreateUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, new CreatePublicationRequest
            {
                Type = PublicationType.Photo, Title = "Foto",
                Images = ["https://cdn.test/a.webp"],
            }, canPublishNews: true);

        // El tipo no cambia al editar, así que la regla del alta sigue aplicando.
        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).UpdateAsync(id, author, new UpdatePublicationRequest
        {
            Title = "Foto", Images = [],
        }));

        Assert.Equal(400, ex.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────
    private async Task<Guid> CreateUserAsync(string email = "autor@ex.com")
    {
        await using var ctx = _db.CreateContext();
        var user = new User { Email = email, PasswordHash = "x", FullName = "Autor", EmailVerified = true, Status = MemberStatus.Active };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    public void Dispose() => _db.Dispose();
}
