using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Transparency;

public record CreateTransparencyRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [Required, MaxLength(20000)]
    public required string Body { get; init; }

    [Required, MaxLength(80)]
    public required string Category { get; init; }
}

public record UpdateTransparencyRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [Required, MaxLength(20000)]
    public required string Body { get; init; }

    [Required, MaxLength(80)]
    public required string Category { get; init; }
}

public enum TransparencyStatusAction
{
    Hide,
    Show,
    Delete
}

public record ChangeTransparencyStatusRequest
{
    [Required]
    public required TransparencyStatusAction Action { get; init; }
}

public record TransparencySummaryDto(
    Guid Id, string Title, string Category, ContentStatus Status, string AuthorName, DateTime CreatedAt);

public record TransparencyArticleDto(
    Guid Id, string Title, string Body, string Category, ContentStatus Status,
    Guid AuthorId, string AuthorName, DateTime CreatedAt, DateTime? UpdatedAt);
