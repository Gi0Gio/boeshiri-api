using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Profiles;

// ── Vista y edición del propio perfil ────────────────────────────

public record ProfilePrivacyDto(bool ShowPhone, bool ShowEmail, bool ShowWhatsapp, bool ShowCommittees, bool ShowHistory);

public record SocialLinkDto(SocialNetworkType Type, string Value, bool Visible);

/// <summary>Perfil propio (vista completa, sin filtrar por privacidad).</summary>
public record MyProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string? Bio,
    string? PhotoUrl,
    string? Discipline,
    string? Location,
    ProfilePrivacyDto Privacy,
    IReadOnlyList<string> Tags,
    IReadOnlyList<SocialLinkDto> SocialLinks);

public record UpdateProfileRequest
{
    [Required, MaxLength(160)]
    public required string FullName { get; init; }

    [MaxLength(2000)]
    public string? Bio { get; init; }

    [MaxLength(120)]
    public string? Discipline { get; init; }

    [MaxLength(500)]
    public string? PhotoUrl { get; init; }

    /// <summary>Etiquetas sociales cosméticas (RF-MEM-01/RF-RBAC-05).</summary>
    public List<string>? Tags { get; init; }
}

public record UpdatePrivacyRequest
{
    public bool ShowPhone { get; init; }
    public bool ShowEmail { get; init; }
    public bool ShowWhatsapp { get; init; }
    public bool ShowCommittees { get; init; } = true;
    public bool ShowHistory { get; init; } = true;
}

public record SocialLinkInput
{
    [Required]
    public required SocialNetworkType Type { get; init; }

    [Required, MaxLength(320)]
    public required string Value { get; init; }

    public bool Visible { get; init; } = true;
}

public record UpdateSocialLinksRequest
{
    public List<SocialLinkInput> Links { get; init; } = [];
}

// ── Comunidad (perfiles públicos, RF-PUB-09) ─────────────────────

public record CommunityMemberDto(Guid Id, string FullName, string? Discipline, string? PhotoUrl, IReadOnlyList<string> Tags);

public record PublicSocialLinkDto(SocialNetworkType Type, string Value);
public record ProfileGalleryItemDto(Guid Id, PublicationType Type, string Title, string? CoverImage);
public record ProfileEventDto(Guid Id, string Title, DateTime Date);

/// <summary>Perfil público de un miembro, ya filtrado por sus opciones de privacidad.</summary>
public record PublicProfileDto(
    Guid Id,
    string FullName,
    string? Bio,
    string? PhotoUrl,
    string? Discipline,
    string? Location,
    IReadOnlyList<string> Tags,
    string? Phone,
    string? Email,
    IReadOnlyList<PublicSocialLinkDto> SocialLinks,
    IReadOnlyList<string> Commissions,
    IReadOnlyList<ProfileEventDto> EventHistory,
    IReadOnlyList<ProfileGalleryItemDto> Gallery);
