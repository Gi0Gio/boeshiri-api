namespace Boeshiri.Domain.Entities;

/// <summary>Imagen de visibilidad pública de un evento (hasta 4, RF-EVT-01).</summary>
public class EventImage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public required string Url { get; set; }
    public int Order { get; set; }
}
