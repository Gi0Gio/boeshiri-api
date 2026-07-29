namespace Boeshiri.Domain.Entities;

/// <summary>Imagen de un producto del marketplace.</summary>
public class ProductImage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public required string Url { get; set; }
    public int Order { get; set; }
}
