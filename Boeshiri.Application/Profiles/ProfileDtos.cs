using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Profiles;

// ── Vista y edición del propio perfil ────────────────────────────

public record ProfilePrivacyDto(bool ShowPhone, bool ShowEmail, bool ShowWhatsapp, bool ShowCommittees, bool ShowHistory);

public record SocialLinkDto(SocialNetworkType Type, string Value, bool Visible);

public record SkillDto(string Name, int Level);

public record SkillInput
{
    [Required, MaxLength(80)]
    public required string Name { get; init; }

    [Range(1, 8)]
    public int Level { get; init; } = 1;
}

/// <summary>Perfil propio (vista completa, sin filtrar por privacidad).</summary>
public record MyProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string? Bio,
    string? Intro,
    string? PhotoUrl,
    string? Discipline,
    string? Location,
    ProfilePrivacyDto Privacy,
    IReadOnlyList<string> Tags,
    IReadOnlyList<SkillDto> Skills,
    IReadOnlyList<SocialLinkDto> SocialLinks,
    bool MarketplaceActive);

public record UpdateProfileRequest
{
    [Required, MaxLength(160)]
    public required string FullName { get; init; }

    [MaxLength(2000)]
    public string? Bio { get; init; }

    [MaxLength(4000)]
    public string? Intro { get; init; }

    [MaxLength(120)]
    public string? Discipline { get; init; }

    /// <summary>Ubicación opcional; se muestra como pill si el miembro la rellena (RF-MEM-02).</summary>
    [MaxLength(120)]
    public string? Location { get; init; }

    [MaxLength(500)]
    public string? PhotoUrl { get; init; }

    /// <summary>Etiquetas sociales cosméticas (RF-MEM-01/RF-RBAC-05).</summary>
    public List<string>? Tags { get; init; }

    /// <summary>Habilidades con nivel (1–8) para el portafolio.</summary>
    public List<SkillInput>? Skills { get; init; }
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

public record CommunityMemberDto(Guid Id, string FullName, string? Discipline, string? PhotoUrl, IReadOnlyList<string> Tags, IReadOnlyList<string> Roles);

public record PublicSocialLinkDto(SocialNetworkType Type, string Value);
public record ProfileGalleryItemDto(Guid Id, PublicationType Type, string Title, string? CoverImage);
public record ProfileEventDto(Guid Id, string Title, DateTime Date);

/// <summary>Perfil público de un miembro, ya filtrado por sus opciones de privacidad.</summary>
public record PublicProfileDto(
    Guid Id,
    string FullName,
    string? Bio,
    string? Intro,
    string? PhotoUrl,
    string? Discipline,
    string? Location,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Tags,
    IReadOnlyList<SkillDto> Skills,
    string? Phone,
    string? Email,
    IReadOnlyList<PublicSocialLinkDto> SocialLinks,
    IReadOnlyList<string> Commissions,
    IReadOnlyList<ProfileEventDto> EventHistory,
    IReadOnlyList<ProfileGalleryItemDto> Gallery,
    /// <summary>Anuncios publicados en el marketplace. Vacío si no vende o no tiene ninguno.</summary>
    IReadOnlyList<ProfileListingDto> Marketplace);

/// <summary>Anuncio del miembro para mostrarlo en su perfil (§9).</summary>
public record ProfileListingDto(
    Guid Id,
    ListingKind Kind,
    string Name,
    string Category,
    decimal Price,
    decimal? PriceMax,
    string? CoverImage);
