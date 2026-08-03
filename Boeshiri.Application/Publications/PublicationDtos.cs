using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Publications;

/// <summary>Enlace de referencia de entrada (artículo).</summary>
public record LinkInput([Required, MaxLength(200)] string Title, [Required, MaxLength(500)] string Url);

/// <summary>Datos para crear una publicación (§6). Las imágenes son URLs ya subidas.</summary>
public record CreatePublicationRequest
{
    [Required]
    public required PublicationType Type { get; init; }

    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [MaxLength(20000)]
    public string? Body { get; init; }

    [MaxLength(500)]
    public string? ExternalUrl { get; init; }

    public Visibility Visibility { get; init; } = Visibility.Public;

    public List<string>? Images { get; init; }
    public List<LinkInput>? Links { get; init; }
    public List<string>? Tags { get; init; }
}

/// <summary>Datos editables de una publicación (RF-MEM-12). El tipo no cambia.</summary>
public record UpdatePublicationRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [MaxLength(20000)]
    public string? Body { get; init; }

    [MaxLength(500)]
    public string? ExternalUrl { get; init; }

    public Visibility Visibility { get; init; } = Visibility.Public;

    /// <summary>
    /// Lista completa de imágenes tras la edición: lo que no venga se elimina.
    /// Si es <c>null</c> las imágenes se dejan como están, para no romper a quien
    /// envíe el formulario sin este campo.
    /// </summary>
    public List<string>? Images { get; init; }

    public List<string>? Tags { get; init; }
}

/// <summary>Acción de cambio de estado de una publicación.</summary>
public enum StatusAction
{
    Hide,
    Show,
    Delete
}

public record ChangeStatusRequest
{
    [Required]
    public required StatusAction Action { get; init; }
}

/// <summary>Resumen de publicación para listados.</summary>
public record PublicationDto(
    Guid Id,
    PublicationType Type,
    string Title,
    Guid AuthorId,
    string AuthorName,
    Visibility Visibility,
    ContentStatus Status,
    DateTime CreatedAt,
    DateTime? EditedAt,
    string? CoverImage,
    IReadOnlyList<string> Tags);

/// <summary>Detalle completo de una publicación.</summary>
public record PublicationDetailDto(
    Guid Id,
    PublicationType Type,
    string Title,
    string? Body,
    string? ExternalUrl,
    Guid AuthorId,
    string AuthorName,
    Visibility Visibility,
    ContentStatus Status,
    DateTime CreatedAt,
    DateTime? EditedAt,
    IReadOnlyList<string> Images,
    IReadOnlyList<LinkInput> Links,
    IReadOnlyList<string> Tags);
