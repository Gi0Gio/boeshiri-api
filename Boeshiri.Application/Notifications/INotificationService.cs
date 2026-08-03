namespace Boeshiri.Application.Notifications;

/// <summary>Aviso in-app tal como lo consume el panel del miembro.</summary>
public record NotificationDto(Guid Id, string Type, string Message, bool Read, DateTime CreatedAt);

/// <summary>
/// Notificaciones in-app. <see cref="Notify"/> encola el aviso en la unidad de
/// trabajo actual (no guarda); el resto de operaciones se autogestionan.
/// </summary>
public interface INotificationService
{
    /// <summary>Encola un aviso para un usuario. No llama a SaveChanges (lo hace el caller).</summary>
    void Notify(Guid userId, string type, string message);

    Task<IReadOnlyList<NotificationDto>> ListAsync(Guid userId, CancellationToken ct = default);

    Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default);

    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default);

    /// <summary>Marca todas las propias como leídas. Devuelve cuántas cambiaron.</summary>
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}
