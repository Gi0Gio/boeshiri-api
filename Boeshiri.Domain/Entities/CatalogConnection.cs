using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Una web externa con la que el marketplace comparte catálogo (p. ej. la web de
/// catálogo del colectivo). Puede ir en los dos sentidos: Boesh Irí trae sus
/// productos (Inbound) y esa web toma los de Boesh Irí (Outbound).
///
/// El marketplace es un CATÁLOGO, no una pasarela de pago: aquí solo viajan
/// productos y su disponibilidad; la compra siempre ocurre fuera (contacto con el
/// vendedor o la tienda del producto).
///
/// Modelo preparado; la sincronización (conectores, tareas programadas, endpoint
/// del feed) está por construir. Ver ICatalogConnector y ADR-0007 en el vault.
/// </summary>
public class CatalogConnection
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Cómo se llama para la Junta: «Catálogo web de Boesh Irí».</summary>
    public required string Name { get; set; }

    /// <summary>Qué conector la atiende (clave de ICatalogConnector.Provider): «json-feed», «shopify»…</summary>
    public required string Provider { get; set; }

    public CatalogSyncDirection Direction { get; set; } = CatalogSyncDirection.Both;

    /// <summary>Dirección base de su API (para traer o empujar).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// NOMBRE de la configuración que guarda la credencial para hablar con ellos
    /// (p. ej. «Catalogos:WebCatalogo:ApiKey» en las variables de Railway). Nunca el
    /// secreto: la base de datos no guarda claves de terceros.
    /// </summary>
    public string? CredentialKey { get; set; }

    /// <summary>
    /// Hash de la clave con la que ESA web lee nuestro catálogo (Outbound). La clave
    /// se muestra una sola vez al crearla; aquí solo queda su hash, como una contraseña.
    /// </summary>
    public string? AccessKeyHash { get; set; }

    /// <summary>
    /// A nombre de quién quedan los productos que se traen (Inbound): todo producto
    /// tiene vendedor. Un miembro, o la cuenta que represente a un aliado.
    /// </summary>
    public Guid? DefaultSellerId { get; set; }
    public User? DefaultSeller { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Cada cuánto sincronizar. Null = solo a mano.</summary>
    public int? SyncIntervalMinutes { get; set; }

    public DateTime? LastSyncAt { get; set; }
    /// <summary>«ok», «parcial» o «error», y el detalle en LastSyncError.</summary>
    public string? LastSyncStatus { get; set; }
    public string? LastSyncError { get; set; }

    /// <summary>Hasta dónde se leyó la otra web (su cursor o fecha), para traer solo lo nuevo.</summary>
    public string? InboundCursor { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ProductExternalLink> Links { get; set; } = new List<ProductExternalLink>();
}

/// <summary>
/// Un producto y su gemelo en una web externa. Con Imported, el original está allí
/// (lo que llegue de allí manda en nombre, precio y fotos, salvo la moderación de
/// Boesh Irí, que siempre gana). Con Published, el original está aquí.
/// </summary>
public class ProductExternalLink
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public Guid ConnectionId { get; set; }
    public CatalogConnection Connection { get; set; } = null!;

    public ExternalLinkRole Role { get; set; }

    /// <summary>Id del producto en la otra web.</summary>
    public required string ExternalId { get; set; }

    /// <summary>Su página allí («Ver en su tienda»).</summary>
    public string? ExternalUrl { get; set; }

    /// <summary>Huella de la versión remota (etag, hash o fecha) para saber si cambió sin compararlo todo.</summary>
    public string? RemoteVersion { get; set; }

    public DateTime? LastSyncedAt { get; set; }
}
