using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Artículo oficial de transparencia de la Junta (§10.7, RF-TRA-01). Al publicarse
/// notifica a todos los miembros en su panel (RF-TRA-02).
/// </summary>
public class TransparencyArticle
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Title { get; set; }
    public required string Body { get; set; }
    public required string Category { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Published;

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
