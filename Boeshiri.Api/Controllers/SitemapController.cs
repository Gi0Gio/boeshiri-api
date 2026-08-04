using System.Text;
using System.Xml;
using Boeshiri.Application.Sitemap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Sitemap del contenido que cambia (publicaciones, eventos, anuncios, perfiles).
///
/// Las páginas fijas del sitio viven en un sitemap estático del front; estas no
/// pueden, porque nacen y desaparecen todos los días y nadie va a mantener el
/// archivo a mano. Se sirve bajo el dominio del sitio por el proxy de Netlify,
/// que es un requisito de los buscadores: un sitemap solo puede declarar URLs
/// del host desde el que se descarga.
/// </summary>
[ApiController]
[AllowAnonymous]
public class SitemapController(ISitemapService sitemap) : ControllerBase
{
    [HttpGet("sitemap-contenido.xml")]
    public async Task<IActionResult> Contenido(CancellationToken ct)
    {
        var entradas = await sitemap.ListPublicUrlsAsync(ct);

        var sb = new Utf8StringWriter();
        using (var w = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
        {
            w.WriteStartDocument();
            w.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            foreach (var e in entradas)
            {
                w.WriteStartElement("url");
                w.WriteElementString("loc", e.Location);
                w.WriteElementString("lastmod", e.LastModified.ToString("yyyy-MM-dd"));
                w.WriteElementString("changefreq", e.ChangeFrequency);
                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndDocument();
        }

        // Media hora: lo justo para que una publicación nueva se anuncie pronto
        // sin rehacer la consulta en cada visita de un robot.
        Response.Headers.CacheControl = "public, max-age=1800";
        return Content(sb.ToString(), "application/xml; charset=utf-8");
    }

    /// <summary>
    /// Un StringWriter dice que escribe UTF-16 (así es en memoria), y XmlWriter
    /// se lo cree y estampa encoding="utf-16" en la declaración. El cuerpo sale
    /// como UTF-8 y el parser del buscador se encuentra un XML que se contradice.
    /// </summary>
    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
