namespace Boeshiri.Domain.Entities;

/// <summary>Enlace de referencia de un artículo (hasta 3), con título.</summary>
public class PublicationLink
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PublicationId { get; set; }
    public Publication Publication { get; set; } = null!;

    public required string Title { get; set; }
    public required string Url { get; set; }
}
