using Boeshiri.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Avisos in-app del usuario autenticado (RF-PUB-16, RF-TRA-02). Cada quien ve
/// solo sus propias notificaciones.
/// </summary>
[ApiController]
[Authorize]
[Route("notificaciones")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

    /// <summary>Lista las notificaciones propias (más recientes primero).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> List(CancellationToken ct)
        => Ok(await notifications.ListAsync(CurrentUserId, ct));

    /// <summary>Cantidad de notificaciones sin leer (para el badge del panel).</summary>
    [HttpGet("no-leidas")]
    public async Task<ActionResult<object>> UnreadCount(CancellationToken ct)
        => Ok(new { count = await notifications.UnreadCountAsync(CurrentUserId, ct) });

    /// <summary>Marca todas las propias como leídas.</summary>
    [HttpPost("leer-todas")]
    public async Task<ActionResult> MarkAllRead(CancellationToken ct)
        => Ok(new { total = await notifications.MarkAllReadAsync(CurrentUserId, ct) });

    /// <summary>Marca una notificación propia como leída.</summary>
    [HttpPost("{id:guid}/leer")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(CurrentUserId, id, ct);
        return NoContent();
    }
}
