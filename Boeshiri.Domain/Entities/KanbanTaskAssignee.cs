namespace Boeshiri.Domain.Entities;

/// <summary>Responsable de una tarea Kanban (M:N tarea–usuario).</summary>
public class KanbanTaskAssignee
{
    public Guid TaskId { get; set; }
    public KanbanTask Task { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
