using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Common;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Storage;

/// <summary>
/// Reglas para las URLs de archivos que llegan del cliente y para soltar archivos
/// del bucket.
///
/// Antes, cualquier URL se guardaba tal cual y al editar se borraba del bucket la
/// que «salía»: bastaba con poner como foto de perfil la URL de un documento de la
/// Junta y cambiarla después para que el servidor borrara el documento. Ahora:
/// <list type="bullet">
/// <item>las URLs nuevas tienen que ser de nuestro bucket y de la carpeta que toca;</item>
/// <item>las que la entidad ya tenía se respetan (datos de antes de esta regla);</item>
/// <item>un archivo solo se borra si ninguna fila de la base lo sigue usando.</item>
/// </list>
/// </summary>
public static class ArchivosGuard
{
    /// <summary>Carpetas de imagen que el procesador de subidas acepta.</summary>
    public static readonly string[] CarpetasImagen = ["avatars", "publicaciones", "productos", "misc"];

    public const string CarpetaDocumentos = "documentos";

    /// <summary>
    /// Exige que cada URL NUEVA (no presente en <paramref name="yaGuardadas"/>) sea un
    /// objeto de nuestro bucket en alguna de las carpetas permitidas.
    /// </summary>
    public static void ExigirPropias(
        IFileStorage storage, IEnumerable<string>? urls, IEnumerable<string>? yaGuardadas, string[] carpetas, string campo)
    {
        var previas = (yaGuardadas ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var url in urls ?? [])
        {
            if (previas.Contains(url))
                continue;
            if (!carpetas.Any(c => storage.IsOwnUrl(url, c)))
                throw AppException.BadRequest($"{campo}: la imagen o el archivo tiene que subirse desde el sitio.");
        }
    }

    /// <summary>
    /// Suelta el archivo del bucket si ninguna fila lo referencia. Se llama DESPUÉS
    /// de guardar, cuando la referencia propia ya no existe: si la URL sigue en uso
    /// en otro sitio (la foto de otra persona, un documento), no se toca.
    /// </summary>
    public static async Task BorrarSiSinUsoAsync(BoeshiriDbContext db, IFileStorage storage, string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var enUso =
            await db.Users.AnyAsync(u => u.PhotoUrl == url, ct) ||
            await db.PublicationImages.AnyAsync(i => i.Url == url, ct) ||
            await db.ProductImages.AnyAsync(i => i.Url == url, ct) ||
            await db.EventImages.AnyAsync(i => i.Url == url, ct) ||
            await db.Documents.AnyAsync(d => d.FileUrl == url, ct);

        if (!enUso)
            await storage.DeleteAsync(url, ct);
    }
}

/// <summary>Validación de enlaces externos (vídeos, música, referencias, redes).</summary>
public static class Enlaces
{
    /// <summary>
    /// Solo http/https absolutos. Un «javascript:» o un «data:» en un enlace que el
    /// front pinta como &lt;a href&gt; es código ejecutándose en el sitio.
    /// </summary>
    public static void ExigirWeb(string? url, string campo)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw AppException.BadRequest($"{campo}: el enlace tiene que empezar por https:// (o http://).");
    }
}
