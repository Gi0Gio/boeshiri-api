using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Publications;

/// <summary>
/// Publicaciones (§6). El autor crea sin aprobación previa (RF-MEM-14) y gestiona
/// las propias (RF-MEM-12); la Junta/Super pueden moderar (RF-ADM-07). La
/// visibilidad público/exclusivo se aplica en la lectura (RF-PUB-18/19/20).
/// </summary>
public interface IPublicationService
{
    /// <summary>Crea una publicación. Noticia exige el permiso noticias.publicar (via <paramref name="canPublishNews"/>).</summary>
    Task<Guid> CreateAsync(Guid authorId, CreatePublicationRequest request, bool canPublishNews, CancellationToken ct = default);

    /// <summary>Publicaciones públicas y publicadas. Si <paramref name="includeMembersOnly"/>, incluye las exclusivas.</summary>
    Task<IReadOnlyList<PublicationDto>> ListPublicAsync(PublicationType? type, bool includeMembersOnly, CancellationToken ct = default);

    /// <summary>Detalle respetando visibilidad y estado (404 genérico si no accesible).</summary>
    Task<PublicationDetailDto> GetDetailAsync(Guid id, bool authenticated, CancellationToken ct = default);

    /// <summary>Publicaciones propias del autor (todas menos las eliminadas).</summary>
    Task<IReadOnlyList<PublicationDto>> ListMineAsync(Guid authorId, CancellationToken ct = default);

    /// <summary>Edita una publicación propia (actualiza la fecha de edición).</summary>
    Task UpdateAsync(Guid id, Guid authorId, UpdatePublicationRequest request, CancellationToken ct = default);

    /// <summary>Oculta/muestra/elimina. El autor puede todo sobre lo propio; con
    /// <paramref name="canModerate"/> se puede ocultar/eliminar contenido ajeno.</summary>
    Task ChangeStatusAsync(Guid id, StatusAction action, Guid userId, bool canModerate, CancellationToken ct = default);
}
