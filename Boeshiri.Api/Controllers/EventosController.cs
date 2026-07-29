using Boeshiri.Api.Authorization;
using Boeshiri.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Eventos (§7.3). Lectura pública con visibilidad (RF-PUB-10/11); la gestión
/// (crear, editar, ocultar, asistencia) exige <c>eventos.gestionar</c> (RF-EVT-02).
/// </summary>
[ApiController]
[Route("eventos")]
public class EventosController(IEventService events) : ControllerBase
{
    /// <summary>Listado público: próximos o historial (RF-PUB-10).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<EventSummaryDto>>> List([FromQuery] EventWhen cuando = EventWhen.All, CancellationToken ct = default)
    {
        var authenticated = User.Identity?.IsAuthenticated ?? false;
        return Ok(await events.ListPublicAsync(cuando, includeMembersOnly: authenticated, ct));
    }

    /// <summary>Historial de eventos del usuario (RF-MEM-08).</summary>
    [Authorize]
    [HttpGet("mi-historial")]
    public async Task<ActionResult<IReadOnlyList<MyEventDto>>> MyHistory(CancellationToken ct)
        => Ok(await events.ListMyHistoryAsync(User.GetUserId(), ct));

    /// <summary>Detalle con reglas de visibilidad (RF-PUB-11/18/19/20).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<EventDetailDto>> Detail(Guid id, CancellationToken ct)
    {
        var authenticated = User.Identity?.IsAuthenticated ?? false;
        return Ok(await events.GetDetailAsync(id, authenticated, ct));
    }

    /// <summary>Crea un evento (RF-EVT-01).</summary>
    [HasPermission("eventos.gestionar")]
    [HttpPost]
    public async Task<ActionResult> Create(CreateEventRequest request, CancellationToken ct)
    {
        var id = await events.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Detail), new { id }, new { id });
    }

    /// <summary>Edita un evento.</summary>
    [HasPermission("eventos.gestionar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateEventRequest request, CancellationToken ct)
    {
        await events.UpdateAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>Oculta / muestra / elimina un evento (RF-EVT-02).</summary>
    [HasPermission("eventos.gestionar")]
    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeEventStatusRequest request, CancellationToken ct)
    {
        await events.ChangeStatusAsync(id, request.Action, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Registra la asistencia del evento (RF-EVT-03).</summary>
    [HasPermission("eventos.gestionar")]
    [HttpPost("{id:guid}/asistencia")]
    public async Task<IActionResult> RecordAttendance(Guid id, RecordAttendanceRequest request, CancellationToken ct)
    {
        await events.RecordAttendanceAsync(id, request, User.GetUserId(), ct);
        return NoContent();
    }
}
