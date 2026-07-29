namespace Boeshiri.Application.Groups;

/// <summary>
/// Tablero Kanban de un grupo (§7.4). Permisos CONTEXTUALES: solo los integrantes
/// ven el tablero; el líder/coordinador crea y mueve tareas (RF-KAN-02); el
/// responsable actualiza y marca lista su tarea (RF-KAN-03).
/// </summary>
public interface IKanbanService
{
    Task<IReadOnlyList<BoardTaskDto>> GetBoardAsync(Guid groupId, Guid userId, CancellationToken ct = default);

    Task<Guid> CreateTaskAsync(Guid groupId, Guid userId, CreateTaskRequest request, CancellationToken ct = default);

    /// <summary>Mueve una tarea de columna. El líder a cualquiera; el responsable a En revisión/Completado.</summary>
    Task MoveTaskAsync(Guid taskId, Boeshiri.Domain.Enums.KanbanStatus status, Guid userId, CancellationToken ct = default);

    /// <summary>Añade un enlace a la tarea (líder o responsable).</summary>
    Task AddLinkAsync(Guid taskId, Guid userId, AddTaskLinkRequest request, CancellationToken ct = default);
}
