using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Documento de la biblioteca (§8). Sin control de versiones: al reemplazar se
/// sobrescribe (RF-DOC-01). El binario vive en el object storage; aquí va su URL
/// y su metadata (RF-DOC-02). Puede nacer directo o como anexo de un artículo
/// (RF-DOC-06).
/// </summary>
public class Document
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    /// <summary>Categoría de gestión (RF-DOC-02), texto libre.</summary>
    public required string Category { get; set; }

    public DocumentLibrary Library { get; set; }
    public DocumentAccessLevel AccessLevel { get; set; }

    // ── Archivo (en object storage) ──────────────────────────────
    public required string FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    /// <summary>Artículo del que proviene, si se subió como anexo (RF-DOC-06).</summary>
    public Guid? PublicationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
