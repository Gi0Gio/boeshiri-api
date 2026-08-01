using System.Text;
using Boeshiri.Application.Common;
using Boeshiri.Infrastructure.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Boeshiri.Tests.Storage;

public class UploadProcessorTests
{
    private readonly UploadProcessor _processor = new();

    [Fact]
    public async Task Image_IsConvertedToWebp()
    {
        using var jpeg = await ImagenAsync(400, 300, new JpegEncoder());

        using var resultado = await _processor.ProcessAsync(jpeg, "foto.jpg", "publicaciones");

        Assert.Equal("image/webp", resultado.ContentType);
        Assert.Equal("foto.webp", resultado.FileName);

        // El contenido es WebP de verdad, no solo el nombre.
        var info = await Image.IdentifyAsync(resultado.Content);
        Assert.Equal("Webp", info.Metadata.DecodedImageFormat?.Name, ignoreCase: true);
    }

    [Fact]
    public async Task Image_LargerThanFolderLimit_IsResized()
    {
        using var grande = await ImagenAsync(2400, 1200, new PngEncoder());

        using var resultado = await _processor.ProcessAsync(grande, "ancha.png", "publicaciones");

        var info = await Image.IdentifyAsync(resultado.Content);
        Assert.Equal(1600, info.Width);        // lado mayor recortado al máximo
        Assert.Equal(800, info.Height);        // proporción conservada
    }

    [Fact]
    public async Task Avatar_UsesSmallerBound()
    {
        using var grande = await ImagenAsync(2000, 2000, new PngEncoder());

        using var resultado = await _processor.ProcessAsync(grande, "yo.png", "avatars");

        var info = await Image.IdentifyAsync(resultado.Content);
        Assert.Equal(800, info.Width);
    }

    [Fact]
    public async Task SmallImage_IsNotUpscaled()
    {
        using var pequena = await ImagenAsync(120, 90, new PngEncoder());

        using var resultado = await _processor.ProcessAsync(pequena, "mini.png", "publicaciones");

        var info = await Image.IdentifyAsync(resultado.Content);
        Assert.Equal(120, info.Width);
        Assert.Equal(90, info.Height);
    }

    [Fact]
    public async Task NonImage_WithImageExtension_IsRejected()
    {
        // Un ejecutable renombrado a .jpg: la extensión y el Content-Type mienten,
        // el contenido no. Este es el caso que la validación existe para frenar.
        using var falso = new MemoryStream("MZ\0 no soy una imagen"u8.ToArray());

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _processor.ProcessAsync(falso, "troyano.jpg", "publicaciones"));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Image_OverSizeLimit_IsRejected()
    {
        // Ruido aleatorio: incompresible, así que supera los 5 MB de verdad.
        var bytes = new byte[6 * 1024 * 1024];
        Random.Shared.NextBytes(bytes);
        using var pesada = new MemoryStream(bytes);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _processor.ProcessAsync(pesada, "enorme.jpg", "publicaciones"));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("5 MB", ex.Message);
    }

    [Fact]
    public async Task Pdf_IsAcceptedUnchanged()
    {
        var contenido = Encoding.ASCII.GetBytes("%PDF-1.7\n% documento de prueba\n%%EOF");
        using var pdf = new MemoryStream(contenido);

        using var resultado = await _processor.ProcessAsync(pdf, "acta.pdf", "documentos");

        Assert.Equal("application/pdf", resultado.ContentType);
        Assert.Equal("acta.pdf", resultado.FileName);
        Assert.Equal(contenido.Length, resultado.Content.Length);
    }

    [Fact]
    public async Task NonPdf_InDocumentFolder_IsRejected()
    {
        using var noPdf = new MemoryStream("solo texto plano"u8.ToArray());

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _processor.ProcessAsync(noPdf, "notas.pdf", "documentos"));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ImageIntoDocumentFolder_IsRejected()
    {
        using var jpeg = await ImagenAsync(100, 100, new JpegEncoder());

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _processor.ProcessAsync(jpeg, "foto.jpg", "documentos"));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task UnknownFolder_IsRejected()
    {
        using var jpeg = await ImagenAsync(100, 100, new JpegEncoder());

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _processor.ProcessAsync(jpeg, "foto.jpg", "backups"));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task EmptyFile_IsRejected()
    {
        using var vacio = new MemoryStream();

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _processor.ProcessAsync(vacio, "nada.jpg", "publicaciones"));

        Assert.Equal(400, ex.StatusCode);
    }

    // ── Helpers ──────────────────────────────────────────────────
    private static async Task<MemoryStream> ImagenAsync(int ancho, int alto, IImageEncoder encoder)
    {
        using var imagen = new Image<Rgba32>(ancho, alto);
        var ms = new MemoryStream();
        await imagen.SaveAsync(ms, encoder);
        ms.Position = 0;
        return ms;
    }
}
