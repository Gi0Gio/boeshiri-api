using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Groups;

public record TaskAssigneeDto(Guid UserId, string Name);
public record TaskLinkDto(string Title, string Url);

/// <summary>Tarea del tablero (§7.4).</summary>
public record BoardTaskDto(
    Guid Id,
    string Title,
    string? Description,
    KanbanStatus Status,
    IReadOnlyList<TaskAssigneeDto> Assignees,
    IReadOnlyList<TaskLinkDto> Links,
    DateTime CreatedAt);

/// <summary>Datos para crear una tarea (la crea el líder, RF-KAN-02).</summary>
public record CreateTaskRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    public List<Guid>? AssigneeIds { get; init; }
}

/// <summary>Mover una tarea de columna (RF-KAN-02/03).</summary>
public record MoveTaskRequest
{
    [Required]
    public required KanbanStatus Status { get; init; }
}

public record AddTaskLinkRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [Required, MaxLength(500)]
    public required string Url { get; init; }
}
