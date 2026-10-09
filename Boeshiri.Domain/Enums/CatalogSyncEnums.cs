namespace Boeshiri.Domain.Enums;

/// <summary>Hacia dónde viaja el catálogo con una web externa.</summary>
public enum CatalogSyncDirection
{
    /// <summary>Boesh Irí trae productos de esa web.</summary>
    Inbound,
    /// <summary>Esa web toma productos de Boesh Irí.</summary>
    Outbound,
    /// <summary>Las dos cosas.</summary>
    Both
}

/// <summary>Por qué un producto está enlazado con una web externa.</summary>
public enum ExternalLinkRole
{
    /// <summary>Vino de esa web: allí está el original y aquí la copia.</summary>
    Imported,
    /// <summary>Salió de Boesh Irí y se publicó también allí.</summary>
    Published
}
