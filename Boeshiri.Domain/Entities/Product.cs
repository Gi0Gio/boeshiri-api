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

    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public string? Description { get; set; }

    /// <summary>Ubicación de entrega (RF-MKT-04).</summary>
    public string? DeliveryLocation { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Published;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }

    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
