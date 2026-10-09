using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Marketplace;

/// <summary>
/// Un producto tal como viaja entre Boesh Irí y una web externa, en los dos
/// sentidos. Solo catálogo: nada de pagos ni pedidos.
/// </summary>
/// <param name="ExternalId">Id en la web de origen (para Inbound) o el nuestro (para Outbound).</param>
/// <param name="Available">False = agotado, oculto o eliminado allí: aquí se oculta.</param>
/// <param name="UpdatedAt">Cuándo cambió por última vez en su origen.</param>
public record CatalogItem(
    string ExternalId,
    ListingKind Kind,
    string Name,
    string Category,
    decimal Price,
    decimal? PriceMax,
    string? Description,
    IReadOnlyList<string> Images,
    string? Url,
    bool Available,
    DateTime UpdatedAt);

/// <summary>Una página de lo traído, con el cursor para pedir la siguiente.</summary>
public record CatalogPage(IReadOnlyList<CatalogItem> Items, string? NextCursor);

/// <summary>Lo que respondió la otra web al empujarle productos: su id para cada uno nuestro.</summary>
public record CatalogPushResult(IReadOnlyDictionary<string, string> RemoteIds, IReadOnlyList<string> Errors);

/// <summary>
/// Conector de una web externa (uno por proveedor: «json-feed», «shopify»…). Es la
/// costura para la sincronización bidireccional; todavía sin implementaciones.
///
/// Reglas acordadas para cuando se construya (ADR-0007):
/// - Se sincroniza en segundo plano o con «Sincronizar»; nunca en medio de una visita.
/// - Lo que desaparece del origen se oculta aquí, no se borra.
/// - La moderación de Boesh Irí siempre gana sobre lo que llegue.
/// - Las imágenes se copian al almacenamiento propio (WebP, límites de subida).
/// - Solo se publica fuera lo que el miembro permite (Product.AllowExternalListing).
/// - Cada conector habla solo con su BaseUrl, con tiempo límite y reintentos.
/// </summary>
public interface ICatalogConnector
{
    /// <summary>Clave que guarda CatalogConnection.Provider.</summary>
    string Provider { get; }

    /// <summary>Traer (Inbound): productos cambiados desde el cursor.</summary>
    Task<CatalogPage> PullAsync(CatalogConnection connection, string? cursor, CancellationToken ct = default);

    /// <summary>Empujar (Outbound) a webs que no leen nuestro feed y necesitan que se les envíe.</summary>
    Task<CatalogPushResult> PushAsync(CatalogConnection connection, IReadOnlyList<CatalogItem> items, CancellationToken ct = default);
}
