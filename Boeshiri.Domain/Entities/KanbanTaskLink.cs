namespace Boeshiri.Domain.Entities;

/// <summary>Enlace adjunto a una tarea Kanban (RF-KAN-03).</summary>
public class KanbanTaskLink
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TaskId { get; set; }
    public KanbanTask Task { get; set; } = null!;

    public required string Title { get; set; }
    public required string Url { get; set; }
}
