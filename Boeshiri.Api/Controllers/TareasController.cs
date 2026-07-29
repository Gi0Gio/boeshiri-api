using Boeshiri.Api.Authorization;
using Boeshiri.Application.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Tablero Kanban de un grupo (§7.4). Acceso contextual: solo integrantes ven el
/// tablero; el líder crea/mueve; el responsable actualiza su tarea (RF-KAN-02/03).
/// </summary>
[ApiController]
[Authorize]
public class TareasController(IKanbanService kanban) : ControllerBase
{
    /// <summary>Tablero del grupo (columnas: Pendiente/EnProceso/EnRevisión/Completado).</summary>
    [HttpGet("grupos/{groupId:guid}/tareas")]
    public async Task<ActionResult<IReadOnlyList<BoardTaskDto>>> Board(Guid groupId, CancellationToken ct)
        => Ok(await kanban.GetBoardAsync(groupId, User.GetUserId(), ct));

    /// <summary>Crea una tarea (líder/coordinador del grupo).</summary>
    [HttpPost("grupos/{groupId:guid}/tareas")]
    public async Task<IActionResult> Create(Guid groupId, CreateTaskRequest request, CancellationToken ct)
    {
        var id = await kanban.CreateTaskAsync(groupId, User.GetUserId(), request, ct);
        return Created($"/tareas/{id}", new { id });
    }

    /// <summary>Mueve una tarea de columna.</summary>
    [HttpPatch("tareas/{taskId:guid}/mover")]
    public async Task<IActionResult> Move(Guid taskId, MoveTaskRequest request, CancellationToken ct)
    {
        await kanban.MoveTaskAsync(taskId, request.Status, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Añade un enlace a la tarea (líder o responsable).</summary>
    [HttpPost("tareas/{taskId:guid}/enlaces")]
    public async Task<IActionResult> AddLink(Guid taskId, AddTaskLinkRequest request, CancellationToken ct)
    {
        await kanban.AddLinkAsync(taskId, User.GetUserId(), request, ct);
        return NoContent();
    }
}
