namespace Boeshiri.Domain.Entities;

/// <summary>
/// Integrante que participó en un evento. Alimenta el historial de eventos de su
/// perfil (RF-EVT-03 / RF-MEM-08).
/// </summary>
public class EventAttendee
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
