using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Evento del colectivo (§7.3, RF-EVT-01). Es un proyecto con lugar, costo,
/// imágenes públicas y responsable. La asistencia (RF-EVT-03) alimenta el
/// historial de perfil de los integrantes que participaron.
/// </summary>
public class Event
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Category { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// En planeación: se anuncia como «Próximamente» aunque falten la fecha o el
    /// costo. Para confirmarlo hacen falta los dos.
    /// </summary>
    public bool Planning { get; set; }

    /// <summary>Inicio (UTC). Null = «fecha por confirmar», solo en planeación.</summary>
    public DateTime? Date { get; set; }

    /// <summary>Fin (UTC), opcional: ARCANA va de 1:00 a 4:00 p. m.</summary>
    public DateTime? EndsAt { get; set; }

    public string? Location { get; set; }

    /// <summary>Costo de entrada: 0 = gratis; null = «por definir», solo en planeación.</summary>
    public decimal? Cost { get; set; }

    public Visibility Visibility { get; set; } = Visibility.Public;
    public ContentStatus Status { get; set; } = ContentStatus.Published;

    /// <summary>Responsable del evento (RF-EVT-01).</summary>
    public Guid? ResponsibleId { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Conteo total de asistentes registrado (RF-EVT-03).</summary>
    public int AttendanceCount { get; set; }

    public ICollection<EventImage> Images { get; set; } = new List<EventImage>();
    public ICollection<EventAttendee> Attendees { get; set; } = new List<EventAttendee>();
}
