using Boeshiri.Application.Common;
using Boeshiri.Application.Documents;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Documents;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Documents;

public class DocumentServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private readonly FakeFileStorage _storage = new();

    private DocumentService NewService(BoeshiriDbContext ctx) => new(ctx, new AuditLogger(ctx), _storage);

    private static CreateDocumentRequest Req(
        DocumentLibrary library = DocumentLibrary.Community,
        DocumentAccessLevel access = DocumentAccessLevel.Members,
        string name = "Doc") =>
        new() { Name = name, Category = "Investigacion", Library = library, AccessLevel = access, FileUrl = "https://r2/doc.pdf" };

    // ── Autorización de subida (RF-DOC-03/05) ────────────────────
    [Fact]
    public async Task CreateAsync_CommunityWithoutUploadPermission_ThrowsForbidden()
    {
        var user = await AddUserAsync();
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateAsync(user, Req(), canUploadCommunity: false, canManageAdmin: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_CommunityWithPermission_Creates()
    {
        var user = await AddUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(user, Req(), canUploadCommunity: true, canManageAdmin: false);

        await using var check = _db.CreateContext();
        Assert.True(await check.Documents.AnyAsync(d => d.Id == id && d.Library == DocumentLibrary.Community));
    }

    [Fact]
    public async Task CreateAsync_AdminLibraryWithoutAdminPermission_ThrowsForbidden()
    {
        var user = await AddUserAsync();
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateAsync(user, Req(DocumentLibrary.Administration), canUploadCommunity: true, canManageAdmin: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_AdminAccessLevel_RequiresAdminPermission()
    {
        var user = await AddUserAsync();
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateAsync(user, Req(access: DocumentAccessLevel.Administration), canUploadCommunity: true, canManageAdmin: false));
        Assert.Equal(403, ex.StatusCode);
    }

    // ── Visibilidad por nivel de acceso (RF-DOC-05) ──────────────
    [Fact]
    public async Task ListAsync_NonAdmin_ExcludesAdminLevelDocuments()
    {
        var admin = await AddUserAsync();
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(admin, Req(name: "Miembros"), canUploadCommunity: true, canManageAdmin: true);
            await svc.CreateAsync(admin, Req(DocumentLibrary.Administration, DocumentAccessLevel.Administration, "Balance"), canUploadCommunity: true, canManageAdmin: true);
        }

        await using var ctx2 = _db.CreateContext();
        var visible = await NewService(ctx2).ListAsync(library: null, category: null, canViewAdmin: false);

        Assert.Single(visible);
        Assert.Equal("Miembros", visible[0].Name);
    }

    [Fact]
    public async Task ListAsync_Admin_SeesAllAndCanFilterByLibrary()
    {
        var admin = await AddUserAsync();
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(admin, Req(name: "Comunidad"), canUploadCommunity: true, canManageAdmin: true);
            await svc.CreateAsync(admin, Req(DocumentLibrary.Administration, DocumentAccessLevel.Administration, "Admin"), canUploadCommunity: true, canManageAdmin: true);
        }

        await using var ctx2 = _db.CreateContext();
        var all = await NewService(ctx2).ListAsync(null, null, canViewAdmin: true);
        var adminLib = await NewService(ctx2).ListAsync(DocumentLibrary.Administration, null, canViewAdmin: true);

        Assert.Equal(2, all.Count);
        Assert.Single(adminLib);
    }

    [Fact]
    public async Task GetAsync_AdminLevel_NonAdmin_ThrowsForbidden()
    {
        var admin = await AddUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(admin, Req(DocumentLibrary.Administration, DocumentAccessLevel.Administration), canUploadCommunity: true, canManageAdmin: true);

        await using var ctx2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx2).GetAsync(id, canViewAdmin: false));
        Assert.Equal(403, ex.StatusCode);
    }

    // ── Reemplazo y borrado (RF-DOC-01) ──────────────────────────
    [Fact]
    public async Task ReplaceAsync_NonAuthorNonAdmin_ThrowsForbidden()
    {
        var author = await AddUserAsync("a@ex.com");
        var stranger = await AddUserAsync("b@ex.com");
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, Req(), canUploadCommunity: true, canManageAdmin: false);

        await using var ctx2 = _db.CreateContext();
        var req = new ReplaceDocumentRequest { Name = "N", Category = "C", FileUrl = "https://r2/v2.pdf" };
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx2).ReplaceAsync(id, stranger, req, canManageAdmin: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ReplaceAsync_Author_OverwritesAndSetsUpdatedAt()
    {
        var author = await AddUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, Req(), canUploadCommunity: true, canManageAdmin: false);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ReplaceAsync(id, author,
                new ReplaceDocumentRequest { Name = "Nuevo", Category = "Investigacion", FileUrl = "https://r2/v2.pdf" },
                canManageAdmin: false);

        await using var check = _db.CreateContext();
        var doc = await check.Documents.SingleAsync(d => d.Id == id);
        Assert.Equal("https://r2/v2.pdf", doc.FileUrl); // sobrescrito, sin versiones
        Assert.NotNull(doc.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_AdminDeletesOthersAndAudits()
    {
        var author = await AddUserAsync("a@ex.com");
        var admin = await AddUserAsync("admin@ex.com");
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(author, Req(), canUploadCommunity: true, canManageAdmin: false);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).DeleteAsync(id, admin, canManageAdmin: true);

        await using var check = _db.CreateContext();
        Assert.False(await check.Documents.AnyAsync(d => d.Id == id));
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "documento.eliminado"));
    }

    private async Task<Guid> AddUserAsync(string email = "u@ex.com")
    {
        await using var ctx = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email, EmailVerified = true, Status = MemberStatus.Active };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    [Fact]
    public async Task DeleteAsync_AlsoRemovesFileFromStorage()
    {
        var autor = await AddUserAsync();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req(), canUploadCommunity: true, canManageAdmin: true);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).DeleteAsync(id, autor, canManageAdmin: true);

        // Sin esto el objeto quedaría en el bucket sin que ninguna fila lo referencie.
        Assert.Single(_storage.Deleted);
    }

    public void Dispose() => _db.Dispose();
}
