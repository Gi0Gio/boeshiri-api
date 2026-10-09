using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Producto del marketplace publicado por un miembro (§9). No hay transacciones:
/// el contacto para la venta se toma del perfil del vendedor (RF-MKT-02/04).
/// </summary>
public class Product
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SellerId { get; set; }
    public User Seller { get; set; } = null!;

    /// <summary>Bien físico o servicio (tutorías, asesorías…).</summary>
    public ListingKind Kind { get; set; } = ListingKind.Product;

    public required string Name { get; set; }
    public required string Category { get; set; }

    /// <summary>Precio, o mínimo del rango cuando hay <see cref="PriceMax"/>.</summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Máximo del rango. Solo para servicios: su costo depende del alcance, y
    /// obligar a un precio único fuerza a inventarse una cifra. Null = precio fijo.
    /// </summary>
    public decimal? PriceMax { get; set; }

    public string? Description { get; set; }

    /// <summary>Ubicación de entrega (producto) o modalidad/lugar (servicio) (RF-MKT-04).</summary>
    public string? DeliveryLocation { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Published;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }

    /// <summary>
    /// Cuándo la ocultó la moderación (no el autor). Mientras tenga valor, el autor
    /// no puede volver a mostrarla: antes bastaba con pulsar «mostrar» para deshacer
    /// lo que había decidido un moderador.
    /// </summary>
    public DateTime? ModeratedAt { get; set; }

    /// <summary>
    /// Último cambio de cualquier tipo (datos, estado, fotos). Lo pone el DbContext
    /// al guardar, así ningún camino lo olvida. Es lo que una web externa pide para
    /// traer solo lo cambiado («desde tal fecha»).
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// El vendedor permite que el producto aparezca también en webs externas
    /// (CatalogConnection Outbound). Sí por defecto: la web de catálogo es del colectivo.
    /// </summary>
    public bool AllowExternalListing { get; set; } = true;

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    /// <summary>Su gemelo en webs externas: de dónde vino o dónde se publicó.</summary>
    public ICollection<ProductExternalLink> ExternalLinks { get; set; } = new List<ProductExternalLink>();
}
