using Boeshiri.Application.Common;
using Boeshiri.Application.Marketplace;
using Boeshiri.Application.Publications;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Auth;
using Boeshiri.Infrastructure.Marketplace;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Publications;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Boeshiri.Tests.Publications;

/// <summary>
/// Lo que decide la moderación no lo deshace el autor, eliminar es definitivo, y el
/// contenido de alguien suspendido o expulsado sale de lo público mientras dure.
/// </summary>
public class ModeracionTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeFileStorage _storage = new();

    private PublicationService Pubs(BoeshiriDbContext c) => new(c, new AuditLogger(c), _storage);
    private MarketplaceService Market(BoeshiriDbContext c) =>
        new(c, new AuditLogger(c), _storage, Options.Create(new AppOptions { PublicBaseUrl = "https://sitio.test" }));

    private async Task<Guid> Usuario(string email, MemberStatus status = MemberStatus.Active)
    {
        await using var c = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email, Status = status, EmailVerified = true, MarketplaceActive = true };
        c.Users.Add(u);
        await c.SaveChangesAsync();
        return u.Id;
    }

    private async Task<Guid> Publicacion(Guid autor)
    {
        await using var c = _db.CreateContext();
        return await Pubs(c).CreateAsync(autor, new CreatePublicationRequest { Type = PublicationType.Article, Title = "x", Body = "x" }, false);
    }

    private async Task Estado(Guid id, StatusAction accion, Guid quien, bool moderador)
    {
        await using var c = _db.CreateContext();
        await Pubs(c).ChangeStatusAsync(id, accion, quien, moderador);
    }

    [Fact]
    public async Task OcultadaPorModeracion_ElAutorNoLaPuedeMostrar()
    {
        var autor = await Usuario("a@ex.com");
        var mod = await Usuario("m@ex.com");
        var id = await Publicacion(autor);
        await Estado(id, StatusAction.Hide, mod, moderador: true);

        var ex = await Assert.ThrowsAsync<AppException>(() => Estado(id, StatusAction.Show, autor, moderador: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task OcultadaPorModeracion_ElModeradorLaPuedeRestaurar()
    {
        var autor = await Usuario("a@ex.com");
        var mod = await Usuario("m@ex.com");
        var id = await Publicacion(autor);
        await Estado(id, StatusAction.Hide, mod, moderador: true);
        await Estado(id, StatusAction.Show, mod, moderador: true);

        await using var c = _db.CreateContext();
        var p = await c.Publications.SingleAsync(x => x.Id == id);
        Assert.Equal(ContentStatus.Published, p.Status);
        Assert.Null(p.ModeratedAt);
    }

    [Fact]
    public async Task OcultadaPorElAutor_ElAutorLaVuelveAMostrar()
    {
        var autor = await Usuario("a@ex.com");
        var id = await Publicacion(autor);
        await Estado(id, StatusAction.Hide, autor, moderador: false);
        await Estado(id, StatusAction.Show, autor, moderador: false);

        await using var c = _db.CreateContext();
        Assert.Equal(ContentStatus.Published, (await c.Publications.SingleAsync(x => x.Id == id)).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Eliminada_NoSeRecupera(bool laEliminoUnModerador)
    {
        var autor = await Usuario("a@ex.com");
        var mod = await Usuario("m@ex.com");
        var id = await Publicacion(autor);
        await Estado(id, StatusAction.Delete, laEliminoUnModerador ? mod : autor, laEliminoUnModerador);

        var ex = await Assert.ThrowsAsync<AppException>(() => Estado(id, StatusAction.Show, autor, moderador: false));
        Assert.Equal(409, ex.StatusCode);
    }

    [Theory]
    [InlineData(MemberStatus.Suspended, false)]
    [InlineData(MemberStatus.Expelled, false)]
    [InlineData(MemberStatus.Retired, true)]
    public async Task ContenidoDeAutorSancionado_SaleDeLoPublico(MemberStatus status, bool visible)
    {
        var autor = await Usuario("a@ex.com");
        var id = await Publicacion(autor);
        await using (var c = _db.CreateContext())
        {
            (await c.Users.FindAsync(autor))!.Status = status;
            await c.SaveChangesAsync();
        }

        await using var c2 = _db.CreateContext();
        var lista = await Pubs(c2).ListPublicAsync(null, includeMembersOnly: true);
        Assert.Equal(visible, lista.Any(p => p.Id == id));
        if (!visible)
            await Assert.ThrowsAsync<AppException>(() => Pubs(c2).GetDetailAsync(id, true));
    }

    [Fact]
    public async Task Producto_OcultadoPorModeracion_ElDuenoNoLoMuestraNiLoMarcaVendido()
    {
        var dueno = await Usuario("d@ex.com");
        var mod = await Usuario("m@ex.com");
        Guid id;
        await using (var c = _db.CreateContext())
            id = await Market(c).CreateAsync(dueno, new CreateProductRequest { Name = "Lámina", Category = "Arte", Price = 10 });
        await using (var c = _db.CreateContext())
            await Market(c).ChangeStatusAsync(id, ProductStatusAction.Hide, mod, canModerate: true);

        foreach (var accion in new[] { ProductStatusAction.Show, ProductStatusAction.Sold })
        {
            await using var c = _db.CreateContext();
            var ex = await Assert.ThrowsAsync<AppException>(() => Market(c).ChangeStatusAsync(id, accion, dueno, canModerate: false));
            Assert.Equal(403, ex.StatusCode);
        }
    }

    [Fact]
    public async Task Producto_DeVendedorSuspendido_NoSeListaNiSeAbre()
    {
        var dueno = await Usuario("d@ex.com");
        Guid id;
        await using (var c = _db.CreateContext())
            id = await Market(c).CreateAsync(dueno, new CreateProductRequest { Name = "Lámina", Category = "Arte", Price = 10 });
        await using (var c = _db.CreateContext())
        {
            (await c.Users.FindAsync(dueno))!.Status = MemberStatus.Suspended;
            await c.SaveChangesAsync();
        }

        await using var c2 = _db.CreateContext();
        Assert.DoesNotContain(await Market(c2).ListPublicAsync(null, null, null), p => p.Id == id);
        await Assert.ThrowsAsync<AppException>(() => Market(c2).GetDetailAsync(id));
    }

    public void Dispose() => _db.Dispose();
}
