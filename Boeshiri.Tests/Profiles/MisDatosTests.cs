using Boeshiri.Application.Common;
using Boeshiri.Application.Profiles;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Profiles;
using Boeshiri.Tests.Support;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Profiles;

/// <summary>Ley 81: descargar los datos propios y eliminar la cuenta.</summary>
public class MisDatosTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly PasswordHasher<User> _hasher = new();
    private readonly FakeFileStorage _storage = new();

    private MisDatosService Svc(BoeshiriDbContext c) => new(c, _hasher, _storage, new AuditLogger(c));

    private async Task<Guid> Miembro()
    {
        await using var c = _db.CreateContext();
        var u = new User
        {
            Email = "ana@ex.com", PasswordHash = "", FullName = "Ana Pérez", Phone = "6000-0000", Bio = "Pinto murales",
            Status = MemberStatus.Active, EmailVerified = true, PhotoUrl = "https://cdn.test/avatars/ana.webp"
        };
        u.PasswordHash = _hasher.HashPassword(u, "Secreta123");
        u.SocialLinks.Add(new SocialLink { Type = SocialNetworkType.Instagram, Value = "@ana", Visible = true });
        c.Users.Add(u);
        c.Publications.Add(new Publication { AuthorId = u.Id, Type = PublicationType.Article, Title = "Mi mural", Body = "x" });
        c.Notifications.Add(new Notification { UserId = u.Id, Type = "x", Message = "Hola" });
        c.RefreshTokens.Add(new RefreshToken { UserId = u.Id, TokenHash = new string('b', 64), ExpiresAt = DateTime.UtcNow.AddDays(30) });
        await c.SaveChangesAsync();
        return u.Id;
    }

    [Fact]
    public async Task Exportar_TraeLoQueHayDeLaPersona()
    {
        var id = await Miembro();
        await using var c = _db.CreateContext();
        var d = await Svc(c).ExportarAsync(id);

        Assert.Equal("ana@ex.com", d.Perfil.Email);
        Assert.Equal("6000-0000", d.Perfil.Telefono);
        Assert.Single(d.Redes);
        Assert.Equal("Mi mural", Assert.Single(d.Publicaciones).Titulo);
        Assert.Single(d.Avisos);
    }

    [Fact]
    public async Task Eliminar_BorraLoPersonalYRetiraLoPublicado()
    {
        var id = await Miembro();
        await using (var c = _db.CreateContext())
            await Svc(c).EliminarCuentaAsync(id, new EliminarCuentaRequest { Password = "Secreta123" });

        await using var check = _db.CreateContext();
        var u = await check.Users.Include(x => x.SocialLinks).SingleAsync(x => x.Id == id);
        Assert.Equal(MisDatosService.NombreEliminado, u.FullName);
        Assert.DoesNotContain("ana", u.Email);
        Assert.Null(u.Phone);
        Assert.Null(u.Bio);
        Assert.Null(u.PhotoUrl);
        Assert.Empty(u.SocialLinks);
        Assert.Equal(MemberStatus.Retired, u.Status);
        Assert.All(await check.Publications.Where(p => p.AuthorId == id).ToListAsync(), p => Assert.Equal(ContentStatus.Deleted, p.Status));
        Assert.False(await check.RefreshTokens.AnyAsync(t => t.UserId == id));
        Assert.False(await check.Notifications.AnyAsync(n => n.UserId == id));
        Assert.Contains("https://cdn.test/avatars/ana.webp", _storage.Deleted);
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "cuenta.eliminada"));
    }

    [Fact]
    public async Task Eliminar_ConContrasenaIncorrecta_NoTocaNada()
    {
        var id = await Miembro();
        await using (var c = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<AppException>(() => Svc(c).EliminarCuentaAsync(id, new EliminarCuentaRequest { Password = "mala" }));
            Assert.Equal(400, ex.StatusCode);
        }
        await using var check = _db.CreateContext();
        Assert.Equal("ana@ex.com", (await check.Users.FindAsync(id))!.Email);
    }

    [Fact]
    public async Task Eliminar_QuienCoordina_TieneQueNombrarAOtroAntes()
    {
        var id = await Miembro();
        await using (var c = _db.CreateContext())
        {
            var g = new Group { Name = "Música", Type = GroupType.Commission };
            c.Groups.Add(g);
            c.GroupMemberships.Add(new GroupMembership { GroupId = g.Id, UserId = id, Role = GroupRole.Coordinator });
            await c.SaveChangesAsync();
        }
        await using var c2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => Svc(c2).EliminarCuentaAsync(id, new EliminarCuentaRequest { Password = "Secreta123" }));
        Assert.Equal(409, ex.StatusCode);
        Assert.Contains("Música", ex.Message);
    }

    public void Dispose() => _db.Dispose();
}
