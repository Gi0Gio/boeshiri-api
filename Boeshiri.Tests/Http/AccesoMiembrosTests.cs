using System.Net;
using System.Net.Http.Json;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Tests.Support;

namespace Boeshiri.Tests.Http;

/// <summary>
/// Quién llega a lo exclusivo de miembros. Un postulante (aunque verificó su correo
/// y puede iniciar sesión para ver el estado de su solicitud) no es miembro: no ve
/// contenido exclusivo, ni gritos, ni comisiones, ni sube archivos. Tampoco quien
/// tiene la membresía en pausa (Inactivo) puede actuar con los permisos de su rol.
/// </summary>
public class AccesoMiembrosTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;
    public AccesoMiembrosTests(ApiFactory api) => _api = api;

    private async Task<(Guid PubExclusiva, Guid Evento, Guid Grito, Guid Articulo)> SembrarAsync()
    {
        var autor = await _api.CrearUsuarioAsync($"autor{Guid.NewGuid():n}@ex.com", MemberStatus.Active, "Miembro");
        Guid pub = default, ev = default, grito = default, art = default;
        await _api.ConDbAsync(async db =>
        {
            var p = new Publication { AuthorId = autor, Type = PublicationType.Article, Title = "Solo miembros", Body = "x", Visibility = Visibility.Members };
            var e = new Event { Category = "Taller", Title = "Exclusivo", Date = DateTime.UtcNow.AddDays(5), Visibility = Visibility.Members, CreatedBy = autor };
            var s = new Shout { AuthorId = autor, Title = "Plan", Place = "Dolega", HappensAt = DateTime.UtcNow.AddDays(3), Slots = 5 };
            var a = new TransparencyArticle { Title = "Acta", Body = "x", Category = "Actas", AuthorId = autor };
            db.AddRange(p, e, s, a);
            await db.SaveChangesAsync();
            (pub, ev, grito, art) = (p.Id, e.Id, s.Id, a.Id);
        });
        return (pub, ev, grito, art);
    }

    public static TheoryData<MemberStatus, bool> NoMiembros => new()
    {
        { MemberStatus.Applicant, false },   // postulante en revisión
        { MemberStatus.Applicant, true },    // postulante rechazado
        { MemberStatus.Inactive, false },    // membresía en pausa
    };

    [Theory]
    [MemberData(nameof(NoMiembros))]
    public async Task QuienNoEsMiembroActivo_NoLlegaALoExclusivo(MemberStatus status, bool rechazado)
    {
        var (pub, ev, grito, art) = await SembrarAsync();
        var roles = status == MemberStatus.Inactive ? new[] { "Miembro" } : [];
        var id = await _api.CrearUsuarioAsync($"n{Guid.NewGuid():n}@ex.com", status, roles);
        if (rechazado)
            await _api.ConDbAsync(async db => { (await db.Users.FindAsync(id))!.RejectedAt = DateTime.UtcNow; await db.SaveChangesAsync(); });
        var c = await _api.ClienteDeAsync(id);

        var lista = await c.GetFromJsonAsync<List<Dictionary<string, object>>>("/publicaciones");
        Assert.DoesNotContain(lista!, p => p["id"].ToString() == pub.ToString());

        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/publicaciones/{pub}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync($"/eventos/{ev}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/gritos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync($"/gritos/{grito}/apuntarme", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"/transparencia/{art}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/grupos/comisiones")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsync("/archivos", new MultipartFormDataContent())).StatusCode);

        // Sí puede ver el estado de su solicitud y sus avisos.
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/auth/yo")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/notificaciones")).StatusCode);
    }

    [Fact]
    public async Task MiembroInactivo_NoActuaConLosPermisosDeSuRol()
    {
        var id = await _api.CrearUsuarioAsync($"i{Guid.NewGuid():n}@ex.com", MemberStatus.Inactive, "Miembro");
        var c = await _api.ClienteDeAsync(id);

        var r = await c.PostAsJsonAsync("/publicaciones", new { type = "Article", title = "Hola", body = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task MiembroActivo_VeLoExclusivo()
    {
        var (pub, ev, _, art) = await SembrarAsync();
        var id = await _api.CrearUsuarioAsync($"m{Guid.NewGuid():n}@ex.com", MemberStatus.Active, "Miembro");
        var c = await _api.ClienteDeAsync(id);

        var lista = await c.GetFromJsonAsync<List<Dictionary<string, object>>>("/publicaciones");
        Assert.Contains(lista!, p => p["id"].ToString() == pub.ToString());
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/publicaciones/{pub}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/eventos/{ev}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/gritos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"/transparencia/{art}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/grupos/comisiones")).StatusCode);
    }

    [Fact]
    public async Task Anonimo_SigueViendoLoPublico()
    {
        var c = _api.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/publicaciones")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/eventos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/gritos/resumen")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/gritos")).StatusCode);
    }
}
