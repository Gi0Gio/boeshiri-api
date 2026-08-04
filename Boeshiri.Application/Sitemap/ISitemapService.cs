namespace Boeshiri.Application.Sitemap;

/// <summary>Una URL pública del sitio para el sitemap.</summary>
/// <param name="Location">URL absoluta bajo el dominio del sitio.</param>
/// <param name="LastModified">Última modificación conocida; los buscadores la usan para no re-rastrear lo que no cambió.</param>
/// <param name="ChangeFrequency">Pista de frecuencia (daily, weekly, monthly).</param>
public record SitemapEntry(string Location, DateTime LastModified, string ChangeFrequency);

/// <summary>
/// Reúne las URLs del contenido publicado para que los buscadores lo encuentren
/// sin depender de que un robot vaya siguiendo enlaces dentro de una SPA.
/// </summary>
public interface ISitemapService
{
    Task<IReadOnlyList<SitemapEntry>> ListPublicUrlsAsync(CancellationToken ct = default);
}
