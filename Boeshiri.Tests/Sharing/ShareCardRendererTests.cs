using Boeshiri.Application.Abstractions;
using Boeshiri.Infrastructure.Sharing;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;

namespace Boeshiri.Tests.Sharing;

/// <summary>
/// La tarjeta se compone en el servidor y nadie la revisa antes de que salga en
/// un WhatsApp, así que lo que se prueba es que ningún texto la tumbe: títulos
/// larguísimos, de una sola palabra o con acentos.
/// </summary>
public class ShareCardRendererTests
{
    private static ShareCardRenderer NewRenderer() =>
        new(new HttpClient(), NullLogger<ShareCardRenderer>.Instance);

    [Theory]
    [InlineData("Lámina")]
    [InlineData("Taller de serigrafía artesanal para principiantes en David, Chiriquí, con materiales incluidos")]
    [InlineData("Antidisestablishmentarianismo")]
    [InlineData("A")]
    public async Task RenderAsync_CualquierTitulo_DevuelvePngDelTamañoEsperado(string titulo)
    {
        var bytes = await NewRenderer().RenderAsync(
            new ShareCardContent("Servicio", titulo, "$25 · Alguien de la comunidad", ImageUrl: null),
            ShareFormat.Square);

        using var img = Image.Load(bytes);
        Assert.Equal(1080, img.Width);
        Assert.Equal(1080, img.Height);
    }

    [Fact]
    public async Task RenderAsync_Historia_UsaElLienzoVertical()
    {
        var bytes = await NewRenderer().RenderAsync(
            new ShareCardContent("Evento", "Encuentro de artistas", null, ImageUrl: null),
            ShareFormat.Story);

        using var img = Image.Load(bytes);
        Assert.Equal(1080, img.Width);
        Assert.Equal(1920, img.Height);
    }

    /// <summary>
    /// Una imagen que no se puede descargar no debe tumbar la tarjeta: se compone
    /// sin ella. Es el caso real de un objeto borrado del bucket.
    /// </summary>
    [Fact]
    public async Task RenderAsync_ImagenInaccesible_ComponeIgual()
    {
        var bytes = await NewRenderer().RenderAsync(
            new ShareCardContent("Producto", "Lámina", "$25", ImageUrl: "https://no.existe.invalid/x.png"),
            ShareFormat.Square);

        using var img = Image.Load(bytes);
        Assert.Equal(1080, img.Width);
    }
}
