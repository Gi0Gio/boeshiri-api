namespace Boeshiri.Domain.Enums;

/// <summary>Tipos de publicación (Requerimientos §6).</summary>
public enum PublicationType
{
    Article,
    Photo,
    Video,
    Music,
    News
}

/// <summary>Visibilidad del contenido (RF-PUB-18).</summary>
public enum Visibility
{
    /// <summary>Visible para cualquiera.</summary>
    Public,

    /// <summary>Exclusivo para miembros autenticados.</summary>
    Members
}

/// <summary>Estado de un contenido moderable (publicación, evento, artículo oficial).</summary>
public enum ContentStatus
{
    Published,
    Hidden,
    Deleted
}
