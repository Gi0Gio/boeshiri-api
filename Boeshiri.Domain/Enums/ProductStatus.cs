namespace Boeshiri.Domain.Enums;

/// <summary>Estado de un producto del marketplace (§9).</summary>
public enum ProductStatus
{
    Published,

    /// <summary>Oculto temporalmente por el miembro (RF-MKT-05).</summary>
    Hidden,

    /// <summary>Vendido (tras concretar una venta).</summary>
    Sold,

    Deleted
}
