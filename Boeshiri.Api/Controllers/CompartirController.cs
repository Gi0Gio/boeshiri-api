using System.Net;
using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Marketplace;
using Boeshiri.Application.Publications;
using Boeshiri.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Páginas e imágenes para compartir en redes (RF-MKT-05).
///
/// Existe porque WhatsApp, Instagram y Facebook NO ejecutan el front: su robot
/// descarga el HTML crudo y lee las etiquetas og:*. Una SPA le devuelve siempre
/// el mismo index vacío, así que el enlace saldría sin título ni imagen. Estas
/// rutas devuelven HTML mínimo con los metadatos correctos y mandan a la persona
/// al sitio.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("compartir")]
public class CompartirController(
    IMarketplaceService marketplace,
    IPublicationService publications,
    IShareCardRenderer renderer,
    IOptions<AppOptions> app) : ControllerBase
{
    private string Front => app.Value.PublicBaseUrl.TrimEnd('/');
    private string ApiBase => $"{Request.Scheme}://{Request.Host}";

    // ── Anuncios del marketplace ─────────────────────────────────

    [HttpGet("producto/{id:guid}")]
    public async Task<IActionResult> Producto(Guid id, CancellationToken ct)
    {
        var p = await marketplace.GetDetailAsync(id, ct);
        return Html(
            titulo: p.Name,
            descripcion: $"{(p.Kind == Boeshiri.Domain.Enums.ListingKind.Service ? "Servicio" : "Producto")} de {p.SellerName} · {Precio(p.Price, p.PriceMax)}",
            imagen: $"{ApiBase}/compartir/producto/{id}/imagen.png",
            destino: $"{Front}/marketplace/{id}");
    }

    [HttpGet("producto/{id:guid}/imagen.png")]
    public async Task<IActionResult> ProductoImagen(Guid id, [FromQuery] string? formato, CancellationToken ct)
    {
        var p = await marketplace.GetDetailAsync(id, ct);
        var bytes = await renderer.RenderAsync(
            new ShareCardContent(
                Eyebrow: p.Kind == Boeshiri.Domain.Enums.ListingKind.Service ? "Servicio" : "En el marketplace",
                Title: p.Name,
                Subtitle: $"{Precio(p.Price, p.PriceMax)} · {p.SellerName}",
                ImageUrl: p.Images.FirstOrDefault()),
            Formato(formato), ct);

        return Imagen(bytes);
    }

    // ── Publicaciones ────────────────────────────────────────────

    [HttpGet("publicacion/{id:guid}")]
    public async Task<IActionResult> Publicacion(Guid id, CancellationToken ct)
    {
        var p = await publications.GetDetailAsync(id, authenticated: false, ct);
        return Html(
            titulo: p.Title,
            descripcion: $"{Tipo(p.Type)} de {p.AuthorName}",
            imagen: $"{ApiBase}/compartir/publicacion/{id}/imagen.png",
            destino: $"{Front}/publicaciones/{id}");
    }

    [HttpGet("publicacion/{id:guid}/imagen.png")]
    public async Task<IActionResult> PublicacionImagen(Guid id, [FromQuery] string? formato, CancellationToken ct)
    {
        var p = await publications.GetDetailAsync(id, authenticated: false, ct);
        var bytes = await renderer.RenderAsync(
            new ShareCardContent(
                Eyebrow: Tipo(p.Type),
                Title: p.Title,
                Subtitle: p.AuthorName,
                ImageUrl: p.Images.FirstOrDefault()),
            Formato(formato), ct);

        return Imagen(bytes);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static ShareFormat Formato(string? f) =>
        string.Equals(f, "historia", StringComparison.OrdinalIgnoreCase) ? ShareFormat.Story : ShareFormat.Square;

    /// <summary>"0.##" evita el "$80.00" que sale del decimal de la base.</summary>
    private static string Precio(decimal price, decimal? priceMax) =>
        priceMax is not null && priceMax > price ? $"${price:0.##} – ${priceMax:0.##}"
        : price > 0 ? $"${price:0.##}"
        : "A convenir";

    private static string Tipo(Boeshiri.Domain.Enums.PublicationType t) => t switch
    {
        Boeshiri.Domain.Enums.PublicationType.News => "Noticia",
        Boeshiri.Domain.Enums.PublicationType.Article => "Artículo",
        Boeshiri.Domain.Enums.PublicationType.Photo => "Foto",
        Boeshiri.Domain.Enums.PublicationType.Video => "Video",
        Boeshiri.Domain.Enums.PublicationType.Music => "Música",
        _ => "Publicación"
    };

    private IActionResult Imagen(byte[] bytes)
    {
        // La tarjeta se recompone en cada petición; la caché evita rehacerla en
        // cada vista previa que pidan las apps al reenviarse el enlace.
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(bytes, "image/png");
    }

    /// <summary>
    /// HTML mínimo: metadatos para el robot y redirección inmediata para la persona.
    /// Todo lo interpolado va escapado — son datos que escriben los miembros.
    /// </summary>
    private ContentResult Html(string titulo, string descripcion, string imagen, string destino)
    {
        var t = WebUtility.HtmlEncode(titulo);
        var d = WebUtility.HtmlEncode(descripcion);
        var img = WebUtility.HtmlEncode(imagen);
        var url = WebUtility.HtmlEncode(destino);

        return Content($"""
            <!DOCTYPE html>
            <html lang="es">
            <head>
              <meta charset="utf-8">
              <title>{t} — Boesh Irí</title>
              <meta property="og:type" content="website">
              <meta property="og:site_name" content="Boesh Irí">
              <meta property="og:title" content="{t}">
              <meta property="og:description" content="{d}">
              <meta property="og:image" content="{img}">
              <meta property="og:image:width" content="1080">
              <meta property="og:image:height" content="1080">
              <meta property="og:url" content="{url}">
              <meta name="twitter:card" content="summary_large_image">
              <meta name="twitter:title" content="{t}">
              <meta name="twitter:description" content="{d}">
              <meta name="twitter:image" content="{img}">
              <meta http-equiv="refresh" content="0; url={url}">
              <link rel="canonical" href="{url}">
            </head>
            <body style="margin:0;background:#00110e;color:#d9f2c2;font-family:system-ui,sans-serif">
              <p style="padding:2rem">Abriendo <a href="{url}" style="color:#00e6bc">{t}</a>…</p>
            </body>
            </html>
            """, "text/html; charset=utf-8");
    }
}
