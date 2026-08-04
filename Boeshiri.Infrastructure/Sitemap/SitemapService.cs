using Boeshiri.Application.Sitemap;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Auth;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Boeshiri.Infrastructure.Sitemap;

/// <summary>
/// Solo entra lo que ve cualquiera sin sesión: la visibilidad "Members" y todo
/// lo oculto o borrado se queda fuera. Anunciar en el sitemap una URL que luego
/// pide sesión es la forma más rápida de que un buscador desconfíe del sitio.
/// </summary>
public class SitemapService(BoeshiriDbContext db, IOptions<AppOptions> appOptions) : ISitemapService
{
    private readonly string _site = appOptions.Value.PublicBaseUrl.TrimEnd('/');

    public async Task<IReadOnlyList<SitemapEntry>> ListPublicUrlsAsync(CancellationToken ct = default)
    {
        var entradas = new List<SitemapEntry>();

        var publicaciones = await db.Publications
            .Where(p => p.Visibility == Visibility.Public && p.Status == ContentStatus.Published)
            .Select(p => new { p.Id, Fecha = p.EditedAt ?? p.CreatedAt })
            .ToListAsync(ct);
        entradas.AddRange(publicaciones.Select(p => new SitemapEntry($"{_site}/publicaciones/{p.Id}", p.Fecha, "monthly")));

        var eventos = await db.Events
            .Where(e => e.Visibility == Visibility.Public && e.Status == ContentStatus.Published)
            .Select(e => new { e.Id, e.CreatedAt })
            .ToListAsync(ct);
        entradas.AddRange(eventos.Select(e => new SitemapEntry($"{_site}/eventos/{e.Id}", e.CreatedAt, "weekly")));

        var anuncios = await db.Products
            .Where(p => p.Status == ProductStatus.Published)
            .Select(p => new { p.Id, Fecha = p.EditedAt ?? p.CreatedAt })
            .ToListAsync(ct);
        entradas.AddRange(anuncios.Select(p => new SitemapEntry($"{_site}/marketplace/{p.Id}", p.Fecha, "weekly")));

        // Los perfiles son el portafolio del colectivo: es lo que hace que a una
        // persona la encuentren por su nombre y llegue al sitio por ahí.
        var perfiles = await db.Users
            .Where(u => u.Status == MemberStatus.Active)
            .Select(u => u.Id)
            .ToListAsync(ct);
        entradas.AddRange(perfiles.Select(id => new SitemapEntry($"{_site}/perfil/{id}", DateTime.UtcNow, "monthly")));

        return entradas;
    }
}
