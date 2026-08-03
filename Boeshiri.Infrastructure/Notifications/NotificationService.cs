using Boeshiri.Application.Common;
using Boeshiri.Application.Notifications;
using Boeshiri.Domain.Entities;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Notifications;

/// <summary>Notificaciones in-app respaldadas por EF Core.</summary>
public class NotificationService(BoeshiriDbContext db) : INotificationService
{
    public void Notify(Guid userId, string type, string message)
    {
        db.Notifications.Add(new Notification { UserId = userId, Type = type, Message = message });
    }

    public async Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.Read, n.CreatedAt))
            .ToListAsync(ct);
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == userId && !n.Read, ct);

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        // Filtra por usuario además de por no-leídas: sin eso, una consulta mal
        // escrita podría marcar avisos ajenos.
        var pendientes = await db.Notifications
            .Where(n => n.UserId == userId && !n.Read)
            .ToListAsync(ct);

        foreach (var n in pendientes) n.Read = true;
        await db.SaveChangesAsync(ct);
        return pendientes.Count;
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default)
    {
        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, ct)
            ?? throw AppException.BadRequest("Notificación no encontrada.");

        if (!notification.Read)
        {
            notification.Read = true;
            await db.SaveChangesAsync(ct);
        }
    }
}
