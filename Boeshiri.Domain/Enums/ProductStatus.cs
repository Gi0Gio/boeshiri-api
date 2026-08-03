namespace Boeshiri.Domain.Enums;

/// <summary>Estado de un producto del marketplace (§9).</summary>
public enum ProductStatus
{
    Published,

    /// <summary>Oculto temporalmente por el miembro (RF-MKT-05).</summary>
    Hidden,

    /// <summary>
    /// Vendido (tras concretar una venta). Solo aplica a bienes físicos: un
    /// servicio no se agota, y sin disponibilidad lo que corresponde es ocultarlo.
    /// </summary>
    Sold,

    Deleted
}
