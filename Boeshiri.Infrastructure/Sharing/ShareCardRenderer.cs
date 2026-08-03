using System.Reflection;
using Boeshiri.Application.Abstractions;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Boeshiri.Infrastructure.Sharing;

/// <summary>
/// Compone la tarjeta para compartir con la identidad de Boesh Irí.
///
/// El membrete se dibuja (tipografía + color), no se apoya en una plantilla
/// externa, para que no dependa de un archivo de diseño que haya que mantener
/// aparte. Las fuentes van embebidas en el ensamblado: los contenedores de
/// Railway no traen fuentes instaladas y usar las del sistema fallaría en
/// producción aunque funcione en local.
/// </summary>
public class ShareCardRenderer : IShareCardRenderer
{
    // Paleta oficial (index.css del front).
    private static readonly Color JungleDeep = Color.ParseHex("00110E");
    private static readonly Color Jungle = Color.ParseHex("002420");
    private static readonly Color Caribbean = Color.ParseHex("00E6BC");
    private static readonly Color Cream = Color.ParseHex("F6FBEF");
    private static readonly Color Tea = Color.ParseHex("D9F2C2");

    private static readonly FontFamily Display;
    private static readonly FontFamily Body;

    private readonly HttpClient _http;
    private readonly ILogger<ShareCardRenderer> _logger;

    static ShareCardRenderer()
    {
        var collection = new FontCollection();
        Display = Load(collection, "Oswald.ttf");
        Body = Load(collection, "Montserrat.ttf");
    }

    public ShareCardRenderer(HttpClient http, ILogger<ShareCardRenderer> logger)
    {
        _http = http;
        _logger = logger;
    }

    private static FontFamily Load(FontCollection collection, string archivo)
    {
        var asm = Assembly.GetExecutingAssembly();
        var nombre = asm.GetManifestResourceNames().Single(n => n.EndsWith(archivo, StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(nombre)!;
        return collection.Add(stream);
    }

    public async Task<byte[]> RenderAsync(ShareCardContent content, ShareFormat format, CancellationToken ct = default)
    {
        var (ancho, alto) = format == ShareFormat.Story ? (1080, 1920) : (1080, 1080);

        using var lienzo = new Image<Rgba32>(ancho, alto);
        lienzo.Mutate(x => x.Fill(JungleDeep));

        // ── Zona de imagen ───────────────────────────────────────
        // En cuadrado ocupa la mitad superior; en historia, algo más de un tercio,
        // dejando sitio al texto sin que quede apretado bajo la interfaz de la app.
        var altoFoto = format == ShareFormat.Story ? (int)(alto * 0.52) : (int)(alto * 0.58);
        var foto = await DescargarAsync(content.ImageUrl, ct);

        if (foto is not null)
        {
            using (foto)
            {
                foto.Mutate(x => x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Crop,
                    Size = new Size(ancho, altoFoto),
                }));
                lienzo.Mutate(x => x.DrawImage(foto, new Point(0, 0), 1f));
            }
        }
        else
        {
            // Sin imagen, un bloque de marca en vez de un hueco vacío.
            lienzo.Mutate(x => x.Fill(Jungle, new RectangleF(0, 0, ancho, altoFoto)));
        }

        // Degradado sobre el borde inferior de la foto: separa la imagen del texto
        // sin recortarla, y evita que un fondo claro deje el título ilegible.
        var fundido = Math.Min(220, altoFoto / 3);
        for (var i = 0; i < fundido; i++)
        {
            var alpha = (float)i / fundido;
            lienzo.Mutate(x => x.Fill(
                JungleDeep.WithAlpha(alpha),
                new RectangleF(0, altoFoto - fundido + i, ancho, 1)));
        }

        // Filo de color bajo la imagen: el mismo recurso del membrete del correo.
        lienzo.Mutate(x => x.Fill(Caribbean, new RectangleF(0, altoFoto, ancho, 8)));

        // ── Texto ────────────────────────────────────────────────
        var margen = 72f;
        var anchoUtil = ancho - margen * 2;
        var y = altoFoto + (format == ShareFormat.Story ? 90f : 64f);

        var fEyebrow = Body.CreateFont(28, FontStyle.Bold);
        var fTitulo = Display.CreateFont(format == ShareFormat.Story ? 92 : 76, FontStyle.Bold);
        var fSub = Body.CreateFont(36, FontStyle.Regular);
        var fMarca = Display.CreateFont(40, FontStyle.Bold);
        var fPie = Body.CreateFont(24, FontStyle.Regular);

        if (!string.IsNullOrWhiteSpace(content.Eyebrow))
        {
            lienzo.Mutate(x => x.DrawText(
                EspaciarLetras(content.Eyebrow.ToUpperInvariant()), fEyebrow, Caribbean, new PointF(margen, y)));
            y += 56;
        }

        var titulo = new RichTextOptions(fTitulo)
        {
            Origin = new PointF(margen, y),
            WrappingLength = anchoUtil,
            LineSpacing = 1.05f,
        };
        lienzo.Mutate(x => x.DrawText(titulo, content.Title.ToUpperInvariant(), Cream));

        var lineas = TextMeasurer.MeasureSize(content.Title.ToUpperInvariant(), titulo);
        y += lineas.Height + 34;

        if (!string.IsNullOrWhiteSpace(content.Subtitle))
        {
            lienzo.Mutate(x => x.DrawText(
                new RichTextOptions(fSub) { Origin = new PointF(margen, y), WrappingLength = anchoUtil },
                content.Subtitle, Tea));
        }

        // ── Membrete al pie ──────────────────────────────────────
        // En historia se sube: Instagram y WhatsApp superponen su barra de envío
        // sobre los últimos ~250 px, y el membrete quedaría tapado justo ahí.
        var margenPie = format == ShareFormat.Story ? 260f : margen;
        var yPie = alto - margenPie - 56;
        lienzo.Mutate(x => x.Fill(Caribbean, new RectangleF(margen, yPie - 26, 64, 5)));
        lienzo.Mutate(x => x.DrawText(EspaciarLetras("BOESH IRÍ"), fMarca, Cream, new PointF(margen, yPie)));
        lienzo.Mutate(x => x.DrawText(
            "Colectivo cultural · Chiriquí, Panamá", fPie, Tea.WithAlpha(0.65f), new PointF(margen, yPie + 52)));

        using var salida = new MemoryStream();
        await lienzo.SaveAsync(salida, new PngEncoder(), ct);
        return salida.ToArray();
    }

    /// <summary>
    /// ImageSharp no aplica letter-spacing, y el membrete de la marca depende de
    /// él. Insertar espacios finos lo imita sin salirse de la tipografía.
    /// </summary>
    private static string EspaciarLetras(string texto) => string.Join(' ', texto.ToCharArray());

    private async Task<Image<Rgba32>?> DescargarAsync(string? url, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct);
            return Image.Load<Rgba32>(bytes);
        }
        catch (Exception ex)
        {
            // Una imagen inaccesible no debe tumbar la tarjeta: se compone sin ella.
            _logger.LogWarning(ex, "No se pudo descargar la imagen {Url} para la tarjeta de compartir", url);
            return null;
        }
    }
}
