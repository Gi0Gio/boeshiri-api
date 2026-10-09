using System.Net;
using System.Net.Http.Json;
using Boeshiri.Tests.Support;

namespace Boeshiri.Tests.Http;

/// <summary>Las rutas anónimas tienen tope por IP.</summary>
public class LimitesTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;
    public LimitesTests(ApiFactory api) => _api = api;

    /// <summary>Cliente con su propia IP (la que deja el proxy de Netlify).</summary>
    private HttpClient ClienteDesde(string ip)
    {
        var c = _api.CreateClient();
        c.DefaultRequestHeaders.Add("x-nf-client-connection-ip", ip);
        // Lo que añade el borde de Railway en las rutas que llegan directo.
        c.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return c;
    }

    [Fact]
    public async Task Contacto_CuartoMensajeSeguido_Responde429()
    {
        var c = ClienteDesde("203.0.113.10");
        var msg = new { name = "Ana", email = "ana@ex.com", message = "Hola" };

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/contacto", msg)).StatusCode);

        var r = await c.PostAsJsonAsync("/contacto", msg);
        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        Assert.Contains("Espera", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Contacto_OtraIp_TieneSuPropioCupo()
    {
        var msg = new { name = "Ana", email = "ana@ex.com", message = "Hola" };
        var a = ClienteDesde("203.0.113.20");
        for (var i = 0; i < 3; i++) await a.PostAsJsonAsync("/contacto", msg);

        var b = ClienteDesde("203.0.113.21");
        Assert.Equal(HttpStatusCode.OK, (await b.PostAsJsonAsync("/contacto", msg)).StatusCode);
    }

    [Fact]
    public async Task Login_UndecimoIntentoEnUnMinuto_Responde429()
    {
        var c = ClienteDesde("203.0.113.30");
        HttpStatusCode ultimo = default;
        for (var i = 0; i < 11; i++)
        {
            var r = await c.PostAsJsonAsync("/auth/login", new { email = $"x{i}@ex.com", password = "x" });
            ultimo = r.StatusCode;
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, ultimo);
    }

    [Fact]
    public async Task FueraDeLasRutasDeNetlify_LaCabeceraNoAbreCupoNuevo()
    {
        // Contacto no pasa por Netlify: inventar una IP distinta en cada intento no
        // tiene que servir para saltarse el límite.
        var msg = new { name = "Ana", email = "ana@ex.com", message = "Hola" };
        HttpStatusCode ultimo = HttpStatusCode.OK;
        for (var i = 0; i < 4; i++)
        {
            var c = _api.CreateClient();
            c.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.99");
            c.DefaultRequestHeaders.Add("x-nf-client-connection-ip", $"203.0.113.{100 + i}");
            ultimo = (await c.PostAsJsonAsync("/contacto", msg)).StatusCode;
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, ultimo);
    }
}
