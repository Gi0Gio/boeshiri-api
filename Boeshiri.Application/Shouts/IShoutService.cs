namespace Boeshiri.Application.Shouts;

/// <summary>
/// Gritos: llamados abiertos entre miembros. Todo exige sesión salvo
/// <see cref="CountOpenAsync"/>, que solo devuelve un número. Publicar exige
/// <c>gritos.publicar</c>; eliminar ajenos, <c>gritos.moderar</c>. Apuntarse no
/// lleva permiso propio: tener sesión ya es la condición.
/// </summary>
public interface IShoutService
{
    /// <summary>
    /// Gritos vivos: abiertos y cuya fecha no pasó. Ordenados por lo que ocurre
    /// primero, no por lo más reciente — en un llamado lo que urge es la fecha.
    /// </summary>
    Task<IReadOnlyList<ShoutSummaryDto>> ListOpenAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Los propios en cualquier estado, para gestionarlos.</summary>
    Task<IReadOnlyList<ShoutSummaryDto>> ListMineAsync(Guid userId, CancellationToken ct = default);

    Task<ShoutDetailDto> GetDetailAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Cuántos gritos hay abiertos. Es lo único que se responde sin sesión.</summary>
    Task<int> CountOpenAsync(CancellationToken ct = default);

    /// <summary>Crea el grito y apunta a su autor: quien llama también va.</summary>
    Task<Guid> CreateAsync(Guid userId, CreateShoutRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, Guid userId, UpdateShoutRequest request, CancellationToken ct = default);

    /// <summary>Toma una plaza. Serializa por grito para que dos personas no se lleven el último cupo.</summary>
    Task JoinAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task LeaveAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Cerrar/cancelar (autor) o eliminar (autor o <c>gritos.moderar</c>).</summary>
    Task ChangeStatusAsync(Guid id, ShoutStatusAction action, Guid userId, bool canModerate, CancellationToken ct = default);
}
