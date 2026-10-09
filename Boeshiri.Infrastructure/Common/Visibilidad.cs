using Boeshiri.Domain.Enums;

namespace Boeshiri.Infrastructure.Common;

/// <summary>
/// Qué autores pueden tener contenido a la vista. Suspender o expulsar a alguien
/// no borra lo que publicó, pero sí lo retira de lo público mientras dure: antes sus
/// anuncios y publicaciones seguían en el sitio y en el sitemap. Retirados e
/// inactivos conservan lo suyo (se fueron o pararon, no fueron sancionados).
/// </summary>
public static class Visibilidad
{
    public static bool AutorVisible(MemberStatus status) =>
        status != MemberStatus.Suspended && status != MemberStatus.Expelled;
}
