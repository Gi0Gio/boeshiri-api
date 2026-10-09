using Boeshiri.Application.Admin;
using Boeshiri.Application.Common;
using Boeshiri.Application.Events;
using Boeshiri.Application.Groups;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Admin;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Events;
using Boeshiri.Infrastructure.Groups;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Persistence.Seed;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boeshiri.Tests.Admin;

/// <summary>
/// Quién puede tocar a quién (el Super Administrador está por encima de la Junta) y
/// que los ids de personas que llegan en una petición sean miembros reales: antes un
/// id inventado acababa en 500 y uno ajeno se aceptaba sin más.
/// </summary>
public class JerarquiaYValidacionTests : IDisposable
{
    private readonly TestDb _db = new();

    public JerarquiaYValidacionTests()
    {
        using var ctx = _db.CreateContext();
        DatabaseSeeder.SeedAsync(ctx).GetAwaiter().GetResult();
    }

    private MemberService Miembros(BoeshiriDbContext c) =>
        new(c, new NotificationService(c), new AuditLogger(c), NullLogger<MemberService>.Instance);
    private static RoleService Roles(BoeshiriDbContext c) => new(c, new AuditLogger(c));

    private async Task<Guid> Usuario(string email, MemberStatus status = MemberStatus.Active, string? rol = null)
    {
        await using var c = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email, Status = status, EmailVerified = true };
        c.Users.Add(u);
        if (rol is not null)
            c.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = (await c.Roles.SingleAsync(r => r.Name == rol)).Id });
        await c.SaveChangesAsync();
        return u.Id;
    }

    private async Task<Guid> RolId(string nombre)
    {
        await using var c = _db.CreateContext();
        return (await c.Roles.SingleAsync(r => r.Name == nombre)).Id;
    }

    [Fact]
    public async Task Junta_NoPuedeSuspenderAlSuperAdministrador()
    {
        var super = await Usuario("s@ex.com", rol: "Super Administrador");
        var junta = await Usuario("j@ex.com", rol: "Junta Directiva");

        await using var c = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Miembros(c).ChangeStatusAsync(super, new ChangeMemberStatusRequest { Status = MemberStatus.Suspended, Motivo = "x" }, junta));
        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Suspender_CierraSusSesiones()
    {
        var m = await Usuario("m@ex.com");
        var junta = await Usuario("j@ex.com", rol: "Junta Directiva");
        await using (var c = _db.CreateContext())
        {
            c.RefreshTokens.Add(new RefreshToken { UserId = m, TokenHash = new string('a', 64), ExpiresAt = DateTime.UtcNow.AddDays(30) });
            await c.SaveChangesAsync();
        }

        await using (var c = _db.CreateContext())
            await Miembros(c).ChangeStatusAsync(m, new ChangeMemberStatusRequest { Status = MemberStatus.Suspended, Motivo = "x" }, junta);

        await using var check = _db.CreateContext();
        Assert.All(await check.RefreshTokens.Where(t => t.UserId == m).ToListAsync(), t => Assert.NotNull(t.RevokedAt));
    }

    [Fact]
    public async Task SoloElSuperAdministrador_AsignaElComodin()
    {
        var junta = await Usuario("j@ex.com", rol: "Junta Directiva");
        var super = await Usuario("s@ex.com", rol: "Super Administrador");
        var otro = await Usuario("o@ex.com");
        var rolSuper = await RolId("Super Administrador");

        await using (var c = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<AppException>(() => Roles(c).AssignRoleAsync(junta, rolSuper, junta));
            Assert.Equal(403, ex.StatusCode);
        }
        await using (var c = _db.CreateContext())
            await Roles(c).AssignRoleAsync(otro, rolSuper, super);
    }

    [Fact]
    public async Task UnPostulante_NoRecibeRoles()
    {
        var super = await Usuario("s@ex.com", rol: "Super Administrador");
        var postulante = await Usuario("p@ex.com", MemberStatus.Applicant);

        var rol = await RolId("Junta Directiva");
        await using var c = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => Roles(c).AssignRoleAsync(postulante, rol, super));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Tarea_SoloSeAsignaAIntegrantesDelGrupo()
    {
        var coord = await Usuario("c@ex.com");
        var ajeno = await Usuario("a@ex.com");
        Guid grupo;
        await using (var c = _db.CreateContext())
        {
            var g = new Group { Name = "Música", Type = GroupType.Commission };
            c.Groups.Add(g);
            c.GroupMemberships.Add(new GroupMembership { GroupId = g.Id, UserId = coord, Role = GroupRole.Coordinator });
            await c.SaveChangesAsync();
            grupo = g.Id;
        }

        await using var c2 = _db.CreateContext();
        var svc = new KanbanService(c2, new NotificationService(c2));
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            svc.CreateTaskAsync(grupo, coord, new CreateTaskRequest { Title = "x", AssigneeIds = [ajeno] }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Evento_ResponsableInexistente_400YFechaSinZonaSeGuardaEnUtc()
    {
        var autor = await Usuario("e@ex.com");
        await using var c = _db.CreateContext();
        var svc = new EventService(c, new NotificationService(c), new AuditLogger(c), new FakeFileStorage());
        var ex = await Assert.ThrowsAsync<AppException>(() => svc.CreateAsync(autor, new CreateEventRequest
        {
            Category = "Arte", Title = "x", Date = DateTime.UtcNow.AddDays(3), Visibility = Visibility.Public, ResponsibleId = Guid.NewGuid()
        }));
        Assert.Equal(400, ex.StatusCode);

        var sinZona = DateTime.SpecifyKind(new DateTime(2026, 12, 1, 18, 0, 0), DateTimeKind.Unspecified);
        var id = await svc.CreateAsync(autor, new CreateEventRequest { Category = "Arte", Title = "y", Date = sinZona, Cost = 0, Visibility = Visibility.Public });
        var ev = await c.Events.SingleAsync(e => e.Id == id);
        Assert.Equal(DateTimeKind.Utc, ev.Date!.Value.Kind);
    }

    public void Dispose() => _db.Dispose();
}
