using Boeshiri.Application.Common;
using Boeshiri.Application.Groups;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Groups;
using Boeshiri.Infrastructure.Notifications;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Groups;

/// <summary>
/// Lo que faltaba para llevar un grupo: editar y borrar tareas, sacar o dejar el
/// grupo, sumar gente a un equipo, y que las personas se enteren.
/// </summary>
public class GestionGruposTests : IDisposable
{
    private readonly TestDb _db = new();

    private static KanbanService Kanban(BoeshiriDbContext c) => new(c, new NotificationService(c));
    private static GroupService Grupos(BoeshiriDbContext c) => new(c, new NotificationService(c), new AuditLogger(c));

    private async Task<Guid> Usuario(string email)
    {
        await using var c = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email, Status = MemberStatus.Active, EmailVerified = true };
        c.Users.Add(u);
        await c.SaveChangesAsync();
        return u.Id;
    }

    private async Task<Guid> Grupo(GroupType tipo = GroupType.Commission, Guid? madre = null, params (Guid id, GroupRole rol)[] gente)
    {
        await using var c = _db.CreateContext();
        var g = new Group { Name = "G" + Guid.NewGuid().ToString("N")[..4], Type = tipo, ParentCommissionId = madre };
        c.Groups.Add(g);
        foreach (var (id, rol) in gente)
            c.GroupMemberships.Add(new GroupMembership { GroupId = g.Id, UserId = id, Role = rol });
        await c.SaveChangesAsync();
        return g.Id;
    }

    private async Task<int> Avisos(Guid userId, string tipo)
    {
        await using var c = _db.CreateContext();
        return await c.Notifications.CountAsync(n => n.UserId == userId && n.Type == tipo);
    }

    [Fact]
    public async Task Tarea_SeEditaReasignaYAvisaSoloALosNuevos()
    {
        var coord = await Usuario("c@ex.com");
        var ana = await Usuario("ana@ex.com");
        var beto = await Usuario("beto@ex.com");
        var g = await Grupo(gente: [(coord, GroupRole.Coordinator), (ana, GroupRole.Member), (beto, GroupRole.Member)]);

        Guid tarea;
        await using (var c = _db.CreateContext())
            tarea = await Kanban(c).CreateTaskAsync(g, coord, new CreateTaskRequest { Title = "Afiche", AssigneeIds = [ana] });
        Assert.Equal(1, await Avisos(ana, "tarea.asignada"));

        await using (var c = _db.CreateContext())
            await Kanban(c).UpdateTaskAsync(tarea, coord, new UpdateTaskRequest { Title = "Afiche final", AssigneeIds = [ana, beto] });

        Assert.Equal(1, await Avisos(ana, "tarea.asignada"));
        Assert.Equal(1, await Avisos(beto, "tarea.asignada"));
        await using var check = _db.CreateContext();
        var t = await check.KanbanTasks.Include(x => x.Assignees).SingleAsync(x => x.Id == tarea);
        Assert.Equal("Afiche final", t.Title);
        Assert.Equal(2, t.Assignees.Count);
    }

    [Fact]
    public async Task Tarea_SoloElQueLleva_LaBorra()
    {
        var coord = await Usuario("c@ex.com");
        var ana = await Usuario("ana@ex.com");
        var g = await Grupo(gente: [(coord, GroupRole.Coordinator), (ana, GroupRole.Member)]);
        Guid tarea;
        await using (var c = _db.CreateContext())
            tarea = await Kanban(c).CreateTaskAsync(g, coord, new CreateTaskRequest { Title = "x", AssigneeIds = [ana] });

        await using (var c = _db.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<AppException>(() => Kanban(c).DeleteTaskAsync(tarea, ana));
            Assert.Equal(403, ex.StatusCode);
        }
        await using (var c = _db.CreateContext())
            await Kanban(c).DeleteTaskAsync(tarea, coord);
        await using var check = _db.CreateContext();
        Assert.False(await check.KanbanTasks.AnyAsync(t => t.Id == tarea));
    }

    [Fact]
    public async Task SalirDeLaComision_SacaTambienDeSusEquipos()
    {
        var coord = await Usuario("c@ex.com");
        var lider = await Usuario("l@ex.com");
        var ana = await Usuario("ana@ex.com");
        var com = await Grupo(gente: [(coord, GroupRole.Coordinator), (lider, GroupRole.Member), (ana, GroupRole.Member)]);
        var eq = await Grupo(GroupType.Team, com, (lider, GroupRole.Leader), (ana, GroupRole.Member));

        await using (var c = _db.CreateContext())
            await Grupos(c).RemoveMemberAsync(com, ana, ana, canManageGlobally: false);

        await using var check = _db.CreateContext();
        Assert.False(await check.GroupMemberships.AnyAsync(m => m.UserId == ana));
    }

    [Fact]
    public async Task ElCoordinador_NoSaleSinNombrarAOtro_YUnMiembroNoSacaANadie()
    {
        var coord = await Usuario("c@ex.com");
        var ana = await Usuario("ana@ex.com");
        var beto = await Usuario("beto@ex.com");
        var com = await Grupo(gente: [(coord, GroupRole.Coordinator), (ana, GroupRole.Member), (beto, GroupRole.Member)]);

        await using var c = _db.CreateContext();
        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(() => Grupos(c).RemoveMemberAsync(com, coord, coord, false))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(() => Grupos(c).RemoveMemberAsync(com, beto, ana, false))).StatusCode);

        await Grupos(c).RemoveMemberAsync(com, beto, coord, false);
        Assert.Equal(1, await Avisos(beto, "grupo.removido"));
    }

    [Fact]
    public async Task ElLider_SumaASuEquipoSoloGenteDeLaComision()
    {
        var coord = await Usuario("c@ex.com");
        var lider = await Usuario("l@ex.com");
        var ana = await Usuario("ana@ex.com");
        var ajeno = await Usuario("x@ex.com");
        var com = await Grupo(gente: [(coord, GroupRole.Coordinator), (lider, GroupRole.Member), (ana, GroupRole.Member)]);
        var eq = await Grupo(GroupType.Team, com, (lider, GroupRole.Leader));

        await using var c = _db.CreateContext();
        await Grupos(c).AddTeamMemberAsync(eq, ana, lider, false);
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(() => Grupos(c).AddTeamMemberAsync(eq, ajeno, lider, false))).StatusCode);
        Assert.True(await c.GroupMemberships.AnyAsync(m => m.GroupId == eq && m.UserId == ana));
        Assert.Equal(1, await Avisos(ana, "equipo.sumado"));
    }

    [Fact]
    public async Task PedirEntrar_AvisaAlCoordinador()
    {
        var coord = await Usuario("c@ex.com");
        var ana = await Usuario("ana@ex.com");
        var com = await Grupo(gente: [(coord, GroupRole.Coordinator)]);

        await using (var c = _db.CreateContext())
            await Grupos(c).RequestJoinAsync(com, ana);
        Assert.Equal(1, await Avisos(coord, "comision.solicitud_nueva"));
    }

    public void Dispose() => _db.Dispose();
}
