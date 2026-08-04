using Boeshiri.Application.Common;
using Boeshiri.Application.Shouts;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Shouts;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Shouts;

public class ShoutServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private static ShoutService NewService(BoeshiriDbContext ctx) =>
        new(ctx, new NotificationService(ctx), new AuditLogger(ctx));

    private static CreateShoutRequest Req(int slots = 4, int enDias = 3, decimal? fee = null) => new()
    {
        Title = "¿Quién se apunta a la playa?",
        Place = "Las Lajas",
        HappensAt = DateTime.UtcNow.AddDays(enDias),
        Slots = slots,
        Fee = fee
    };

    // ── Crear ────────────────────────────────────────────────────

    /// <summary>
    /// Quien grita también va: si no se apuntara solo, «faltan N» contaría su
    /// propia plaza como libre y el número de la pieza roja mentiría desde el
    /// primer segundo.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ApuntaAlAutor()
    {
        var autor = await AddUserAsync("autor@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req(slots: 4));

        await using var check = _db.CreateContext();
        var resumen = (await NewService(check).ListOpenAsync(autor)).Single();
        Assert.Equal(id, resumen.Id);
        Assert.Equal(1, resumen.Taken);
        Assert.True(resumen.Joined);
        Assert.True(resumen.Mine);
    }

    [Fact]
    public async Task CreateAsync_FechaPasada_ThrowsBadRequest()
    {
        var autor = await AddUserAsync("autor@ex.com");

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(
            () => NewService(ctx).CreateAsync(autor, Req(enDias: -1)));
        Assert.Equal(400, ex.StatusCode);
    }

    /// <summary>El tope por miembro es lo que impide que una sola persona inunde la pared.</summary>
    [Fact]
    public async Task CreateAsync_CuartoGritoAbierto_ThrowsConflict()
    {
        var autor = await AddUserAsync("autor@ex.com");

        for (var i = 0; i < 3; i++)
        {
            await using var ctx = _db.CreateContext();
            await NewService(ctx).CreateAsync(autor, Req());
        }

        await using var cuarto = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(
            () => NewService(cuarto).CreateAsync(autor, Req()));
        Assert.Equal(409, ex.StatusCode);
    }

    // ── Apuntarse ────────────────────────────────────────────────

    [Fact]
    public async Task JoinAsync_TomaPlazaYAvisaAlAutor()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var otro = await AddUserAsync("otro@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req(slots: 3));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).JoinAsync(id, otro);

        await using var check = _db.CreateContext();
        Assert.Equal(2, await check.ShoutJoins.CountAsync(j => j.ShoutId == id));
        Assert.True(await check.Notifications.AnyAsync(n => n.UserId == autor && n.Type == "grito.apuntado"));
    }

    [Fact]
    public async Task JoinAsync_DosVeces_ThrowsConflict()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var otro = await AddUserAsync("otro@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req(slots: 4));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).JoinAsync(id, otro);

        await using var repite = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(repite).JoinAsync(id, otro));
        Assert.Equal(409, ex.StatusCode);
    }

    /// <summary>Con el autor dentro, un grito de 2 plazas se llena con un solo apuntado.</summary>
    [Fact]
    public async Task JoinAsync_SinCupos_ThrowsConflict()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var segundo = await AddUserAsync("segundo@ex.com");
        var tercero = await AddUserAsync("tercero@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req(slots: 2));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).JoinAsync(id, segundo);

        await using var lleno = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(lleno).JoinAsync(id, tercero));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task JoinAsync_GritoCancelado_ThrowsConflict()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var otro = await AddUserAsync("otro@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req());

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, ShoutStatusAction.Cancel, autor, canModerate: false);

        await using var ctxJoin = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctxJoin).JoinAsync(id, otro));
        Assert.Equal(409, ex.StatusCode);
    }

    // ── Salirse ──────────────────────────────────────────────────

    /// <summary>
    /// El autor no se «sale» de su propio plan: eso dejaría a los apuntados yendo a
    /// algo que ya no existe sin que nadie se lo diga. Lo que corresponde es cancelar.
    /// </summary>
    [Fact]
    public async Task LeaveAsync_ElAutor_ThrowsBadRequest()
    {
        var autor = await AddUserAsync("autor@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req());

        await using var ctxLeave = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctxLeave).LeaveAsync(id, autor));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task LeaveAsync_LiberaLaPlaza()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var otro = await AddUserAsync("otro@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req(slots: 2));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).JoinAsync(id, otro);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).LeaveAsync(id, otro);

        await using var check = _db.CreateContext();
        var resumen = (await NewService(check).ListOpenAsync(autor)).Single();
        Assert.Equal(1, resumen.Taken);
        Assert.Equal(ShoutState.Open, resumen.State);
    }

    // ── Estado calculado ─────────────────────────────────────────

    [Fact]
    public async Task ListOpenAsync_ExcluyeVencidosYCerrados()
    {
        var autor = await AddUserAsync("autor@ex.com");

        Guid vivo, cerrado;
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            vivo = await svc.CreateAsync(autor, Req());
            cerrado = await svc.CreateAsync(autor, Req());
        }

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(cerrado, ShoutStatusAction.Close, autor, canModerate: false);

        // Uno vencido: se fuerza la fecha al pasado sin tocar el estado guardado.
        await using (var ctx = _db.CreateContext())
        {
            var pasado = new Shout
            {
                AuthorId = autor,
                Title = "Ya pasó",
                Place = "David",
                HappensAt = DateTime.UtcNow.AddDays(-1),
                Slots = 4
            };
            ctx.Shouts.Add(pasado);
            await ctx.SaveChangesAsync();
        }

        await using var check = _db.CreateContext();
        var abiertos = await NewService(check).ListOpenAsync(autor);
        Assert.Equal(vivo, Assert.Single(abiertos).Id);
    }

    /// <summary>
    /// Un grito lleno sigue existiendo en la pared: cambia de estado, no desaparece.
    /// </summary>
    [Fact]
    public async Task Estado_LlenoSeCalculaSinGuardarse()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var otro = await AddUserAsync("otro@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req(slots: 2));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).JoinAsync(id, otro);

        await using var check = _db.CreateContext();
        var resumen = (await NewService(check).ListOpenAsync(autor)).Single();
        Assert.Equal(ShoutState.Full, resumen.State);
        // Lo guardado sigue siendo solo lo que decidió una persona.
        Assert.Equal(ShoutStatus.Open, (await check.Shouts.FindAsync(id))!.Status);
    }

    /// <summary>Cancelado gana a vencido: quien mira necesita saber que se canceló.</summary>
    [Fact]
    public async Task Estado_CanceladoGanaAVencido()
    {
        var autor = await AddUserAsync("autor@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req());

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, ShoutStatusAction.Cancel, autor, canModerate: false);

        await using (var ctx = _db.CreateContext())
        {
            var s = await ctx.Shouts.FindAsync(id);
            s!.HappensAt = DateTime.UtcNow.AddDays(-1);
            await ctx.SaveChangesAsync();
        }

        await using var check = _db.CreateContext();
        var detalle = await NewService(check).GetDetailAsync(id, autor);
        Assert.Equal(ShoutState.Cancelled, detalle.State);
    }

    // ── Editar y moderar ─────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_MenosCuposQueApuntados_ThrowsConflict()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var otro = await AddUserAsync("otro@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req(slots: 4));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).JoinAsync(id, otro);

        await using var ctxUpd = _db.CreateContext();
        var req = new UpdateShoutRequest
        {
            Title = "Otro título",
            Place = "Las Lajas",
            HappensAt = DateTime.UtcNow.AddDays(3),
            Slots = 1 // menos que los 2 que ya van (autor + otro)
        };
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctxUpd).UpdateAsync(id, autor, req));
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeStatusAsync_AjenoSinModerar_ThrowsForbidden()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var intruso = await AddUserAsync("intruso@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req());

        await using var ctxDel = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(
            () => NewService(ctxDel).ChangeStatusAsync(id, ShoutStatusAction.Delete, intruso, canModerate: false));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task ChangeStatusAsync_ModeradorElimina_QuedaAuditado()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var moderador = await AddUserAsync("junta@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req());

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, ShoutStatusAction.Delete, moderador, canModerate: true);

        await using var check = _db.CreateContext();
        Assert.True(await check.AuditEntries.AnyAsync(a => a.Action == "grito.moderado"));
        Assert.Equal(ShoutStatus.Deleted, (await check.Shouts.FindAsync(id))!.Status);
    }

    /// <summary>Cancelar avisa a los apuntados; cerrar no tiene por qué alarmar a nadie.</summary>
    [Fact]
    public async Task ChangeStatusAsync_Cancelar_AvisaALosApuntados()
    {
        var autor = await AddUserAsync("autor@ex.com");
        var otro = await AddUserAsync("otro@ex.com");

        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateAsync(autor, Req());

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).JoinAsync(id, otro);

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(id, ShoutStatusAction.Cancel, autor, canModerate: false);

        await using var check = _db.CreateContext();
        Assert.True(await check.Notifications.AnyAsync(n => n.UserId == otro && n.Type == "grito.cancelado"));
        Assert.False(await check.Notifications.AnyAsync(n => n.UserId == autor && n.Type == "grito.cancelado"));
    }

    // ── Anónimo ──────────────────────────────────────────────────

    /// <summary>Lo único que sale sin sesión es el número.</summary>
    [Fact]
    public async Task CountOpenAsync_SoloCuentaLosVivos()
    {
        var autor = await AddUserAsync("autor@ex.com");

        Guid cerrado;
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateAsync(autor, Req());
            cerrado = await svc.CreateAsync(autor, Req());
        }

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).ChangeStatusAsync(cerrado, ShoutStatusAction.Close, autor, canModerate: false);

        await using var check = _db.CreateContext();
        Assert.Equal(1, await NewService(check).CountOpenAsync());
    }

    // ── Helpers ──────────────────────────────────────────────────

    private async Task<Guid> AddUserAsync(string email)
    {
        await using var ctx = _db.CreateContext();
        var u = new User
        {
            Email = email,
            PasswordHash = "x",
            FullName = email,
            EmailVerified = true,
            Status = MemberStatus.Active
        };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    public void Dispose() => _db.Dispose();
}
