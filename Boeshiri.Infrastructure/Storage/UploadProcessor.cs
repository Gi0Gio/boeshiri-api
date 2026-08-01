using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Common;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Boeshiri.Infrastructure.Storage;

/// <summary>
/// Política de subida (RF-IMG-01, SDD §8). Solo entran dos cosas al bucket:
/// imágenes —reencodadas a WebP y redimensionadas— y PDF. Todo lo demás se rechaza.
///
/// La validación mira el CONTENIDO, no el Content-Type ni la extensión: ambos los
/// controla el cliente. Una imagen es una imagen si ImageSharp logra decodificarla;
/// un PDF lo es si empieza por la firma %PDF-.
/// </summary>
public class UploadProcessor : IUploadProcessor
{
    private const long MaxImageBytes = 5 * 1024 * 1024;   // RF-IMG-01
    private const long MaxPdfBytes = 10 * 1024 * 1024;
    private const int WebpQuality = 82;

    /// <summary>Lado máximo en píxeles por carpeta. Ausente = carpeta de documentos.</summary>
    private static readonly Dictionary<string, int> ImageFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["avatars"] = 800,
        ["publicaciones"] = 1600,
        ["productos"] = 1600,
        ["misc"] = 1600,
    };

    private const string DocumentFolder = "documentos";

    /// <summary>Formatos de entrada aceptados, por nombre de formato de ImageSharp.</summary>
    private static readonly HashSet<string> AllowedImageFormats =
        new(StringComparer.OrdinalIgnoreCase) { "JPEG", "PNG", "WEBP" };

    public async Task<ProcessedUpload> ProcessAsync(Stream input, string fileName, string folder, CancellationToken ct = default)
    {
        if (string.Equals(folder, DocumentFolder, StringComparison.OrdinalIgnoreCase))
            return await ProcessPdfAsync(input, fileName, ct);

        if (ImageFolders.TryGetValue(folder, out var maxDim))
            return await ProcessImageAsync(input, fileName, maxDim, ct);

        throw AppException.BadRequest($"No se admiten subidas a '{folder}'.");
    }

    private static async Task<ProcessedUpload> ProcessImageAsync(Stream input, string fileName, int maxDim, CancellationToken ct)
    {
        var buffer = await BufferAsync(input, MaxImageBytes, "La imagen supera el límite de 5 MB.", ct);

        Image image;
        try
        {
            image = await Image.LoadAsync(buffer, ct);
        }
        catch (Exception)
        {
            // No decodifica ⇒ no es una imagen, por mucho que la extensión lo diga.
            throw AppException.BadRequest("El archivo no es una imagen válida. Usa JPG, PNG o WebP.");
        }

        using (image)
        {
            var formato = image.Metadata.DecodedImageFormat?.Name ?? "desconocido";
            if (!AllowedImageFormats.Contains(formato))
                throw AppException.BadRequest($"Formato de imagen no admitido ({formato}). Usa JPG, PNG o WebP.");

            image.Mutate(x =>
            {
                // Las fotos de móvil traen la rotación en los metadatos: sin esto se
                // guardarían tumbadas, porque al reencodar se pierde ese dato.
                x.AutoOrient();
                if (Math.Max(image.Width, image.Height) > maxDim)
                    x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(maxDim, maxDim) });
            });

            // Fuera metadatos: una foto de móvil puede llevar coordenadas GPS, y esto
            // se publica en URLs abiertas. Además recorta bastantes KB.
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            var salida = new MemoryStream();
            await image.SaveAsWebpAsync(salida, new WebpEncoder { Quality = WebpQuality }, ct);
            salida.Position = 0;

            return new ProcessedUpload(salida, $"{SinExtension(fileName)}.webp", "image/webp");
        }
    }

    private static async Task<ProcessedUpload> ProcessPdfAsync(Stream input, string fileName, CancellationToken ct)
    {
        var buffer = await BufferAsync(input, MaxPdfBytes, "El documento supera el límite de 10 MB.", ct);

        // Firma de PDF: los 5 primeros bytes son "%PDF-".
        Span<byte> firma = stackalloc byte[5];
        if (buffer.Length < 5 || buffer.Read(firma) < 5 || !firma.SequenceEqual("%PDF-"u8))
            throw AppException.BadRequest("El archivo no es un PDF válido. En esta biblioteca solo se admiten PDF.");

        buffer.Position = 0;
        return new ProcessedUpload(buffer, $"{SinExtension(fileName)}.pdf", "application/pdf");
    }

    /// <summary>
    /// Vuelca el stream a memoria aplicando el límite. Se copia en trozos y se corta
    /// al pasarse: leerlo entero antes de medir permitiría agotar la memoria con un
    /// stream sin longitud declarada.
    /// </summary>
    private static async Task<MemoryStream> BufferAsync(Stream input, long maxBytes, string mensaje, CancellationToken ct)
    {
        var salida = new MemoryStream();
        var trozo = new byte[81920];
        int leidos;
        while ((leidos = await input.ReadAsync(trozo, ct)) > 0)
        {
            if (salida.Length + leidos > maxBytes)
            {
                await salida.DisposeAsync();
                throw AppException.BadRequest(mensaje);
            }
            await salida.WriteAsync(trozo.AsMemory(0, leidos), ct);
        }

        if (salida.Length == 0)
            throw AppException.BadRequest("El archivo está vacío.");

        salida.Position = 0;
        return salida;
    }

    private static string SinExtension(string fileName)
    {
        var limpio = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
        return string.IsNullOrWhiteSpace(limpio) ? "archivo" : limpio;
    }
}
