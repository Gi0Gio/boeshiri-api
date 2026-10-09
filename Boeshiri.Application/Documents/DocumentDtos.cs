using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Documents;

public record CreateDocumentRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Required, MaxLength(80)]
    public required string Category { get; init; }

    [Required]
    public required DocumentLibrary Library { get; init; }

    public DocumentAccessLevel AccessLevel { get; init; } = DocumentAccessLevel.Members;

    [Required, MaxLength(500)]
    public required string FileUrl { get; init; }

    [MaxLength(200)]
    public string? FileName { get; init; }

    [MaxLength(120)]
    public string? ContentType { get; init; }

    public long? SizeBytes { get; init; }

    /// <summary>Artículo del que proviene, si se sube como anexo (RF-DOC-06).</summary>
    public Guid? PublicationId { get; init; }
}

/// <summary>Reemplazo del archivo/metadata; sobrescribe (sin versiones, RF-DOC-01).</summary>
public record ReplaceDocumentRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Required, MaxLength(80)]
    public required string Category { get; init; }

    [Required, MaxLength(500)]
    public required string FileUrl { get; init; }

    [MaxLength(200)]
    public string? FileName { get; init; }

    [MaxLength(120)]
    public string? ContentType { get; init; }

    public long? SizeBytes { get; init; }
}

public record DocumentDto(
    Guid Id,
    string Name,
    string Category,
    DocumentLibrary Library,
    DocumentAccessLevel AccessLevel,
    // No sale en la respuesta: el bucket es público, y con la URL directa cualquiera
    // descarga el archivo saltándose el nivel de acceso. Se descarga por la API.
    [property: System.Text.Json.Serialization.JsonIgnore] string FileUrl,
    string? FileName,
    string? ContentType,
    long? SizeBytes,
    Guid AuthorId,
    string AuthorName,
    Guid? PublicationId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
