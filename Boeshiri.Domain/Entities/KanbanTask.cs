using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Tarea de un tablero Kanban de un grupo (§7.4). El líder la crea/mueve
/// (RF-KAN-02); los responsables la actualizan y marcan lista (RF-KAN-03).
/// </summary>
public class KanbanTask
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public required string Title { get; set; }
    public string? Description { get; set; }
    public KanbanStatus Status { get; set; } = KanbanStatus.Pending;

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<KanbanTaskAssignee> Assignees { get; set; } = new List<KanbanTaskAssignee>();
    public ICollection<KanbanTaskLink> Links { get; set; } = new List<KanbanTaskLink>();
}
