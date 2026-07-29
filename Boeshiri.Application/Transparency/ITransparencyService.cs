namespace Boeshiri.Application.Transparency;

/// <summary>
/// Panel de transparencia (§10.7). Publicar/editar/ocultar/eliminar exige
/// <c>transparencia.gestionar</c> (RF-TRA-01). Al publicar se notifica a todos los
/// miembros en su panel (RF-TRA-02).
/// </summary>
public interface ITransparencyService
{
    /// <summary>Lista artículos. Con <paramref name="includeHidden"/> incluye los ocultos (gestión).</summary>
    Task<IReadOnlyList<TransparencySummaryDto>> ListAsync(bool includeHidden, CancellationToken ct = default);

    Task<TransparencyArticleDto> GetDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>Publica un artículo y notifica a todos los miembros activos (RF-TRA-02).</summary>
    Task<Guid> CreateAsync(Guid userId, CreateTransparencyRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, Guid userId, UpdateTransparencyRequest request, CancellationToken ct = default);

    Task ChangeStatusAsync(Guid id, TransparencyStatusAction action, Guid userId, CancellationToken ct = default);
}
