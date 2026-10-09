using Boeshiri.Application.Abstractions;
using Boeshiri.Infrastructure.Sharing;
using Boeshiri.Tests.Support;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace Boeshiri.Tests.Sharing;

/// <summary>
/// La tarjeta se compone en el servidor y nadie la revisa antes de que salga en
/// un WhatsApp, así que lo que se prueba es que ningún texto la tumbe: títulos
/// larguísimos, de una sola palabra o con acentos.
/// </summary>
public class ShareCardRendererTests
{
    private static ShareCardRenderer NewRenderer(HttpMessageHandler? handler = null) =>
        new(new HttpClient(handler ?? new ContadorHandler()), new FakeFileStorage(),
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 64L * 1024 * 1024 }),
            NullLogger<ShareCardRenderer>.Instance);

    /// <summary>Responde con un PNG pequeño y cuenta cuántas descargas se pidieron.</summary>
    private sealed class ContadorHandler : HttpMessageHandler
    {
        public List<string> Pedidas { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Pedidas.Add(request.RequestUri!.ToString());
            using var img = new Image<Rgba32>(40, 40);
            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new ByteArrayContent(ms.ToArray()) });
        }
    }

    /// <summary>
    /// La URL de la imagen la guardó un usuario. Una dirección que no es de nuestro
    /// bucket no se descarga (ni la red interna ni un archivo de varios GB).
    /// </summary>
    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://localhost:5432/")]
    [InlineData("https://externo.test/enorme.png")]
    public async Task RenderAsync_ImagenQueNoEsNuestra_NoSeDescarga(string url)
    {
        var handler = new ContadorHandler();
        var bytes = await NewRenderer(handler).RenderAsync(new ShareCardContent("Producto", "Lámina", null, url), ShareFormat.Square);

        Assert.Empty(handler.Pedidas);
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task RenderAsync_ImagenPropia_SeDescargaUnaVezYLaTarjetaSeGuarda()
    {
        var handler = new ContadorHandler();
        var renderer = NewRenderer(handler);
        var contenido = new ShareCardContent("Producto", "Lámina", "$25", "https://cdn.test/productos/a.webp");

        var primera = await renderer.RenderAsync(contenido, ShareFormat.Square);
        var segunda = await renderer.RenderAsync(contenido, ShareFormat.Square);

        Assert.Single(handler.Pedidas);
        Assert.Same(primera, segunda);
    }

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
