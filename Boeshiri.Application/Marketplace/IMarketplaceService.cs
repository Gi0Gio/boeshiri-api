namespace Boeshiri.Application.Marketplace;

/// <summary>
/// Marketplace de productos de los miembros (§9). Sin transacciones: el contacto
/// se toma del perfil del vendedor (RF-MKT-02/04). Publicar exige estar dado de
/// alta (RF-MKT-03); moderar exige <c>productos.moderar</c> (RF-MKT-07/08).
/// </summary>
public interface IMarketplaceService
{
    /// <summary>Da de alta al miembro en el marketplace (RF-MKT-03).</summary>
    Task EnrollAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<ProductSummaryDto>> ListPublicAsync(string? name, string? category, CancellationToken ct = default);

    /// <summary>Detalle con los datos de contacto del vendedor (RF-MKT-02).</summary>
    Task<ProductDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ProductSummaryDto>> ListMineAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Publica un producto. Requiere estar dado de alta (RF-MKT-03/04).</summary>
    Task<Guid> CreateAsync(Guid userId, CreateProductRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, Guid userId, UpdateProductRequest request, CancellationToken ct = default);

    /// <summary>Ocultar/mostrar/vender/eliminar. Propio (RF-MKT-05) o moderación (RF-MKT-08).</summary>
    Task ChangeStatusAsync(Guid id, ProductStatusAction action, Guid userId, bool canModerate, CancellationToken ct = default);

    /// <summary>Genera enlace + imagen para compartir (RF-MKT-05).</summary>
    Task<ProductShareDto> GetShareAsync(Guid id, CancellationToken ct = default);
}
