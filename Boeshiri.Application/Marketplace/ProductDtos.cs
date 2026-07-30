using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Marketplace;

public record CreateProductRequest
{
    /// <summary>Producto (bien físico) o servicio (tutorías, asesorías…).</summary>
    public ListingKind Kind { get; init; } = ListingKind.Product;

    [Required, MaxLength(160)]
    public required string Name { get; init; }

    [Required, MaxLength(80)]
    public required string Category { get; init; }

    [Range(0, 1000000)]
    public decimal Price { get; init; }

    [MaxLength(4000)]
    public string? Description { get; init; }

    [MaxLength(200)]
    public string? DeliveryLocation { get; init; }

    public List<string>? Images { get; init; }
}

public record UpdateProductRequest
{
    [Required, MaxLength(160)]
    public required string Name { get; init; }

    [Required, MaxLength(80)]
    public required string Category { get; init; }

    [Range(0, 1000000)]
    public decimal Price { get; init; }

    [MaxLength(4000)]
    public string? Description { get; init; }

    [MaxLength(200)]
    public string? DeliveryLocation { get; init; }
}

/// <summary>Acción sobre el estado del producto (RF-MKT-05).</summary>
public enum ProductStatusAction
{
    Hide,
    Show,
    Sold,
    Delete
}

public record ChangeProductStatusRequest
{
    [Required]
    public required ProductStatusAction Action { get; init; }
}

/// <summary>Datos de contacto del vendedor tomados de su perfil (RF-MKT-02/04).</summary>
public record SellerContactDto(
    string? Email,
    string? Phone,
    IReadOnlyList<ContactLinkDto> SocialLinks);

public record ContactLinkDto(SocialNetworkType Type, string Value);

public record ProductSummaryDto(
    Guid Id,
    ListingKind Kind,
    string Name,
    string Category,
    decimal Price,
    Guid SellerId,
    string SellerName,
    ProductStatus Status,
    string? CoverImage);

public record ProductDetailDto(
    Guid Id,
    ListingKind Kind,
    string Name,
    string Category,
    decimal Price,
    string? Description,
    string? DeliveryLocation,
    ProductStatus Status,
    Guid SellerId,
    string SellerName,
    SellerContactDto Contact,
    IReadOnlyList<string> Images);

/// <summary>Enlace + imagen listos para compartir en redes (RF-MKT-05).</summary>
public record ProductShareDto(string Url, string? Image);
