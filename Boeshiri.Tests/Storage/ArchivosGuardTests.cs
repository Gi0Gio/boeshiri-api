using Boeshiri.Application.Common;
using Boeshiri.Application.Documents;
using Boeshiri.Application.Groups;
using Boeshiri.Application.Profiles;
using Boeshiri.Application.Publications;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Documents;
using Boeshiri.Infrastructure.Groups;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Profiles;
using Boeshiri.Infrastructure.Publications;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Storage;

/// <summary>
/// Las URLs de archivos vienen del cliente. Un miembro no puede usar una URL ajena
/// para que el servidor borre del bucket un archivo de otra persona, ni guardar
/// direcciones que no sean nuestras ni enlaces que no sean web.
/// </summary>
public class ArchivosGuardTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly FakeFileStorage _storage = new();

    private const string AvatarVictima = "https://cdn.test/avatars/victima.webp";
    private const string ActaJunta = "https://cdn.test/documentos/acta.pdf";

    private async Task<Guid> Usuario(string email, string? foto = null)
    {
        await using var ctx = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email, Status = MemberStatus.Active, EmailVerified = true, PhotoUrl = foto };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private ProfileService Perfil(BoeshiriDbContext ctx) => new(ctx, _storage);
    private PublicationService Pubs(BoeshiriDbContext ctx) => new(ctx, new AuditLogger(ctx), _storage);

    [Fact]
    public async Task Perfil_UrlAjenaEnUso_NoSeBorraAlCambiarLaFoto()
    {
        await Usuario("victima@ex.com", foto: AvatarVictima);
        var atacante = await Usuario("atacante@ex.com");

        await using (var ctx = _db.CreateContext())
            await Perfil(ctx).UpdateProfileAsync(atacante, new UpdateProfileRequest { FullName = "A", PhotoUrl = AvatarVictima });
        await using (var ctx = _db.CreateContext())
            await Perfil(ctx).UpdateProfileAsync(atacante, new UpdateProfileRequest { FullName = "A", PhotoUrl = null });

        Assert.DoesNotContain(AvatarVictima, _storage.Deleted);
    }

    [Fact]
    public async Task Perfil_CambiarLaFotoPropia_SueltaLaAnterior()
    {
        var yo = await Usuario("yo@ex.com", foto: "https://cdn.test/avatars/vieja.webp");

        await using (var ctx = _db.CreateContext())
            await Perfil(ctx).UpdateProfileAsync(yo, new UpdateProfileRequest { FullName = "Yo", PhotoUrl = "https://cdn.test/avatars/nueva.webp" });

        Assert.Equal(["https://cdn.test/avatars/vieja.webp"], _storage.Deleted);
    }

    [Theory]
    [InlineData("https://otro-sitio.test/avatars/x.webp")]   // no es nuestro bucket
    [InlineData(ActaJunta)]                                 // es nuestro, pero un documento
    [InlineData("https://cdn.test/avatars/../documentos/acta.pdf")]
    public async Task Perfil_FotoQueNoEsUnaImagenNuestra_SeRechaza(string url)
    {
        var yo = await Usuario("yo@ex.com");
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Perfil(ctx).UpdateProfileAsync(yo, new UpdateProfileRequest { FullName = "Yo", PhotoUrl = url }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Perfil_FotoExternaDeAntes_SeConservaAlEditar()
    {
        // Datos guardados antes de la regla: editar el nombre no obliga a cambiar la foto.
        var yo = await Usuario("yo@ex.com", foto: "https://externo.test/foto.jpg");
        await using var ctx = _db.CreateContext();
        await Perfil(ctx).UpdateProfileAsync(yo, new UpdateProfileRequest { FullName = "Nuevo", PhotoUrl = "https://externo.test/foto.jpg" });
    }

    [Fact]
    public async Task Publicacion_DocumentoComoImagen_SeRechaza()
    {
        var autor = await Usuario("a@ex.com");
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => Pubs(ctx).CreateAsync(autor, new CreatePublicationRequest
        {
            Type = PublicationType.Article, Title = "x", Body = "x", Images = [ActaJunta]
        }, canPublishNews: false));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Publicacion_ImagenQueOtroUsa_NoSeBorraAlQuitarla()
    {
        var victima = await Usuario("v@ex.com", foto: AvatarVictima);
        var autor = await Usuario("a@ex.com");
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await Pubs(ctx).CreateAsync(autor, new CreatePublicationRequest
            {
                Type = PublicationType.Photo, Title = "x", Images = [AvatarVictima, "https://cdn.test/publicaciones/mia.webp"]
            }, canPublishNews: false);
        await using (var ctx = _db.CreateContext())
            await Pubs(ctx).UpdateAsync(id, autor, new UpdatePublicationRequest
            {
                Title = "x", Images = ["https://cdn.test/publicaciones/mia.webp"]
            });

        Assert.DoesNotContain(AvatarVictima, _storage.Deleted);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://x.test/y")]
    [InlineData("no-es-un-enlace")]
    public async Task Publicacion_EnlaceQueNoEsWeb_SeRechaza(string url)
    {
        var autor = await Usuario("a@ex.com");
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => Pubs(ctx).CreateAsync(autor, new CreatePublicationRequest
        {
            Type = PublicationType.Video, Title = "x", ExternalUrl = url
        }, canPublishNews: false));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Documento_ArchivoQueNoEsDeDocumentos_SeRechaza()
    {
        var autor = await Usuario("a@ex.com");
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            new DocumentService(ctx, new AuditLogger(ctx), _storage).CreateAsync(autor, new CreateDocumentRequest
            {
                Name = "x", Category = "x", Library = DocumentLibrary.Community, AccessLevel = DocumentAccessLevel.Members,
                FileUrl = AvatarVictima
            }, canUploadCommunity: true, canManageAdmin: false));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Tarea_EnlaceQueNoEsWeb_SeRechaza()
    {
        var lider = await Usuario("l@ex.com");
        Guid tarea;
        await using (var ctx = _db.CreateContext())
        {
            var g = new Group { Name = "E", Type = GroupType.Team };
            g.Memberships.Add(new GroupMembership { UserId = lider, Role = GroupRole.Leader });
            var t = new KanbanTask { Group = g, Title = "x", CreatedBy = lider };
            ctx.AddRange(g, t);
            await ctx.SaveChangesAsync();
            tarea = t.Id;
        }
        await using var c2 = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            new KanbanService(c2).AddLinkAsync(tarea, lider, new AddTaskLinkRequest { Title = "x", Url = "javascript:alert(1)" }));
        Assert.Equal(400, ex.StatusCode);
    }

    public void Dispose() => _db.Dispose();
}
