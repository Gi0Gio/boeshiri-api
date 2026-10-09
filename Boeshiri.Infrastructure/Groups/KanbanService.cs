using Boeshiri.Infrastructure.Storage;
using Boeshiri.Application.Common;
using Boeshiri.Application.Groups;
using Boeshiri.Application.Notifications;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Groups;

/// <summary>Tablero Kanban (§7.4) con autorización contextual por grupo (ADR-0005).</summary>
public class KanbanService(BoeshiriDbContext db, INotificationService notifications) : IKanbanService
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

        var asignados = await ValidarAsignadosAsync(groupId, request.AssigneeIds, ct);

        var task = new KanbanTask
        {
            GroupId = groupId,
            Title = request.Title.Trim(),
            Description = request.Description,
            Status = KanbanStatus.Pending,
            CreatedBy = userId
        };

        foreach (var assigneeId in asignados)
            task.Assignees.Add(new KanbanTaskAssignee { UserId = assigneeId });
        AvisarAsignados(asignados, userId, task.Title);

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

        Enlaces.ExigirWeb(request.Url, "Enlace");
        db.KanbanTaskLinks.Add(new KanbanTaskLink { TaskId = taskId, Title = request.Title.Trim(), Url = request.Url.Trim() });
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateTaskAsync(Guid taskId, Guid userId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await db.KanbanTasks
            .Include(t => t.Assignees)
            .FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw AppException.NotFound("La tarea no existe.");

        if (!IsManager(await RoleInGroupAsync(task.GroupId, userId, ct)))
            throw AppException.Forbidden("Solo el líder o coordinador del grupo puede editar tareas.");

        var asignados = await ValidarAsignadosAsync(task.GroupId, request.AssigneeIds, ct);

        task.Title = request.Title.Trim();
        task.Description = request.Description;

        var nuevos = asignados.Where(id => task.Assignees.All(a => a.UserId != id)).ToList();
        foreach (var fuera in task.Assignees.Where(a => !asignados.Contains(a.UserId)).ToList())
            task.Assignees.Remove(fuera);
        foreach (var id in nuevos)
            task.Assignees.Add(new KanbanTaskAssignee { TaskId = task.Id, UserId = id });
        AvisarAsignados(nuevos, userId, task.Title);

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteTaskAsync(Guid taskId, Guid userId, CancellationToken ct = default)
    {
        var task = await db.KanbanTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw AppException.NotFound("La tarea no existe.");

        if (!IsManager(await RoleInGroupAsync(task.GroupId, userId, ct)))
            throw AppException.Forbidden("Solo el líder o coordinador del grupo puede borrar tareas.");

        db.KanbanTasks.Remove(task);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Los asignados tienen que ser integrantes del grupo.</summary>
    private async Task<List<Guid>> ValidarAsignadosAsync(Guid groupId, List<Guid>? ids, CancellationToken ct)
    {
        var asignados = (ids ?? []).Distinct().ToList();
        var integrantes = await db.GroupMemberships
            .Where(m => m.GroupId == groupId && asignados.Contains(m.UserId))
            .CountAsync(ct);
        if (integrantes != asignados.Count)
            throw AppException.BadRequest("Solo puedes asignar la tarea a integrantes del grupo.");
        return asignados;
    }

    /// <summary>Quien recibe una tarea se entera (salvo que se la asigne a sí mismo).</summary>
    private void AvisarAsignados(IEnumerable<Guid> ids, Guid autorId, string titulo)
    {
        foreach (var id in ids.Where(id => id != autorId))
            notifications.Notify(id, "tarea.asignada", $"Te asignaron la tarea «{titulo}».");
    }

    private static bool IsManager(GroupRole? role) => role is GroupRole.Leader or GroupRole.Coordinator;

    private Task<GroupRole?> RoleInGroupAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMemberships
            .Where(m => m.GroupId == groupId && m.UserId == userId)
            .Select(m => (GroupRole?)m.Role)
            .FirstOrDefaultAsync(ct);
}
