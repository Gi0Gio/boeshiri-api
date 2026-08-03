namespace Boeshiri.Application.Profiles;

/// <summary>
/// Perfil del miembro (RF-MEM-01..08) y perfiles públicos de la Comunidad
/// (RF-PUB-09). El perfil público respeta las opciones de privacidad del miembro.
/// </summary>
public interface IProfileService
{
    Task<MyProfileDto> GetMyProfileAsync(Guid userId, CancellationToken ct = default);

    Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default);

    Task UpdatePrivacyAsync(Guid userId, UpdatePrivacyRequest request, CancellationToken ct = default);

    /// <summary>Reemplaza las redes del perfil, con validaciones (RF-MEM-04/05).</summary>
    Task UpdateSocialLinksAsync(Guid userId, UpdateSocialLinksRequest request, CancellationToken ct = default);

    /// <summary>Lista los perfiles públicos de miembros activos (RF-PUB-09).</summary>
    Task<IReadOnlyList<CommunityMemberDto>> ListCommunityAsync(string? role = null, CancellationToken ct = default);

    /// <summary>Perfil público de un miembro, filtrado por su privacidad.</summary>
    Task<PublicProfileDto> GetPublicProfileAsync(Guid userId, CancellationToken ct = default);
}
