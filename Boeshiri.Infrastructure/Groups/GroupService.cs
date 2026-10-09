using Boeshiri.Infrastructure.Common;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Groups;
using Boeshiri.Application.Notifications;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Groups;

/// <summary>Comisiones, equipos y membresías (§7.1/7.2) con autorización contextual (ADR-0005).</summary>
public class GroupService(
    BoeshiriDbContext db,
    INotificationService notifications,
    IAuditLogger audit) : IGroupService
{
    public async Task<IReadOnlyList<CommissionDto>> ListCommissionsAsync(CancellationToken ct = default)
    {
        return await db.Groups
            .Where(g => g.Type == GroupType.Commission)
            .OrderBy(g => g.Name)
            .Select(g => new CommissionDto(
                g.Id,
                g.Name,
                g.Permanent,
                g.Memberships.Count,
                g.Memberships.Where(m => m.Role == GroupRole.Coordinator).Select(m => m.User.FullName).FirstOrDefault(),
                db.Groups
                    .Where(t => t.ParentCommissionId == g.Id && t.Type == GroupType.Team)
                    .OrderBy(t => t.Name)
                    .Select(t => new TeamDto(
                        t.Id, t.Name,
                        t.Memberships.Where(m => m.Role == GroupRole.Leader).Select(m => m.User.FullName).FirstOrDefault(),
                        t.Memberships.Count,
                        t.Memberships.Where(m => m.Role == GroupRole.Leader).Select(m => (Guid?)m.UserId).FirstOrDefault()))
                    .ToList()))
            .ToListAsync(ct);
    }

    public async Task<CommissionDetailDto> GetCommissionDetailAsync(Guid commissionId, CancellationToken ct = default)
    {
        var commission = await db.Groups
            .FirstOrDefaultAsync(g => g.Id == commissionId && g.Type == GroupType.Commission, ct)
            ?? throw AppException.NotFound("La comisión no existe.");

        var members = await db.GroupMemberships
            .Where(m => m.GroupId == commissionId)
            .OrderBy(m => m.Role).ThenBy(m => m.User.FullName)
            .Select(m => new GroupMemberDto(m.UserId, m.User.FullName, m.Role))
            .ToListAsync(ct);

        var teams = await db.Groups
            .Where(g => g.ParentCommissionId == commissionId && g.Type == GroupType.Team)
            .OrderBy(g => g.Name)
            .Select(g => new TeamDto(
                g.Id, g.Name,
                g.Memberships.Where(m => m.Role == GroupRole.Leader).Select(m => m.User.FullName).FirstOrDefault(),
                g.Memberships.Count,
                g.Memberships.Where(m => m.Role == GroupRole.Leader).Select(m => (Guid?)m.UserId).FirstOrDefault()))
            .ToListAsync(ct);

        return new CommissionDetailDto(commission.Id, commission.Name, commission.Permanent, members, teams);
    }

    public async Task<TeamDetailDto> GetTeamDetailAsync(Guid teamId, CancellationToken ct = default)
    {
        var team = await db.Groups
            .Where(g => g.Id == teamId && g.Type == GroupType.Team)
            .Select(g => new { g.Id, g.Name, CommissionId = g.ParentCommissionId!.Value, CommissionName = g.ParentCommission!.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw AppException.NotFound("El equipo no existe.");

        var members = await db.GroupMemberships
            .Where(m => m.GroupId == teamId)
            .OrderBy(m => m.Role).ThenBy(m => m.User.FullName)
            .Select(m => new GroupMemberDto(m.UserId, m.User.FullName, m.Role))
            .ToListAsync(ct);

        return new TeamDetailDto(team.Id, team.Name, team.CommissionId, team.CommissionName, members);
    }

    public async Task<Guid> CreateCommissionAsync(CreateCommissionRequest request, Guid userId, bool canManageGlobally, CancellationToken ct = default)
    {
        if (!canManageGlobally)
            throw AppException.Forbidden("Solo la Junta puede crear comisiones.");

        var name = request.Name.Trim();
        if (await db.Groups.AnyAsync(g => g.Type == GroupType.Commission && g.Name.ToLower() == name.ToLower(), ct))
            throw AppException.Conflict("Ya existe una comisión con ese nombre.");

        if (request.CoordinatorUserId is Guid c)
            await Personas.ExigirActivosAsync(db, [c], "Coordinador", ct);

        var commission = new Group { Name = name, Type = GroupType.Commission, Permanent = request.Permanent };
        if (request.CoordinatorUserId is Guid coord)
            commission.Memberships.Add(new GroupMembership { UserId = coord, Role = GroupRole.Coordinator });

        db.Groups.Add(commission);
        audit.Log(userId, "comision.creada", "Group", commission.Id.ToString(), name);
        await db.SaveChangesAsync(ct);
        return commission.Id;
    }

    public async Task AssignCoordinatorAsync(Guid commissionId, Guid coordinatorUserId, Guid userId, bool canManageGlobally, CancellationToken ct = default)
    {
        var isCommission = await db.Groups.AnyAsync(g => g.Id == commissionId && g.Type == GroupType.Commission, ct);
        if (!isCommission)
            throw AppException.NotFound("La comisión no existe.");

        await EnsureCanManageAsync(commissionId, userId, canManageGlobally, ct);
        await Personas.ExigirActivosAsync(db, [coordinatorUserId], "Coordinador", ct);

        var memberships = await db.GroupMemberships.Where(m => m.GroupId == commissionId).ToListAsync(ct);

        // Degradar coordinador(es) actual(es).
        foreach (var m in memberships.Where(m => m.Role == GroupRole.Coordinator))
            m.Role = GroupRole.Member;

        var target = memberships.FirstOrDefault(m => m.UserId == coordinatorUserId);
        if (target is null)
            db.GroupMemberships.Add(new GroupMembership { GroupId = commissionId, UserId = coordinatorUserId, Role = GroupRole.Coordinator });
        else
            target.Role = GroupRole.Coordinator;

        audit.Log(userId, "comision.coordinador_asignado", "Group", commissionId.ToString(), coordinatorUserId.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MyGroupDto>> ListMyGroupsAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.GroupMemberships
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Group.Name)
            .Select(m => new MyGroupDto(m.GroupId, m.Group.Name, m.Group.Type, m.Role, m.Group.ParentCommissionId))
            .ToListAsync(ct);
    }

    public async Task RequestJoinAsync(Guid commissionId, Guid userId, CancellationToken ct = default)
    {
        var isCommission = await db.Groups.AnyAsync(g => g.Id == commissionId && g.Type == GroupType.Commission, ct);
        if (!isCommission)
            throw AppException.NotFound("La comisión no existe.");

        if (await db.GroupMemberships.AnyAsync(m => m.GroupId == commissionId && m.UserId == userId, ct))
            throw AppException.Conflict("Ya perteneces a esta comisión.");

        if (await db.JoinRequests.AnyAsync(r => r.CommissionId == commissionId && r.UserId == userId && r.Status == JoinRequestStatus.Pending, ct))
            throw AppException.Conflict("Ya tienes una solicitud pendiente en esta comisión.");

        db.JoinRequests.Add(new JoinRequest { CommissionId = commissionId, UserId = userId });

        // Sin aviso, la solicitud esperaba a que el coordinador entrara a mirar.
        var quien = await db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstAsync(ct);
        var comision = await db.Groups.Where(g => g.Id == commissionId).Select(g => g.Name).FirstAsync(ct);
        var coordinadores = await db.GroupMemberships
            .Where(m => m.GroupId == commissionId && m.Role == GroupRole.Coordinator)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        foreach (var c in coordinadores)
            notifications.Notify(c, "comision.solicitud_nueva", $"{quien} pidió entrar a {comision}.");

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<JoinRequestDto>> ListJoinRequestsAsync(Guid commissionId, Guid userId, bool canManageGlobally, CancellationToken ct = default)
    {
        await EnsureCanManageAsync(commissionId, userId, canManageGlobally, ct);

        return await db.JoinRequests
            .Where(r => r.CommissionId == commissionId && r.Status == JoinRequestStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new JoinRequestDto(r.Id, r.UserId, r.User.FullName, r.User.Email, r.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task DecideJoinAsync(Guid requestId, JoinDecision decision, Guid deciderId, bool canManageGlobally, CancellationToken ct = default)
    {
        var request = await db.JoinRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw AppException.NotFound("La solicitud no existe.");

        if (request.Status != JoinRequestStatus.Pending)
            throw AppException.Conflict("La solicitud ya fue decidida.");

        await EnsureCanManageAsync(request.CommissionId, deciderId, canManageGlobally, ct);

        request.DecidedAt = DateTime.UtcNow;

        if (decision == JoinDecision.Accept)
        {
            request.Status = JoinRequestStatus.Accepted;
            if (!await db.GroupMemberships.AnyAsync(m => m.GroupId == request.CommissionId && m.UserId == request.UserId, ct))
                db.GroupMemberships.Add(new GroupMembership { GroupId = request.CommissionId, UserId = request.UserId, Role = GroupRole.Member });

            notifications.Notify(request.UserId, "comision.ingreso_aceptado", "Tu solicitud de ingreso a la comisión fue aceptada.");
            audit.Log(deciderId, "comision.ingreso_aceptado", "Group", request.CommissionId.ToString(), request.UserId.ToString());
        }
        else
        {
            request.Status = JoinRequestStatus.Rejected;
            notifications.Notify(request.UserId, "comision.ingreso_rechazado", "Tu solicitud de ingreso a la comisión no fue aceptada.");
            audit.Log(deciderId, "comision.ingreso_rechazado", "Group", request.CommissionId.ToString(), request.UserId.ToString());
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> CreateTeamAsync(Guid commissionId, CreateTeamRequest request, Guid userId, bool canManageGlobally, CancellationToken ct = default)
    {
        var isCommission = await db.Groups.AnyAsync(g => g.Id == commissionId && g.Type == GroupType.Commission, ct);
        if (!isCommission)
            throw AppException.NotFound("La comisión no existe.");

        await EnsureCanManageAsync(commissionId, userId, canManageGlobally, ct);

        // El líder sale de la comisión: un equipo es una parte de ella, no gente de fuera.
        var esIntegrante = await db.GroupMemberships
            .AnyAsync(m => m.GroupId == commissionId && m.UserId == request.LeaderUserId, ct);
        if (!esIntegrante)
            throw AppException.BadRequest("El líder del equipo tiene que ser integrante de la comisión.");
        await Personas.ExigirActivosAsync(db, [request.LeaderUserId], "Líder", ct);

        var team = new Group
        {
            Name = request.Name.Trim(),
            Type = GroupType.Team,
            Permanent = false,
            ParentCommissionId = commissionId
        };
        team.Memberships.Add(new GroupMembership { UserId = request.LeaderUserId, Role = GroupRole.Leader });

        db.Groups.Add(team);
        audit.Log(userId, "equipo.creado", "Group", team.Id.ToString(), request.Name);
        await db.SaveChangesAsync(ct);
        return team.Id;
    }

    public async Task RemoveMemberAsync(Guid groupId, Guid memberId, Guid userId, bool canManageGlobally, CancellationToken ct = default)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, ct)
            ?? throw AppException.NotFound("El grupo no existe.");
        var membership = await db.GroupMemberships.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberId, ct)
            ?? throw AppException.NotFound("Esa persona no pertenece al grupo.");

        var esUnoMismo = memberId == userId;
        if (!esUnoMismo)
            await EnsureCanManageGroupAsync(group, userId, canManageGlobally, ct);

        if (membership.Role is GroupRole.Coordinator or GroupRole.Leader)
            throw AppException.Conflict(esUnoMismo
                ? "Primero nombra a otra persona al frente del grupo; después puedes salir."
                : "Primero nombra a otra persona al frente del grupo.");

        db.GroupMemberships.Remove(membership);

        // Salir de una comisión es salir también de sus equipos (que no queden
        // integrantes de un equipo que ya no están en la comisión).
        if (group.Type == GroupType.Commission)
        {
            var enEquipos = await db.GroupMemberships
                .Where(m => m.UserId == memberId && m.Group.ParentCommissionId == groupId)
                .ToListAsync(ct);
            if (enEquipos.Any(m => m.Role == GroupRole.Leader))
                throw AppException.Conflict("Lidera un equipo de esta comisión: nombra antes a otro líder.");
            db.GroupMemberships.RemoveRange(enEquipos);
        }

        if (!esUnoMismo)
            notifications.Notify(memberId, "grupo.removido", $"Ya no formas parte de {group.Name}.");
        audit.Log(userId, esUnoMismo ? "grupo.salida" : "grupo.integrante_removido", "Group", groupId.ToString(), memberId.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task AddTeamMemberAsync(Guid teamId, Guid memberId, Guid userId, bool canManageGlobally, CancellationToken ct = default)
    {
        var team = await db.Groups.FirstOrDefaultAsync(g => g.Id == teamId && g.Type == GroupType.Team, ct)
            ?? throw AppException.NotFound("El equipo no existe.");

        await EnsureCanManageGroupAsync(team, userId, canManageGlobally, ct);

        if (await db.GroupMemberships.AnyAsync(m => m.GroupId == teamId && m.UserId == memberId, ct))
            throw AppException.Conflict("Esa persona ya está en el equipo.");

        var enComision = await db.GroupMemberships
            .AnyAsync(m => m.GroupId == team.ParentCommissionId && m.UserId == memberId, ct);
        if (!enComision)
            throw AppException.BadRequest("Solo se suman al equipo integrantes de su comisión.");

        db.GroupMemberships.Add(new GroupMembership { GroupId = teamId, UserId = memberId, Role = GroupRole.Member });
        notifications.Notify(memberId, "equipo.sumado", $"Te sumaron al equipo {team.Name}.");
        audit.Log(userId, "equipo.integrante_sumado", "Group", teamId.ToString(), memberId.ToString());
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gestión de un grupo cualquiera: en una comisión, su coordinador; en un equipo,
    /// su líder o quien gestiona la comisión madre. La Junta siempre.
    /// </summary>
    private async Task EnsureCanManageGroupAsync(Group group, Guid userId, bool canManageGlobally, CancellationToken ct)
    {
        if (canManageGlobally) return;
        if (group.Type == GroupType.Team)
        {
            var esLider = await db.GroupMemberships
                .AnyAsync(m => m.GroupId == group.Id && m.UserId == userId && m.Role == GroupRole.Leader, ct);
            if (esLider) return;
            await EnsureCanManageAsync(group.ParentCommissionId!.Value, userId, false, ct);
            return;
        }
        await EnsureCanManageAsync(group.Id, userId, false, ct);
    }

    /// <summary>Autoriza gestión de una comisión: coordinador contextual o permiso global.</summary>
    private async Task EnsureCanManageAsync(Guid commissionId, Guid userId, bool canManageGlobally, CancellationToken ct)
    {
        if (canManageGlobally)
            return;

        var isCoordinator = await db.GroupMemberships
            .AnyAsync(m => m.GroupId == commissionId && m.UserId == userId && m.Role == GroupRole.Coordinator, ct);

        if (!isCoordinator)
            throw AppException.Forbidden("Solo el coordinador de la comisión o la Junta pueden gestionarla.");
    }
}
