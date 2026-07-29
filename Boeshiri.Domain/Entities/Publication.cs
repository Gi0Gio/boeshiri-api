using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Publicación de un miembro (Requerimientos §6). Clase única para los 5 tipos;
/// los campos usados dependen de <see cref="Type"/>. La metadata de fechas es
/// visible al público (RF-MEM-13).
/// </summary>
public class Publication
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public PublicationType Type { get; set; }
    public required string Title { get; set; }

    /// <summary>Cuerpo del artículo/noticia.</summary>
    public string? Body { get; set; }

    /// <summary>Enlace externo para Video (YouTube) o Música (Spotify/YouTube/SoundCloud).</summary>
    public string? ExternalUrl { get; set; }

    public Visibility Visibility { get; set; } = Visibility.Public;
    public ContentStatus Status { get; set; } = ContentStatus.Published;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }

    public ICollection<PublicationImage> Images { get; set; } = new List<PublicationImage>();
    public ICollection<PublicationLink> Links { get; set; } = new List<PublicationLink>();
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
