using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Common;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Storage;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boeshiri.Tests.Storage;

public class FileManagerServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeFileStorage _storage = new();

    private FileManagerService NewService(BoeshiriDbContext ctx) =>
        new(ctx, _storage, new AuditLogger(ctx), NullLogger<FileManagerService>.Instance);

    private const string UrlUso = "https://cdn.test/publicaciones/en-uso.webp";
    private const string UrlPapelera = "https://cdn.test/publicaciones/papelera.webp";
    private const string UrlHuerfano = "https://cdn.test/misc/nadie.webp";

    [Fact]
    public async Task ListAsync_ClassifiesInUseTrashAndOrphan()
    {
        await SembrarAsync();

        await using var ctx = _db.CreateContext();
        var archivos = await NewService(ctx).ListAsync(null);

        Assert.Equal(FileUsage.InUse, archivos.Single(f => f.Url == UrlUso).Usage);
        Assert.Equal(FileUsage.Trash, archivos.Single(f => f.Url == UrlPapelera).Usage);
        Assert.Equal(FileUsage.Orphan, archivos.Single(f => f.Url == UrlHuerfano).Usage);

        // El dueño se identifica para que el gestor pueda decir de quién es.
        var enUso = archivos.Single(f => f.Url == UrlUso);
        Assert.Equal("Publicación", enUso.OwnerType);
        Assert.Equal("Viva", enUso.OwnerName);
    }

    [Fact]
    public async Task DeleteAsync_InUse_ThrowsConflict()
    {
        await SembrarAsync();

        // Es la garantía central: borrar esto dejaría un enlace roto en el sitio.
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).DeleteAsync(Key(UrlUso), Guid.NewGuid()));

        Assert.Equal(409, ex.StatusCode);
        Assert.Empty(_storage.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_Orphan_RemovesFromBucket()
    {
        await SembrarAsync();

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).DeleteAsync(Key(UrlHuerfano), Guid.NewGuid());

        Assert.Equal([Key(UrlHuerfano)], _storage.Deleted);
    }

    [Fact]
    public async Task EmptyTrash_RemovesFilesAndImageRowsButKeepsEntity()
    {
        await SembrarAsync();
        var actor = Guid.NewGuid();

        int total;
        await using (var ctx = _db.CreateContext())
            total = await NewService(ctx).EmptyTrashAsync(actor);

        Assert.Equal(1, total);
        Assert.Equal([Key(UrlPapelera)], _storage.Deleted);

        await using var check = _db.CreateContext();
        // La fila de imagen se va...
        Assert.False(await check.PublicationImages.AnyAsync(i => i.Url == UrlPapelera));
        // ...pero la publicación se conserva con su estado, para no perder el rastro.
        Assert.True(await check.Publications.AnyAsync(p => p.Title == "Borrada" && p.Status == ContentStatus.Deleted));
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "archivo.papelera_vaciada" && a.ActorId == actor));
    }

    [Fact]
    public async Task EmptyTrash_LeavesInUseAndOrphansAlone()
    {
        await SembrarAsync();

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).EmptyTrashAsync(Guid.NewGuid());

        // Vaciar la papelera no toca lo demás: el huérfano se borra aparte.
        Assert.DoesNotContain(Key(UrlUso), _storage.Deleted);
        Assert.DoesNotContain(Key(UrlHuerfano), _storage.Deleted);
    }

    [Fact]
    public async Task ProfilePhoto_IsAlwaysInUse()
    {
        var url = "https://cdn.test/avatars/yo.webp";
        await using (var ctx = _db.CreateContext())
        {
            ctx.Users.Add(new User { Email = "a@ex.com", PasswordHash = "x", FullName = "Ana", PhotoUrl = url });
            await ctx.SaveChangesAsync();
        }
        _storage.Objects.Add(new StoredObject(Key(url), url, 10, DateTime.UtcNow));

        await using var ctx2 = _db.CreateContext();
        var archivo = (await NewService(ctx2).ListAsync(null)).Single(f => f.Url == url);

        Assert.Equal(FileUsage.InUse, archivo.Usage);
        Assert.Equal("Perfil", archivo.OwnerType);
    }

    // ── Helpers ──────────────────────────────────────────────────
    private static string Key(string url) => url.Replace("https://cdn.test/", "");

    private async Task SembrarAsync()
    {
        await using var ctx = _db.CreateContext();
        var autor = new User { Email = "autor@ex.com", PasswordHash = "x", FullName = "Autor" };
        ctx.Users.Add(autor);

        var viva = new Publication { AuthorId = autor.Id, Type = PublicationType.Article, Title = "Viva", Status = ContentStatus.Published };
        viva.Images.Add(new PublicationImage { Url = UrlUso, Order = 0 });

        var borrada = new Publication { AuthorId = autor.Id, Type = PublicationType.Article, Title = "Borrada", Status = ContentStatus.Deleted };
        borrada.Images.Add(new PublicationImage { Url = UrlPapelera, Order = 0 });

        ctx.Publications.AddRange(viva, borrada);
        await ctx.SaveChangesAsync();

        foreach (var u in new[] { UrlUso, UrlPapelera, UrlHuerfano })
            _storage.Objects.Add(new StoredObject(Key(u), u, 10, DateTime.UtcNow));
    }

    public void Dispose() => _db.Dispose();
}
