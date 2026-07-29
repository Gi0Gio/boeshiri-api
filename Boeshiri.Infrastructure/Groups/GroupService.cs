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
                g.Memberships.Where(m => m.Role == GroupRole.Coordinator).Select(m => m.User.FullName).FirstOrDefault()))
            .ToListAsync(ct);
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
