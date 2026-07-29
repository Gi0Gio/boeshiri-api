using Boeshiri.Application.Common;
using Boeshiri.Application.Groups;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Groups;

/// <summary>Tablero Kanban (§7.4) con autorización contextual por grupo (ADR-0005).</summary>
public class KanbanService(BoeshiriDbContext db) : IKanbanService
{
    public async Task<IReadOnlyList<BoardTaskDto>> GetBoardAsync(Guid groupId, Guid userId, CancellationToken ct = default)
    {
        // Solo los integrantes del grupo ven su tablero.
        if (await RoleInGroupAsync(groupId, userId, ct) is null)
            throw AppException.Forbidden("No perteneces a este grupo.");

        return await db.KanbanTasks
            .Where(t => t.GroupId == groupId)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new BoardTaskDto(
                t.Id, t.Title, t.Description, t.Status,
                t.Assignees.Select(a => new TaskAssigneeDto(a.UserId, a.User.FullName)).ToList(),
                t.Links.Select(l => new TaskLinkDto(l.Title, l.Url)).ToList(),
                t.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<Guid> CreateTaskAsync(Guid groupId, Guid userId, CreateTaskRequest request, CancellationToken ct = default)
    {
        var role = await RoleInGroupAsync(groupId, userId, ct);
        if (!IsManager(role))
            throw AppException.Forbidden("Solo el líder o coordinador del grupo puede crear tareas.");

        var task = new KanbanTask
        {
            GroupId = groupId,
            Title = request.Title.Trim(),
            Description = request.Description,
            Status = KanbanStatus.Pending,
            CreatedBy = userId
        };

        foreach (var assigneeId in (request.AssigneeIds ?? []).Distinct())
            task.Assignees.Add(new KanbanTaskAssignee { UserId = assigneeId });

        db.KanbanTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return task.Id;
    }

    public async Task MoveTaskAsync(Guid taskId, KanbanStatus status, Guid userId, CancellationToken ct = default)
    {
        var task = await db.KanbanTasks
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw AppException.NotFound("La tarea no existe.");

        var role = await RoleInGroupAsync(task.GroupId, userId, ct);
        var isAssignee = task.Assignees.Any(a => a.UserId == userId);

        // El líder/coordinador mueve a cualquier columna; el responsable solo puede
        // marcar su tarea como lista (En revisión / Completado) — RF-KAN-02/03.
        var allowed = IsManager(role) ||
            (isAssignee && status is KanbanStatus.InReview or KanbanStatus.Done);

        if (!allowed)
            throw AppException.Forbidden("No tienes permiso para mover esta tarea.");

        task.Status = status;
        await db.SaveChangesAsync(ct);
    }

    public async Task AddLinkAsync(Guid taskId, Guid userId, AddTaskLinkRequest request, CancellationToken ct = default)
    {
        var task = await db.KanbanTasks
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw AppException.NotFound("La tarea no existe.");

        var role = await RoleInGroupAsync(task.GroupId, userId, ct);
        var isAssignee = task.Assignees.Any(a => a.UserId == userId);

        if (!IsManager(role) && !isAssignee)
            throw AppException.Forbidden("Solo el líder o un responsable puede añadir enlaces.");

        db.KanbanTaskLinks.Add(new KanbanTaskLink { TaskId = taskId, Title = request.Title, Url = request.Url });
        await db.SaveChangesAsync(ct);
    }

    private static bool IsManager(GroupRole? role) => role is GroupRole.Leader or GroupRole.Coordinator;

    private Task<GroupRole?> RoleInGroupAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMemberships
            .Where(m => m.GroupId == groupId && m.UserId == userId)
            .Select(m => (GroupRole?)m.Role)
            .FirstOrDefaultAsync(ct);
}
