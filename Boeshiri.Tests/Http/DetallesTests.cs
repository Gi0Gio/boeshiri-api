using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Boeshiri.Tests.Support;

namespace Boeshiri.Tests.Http;

/// <summary>Detalles de la API que se ven desde fuera.</summary>
public class DetallesTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private HttpClient Cliente(string ip)
    {
        var c = api.CreateClient();
        c.DefaultRequestHeaders.Add("x-nf-client-connection-ip", ip);
        return c;
    }

    [Fact]
    public async Task Validacion_MensajesEnEspanol()
    {
        var r = await Cliente("198.51.100.1").PostAsJsonAsync("/contacto", new { name = "", email = "no-es-correo", message = "" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);

        var json = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("field is required", json);
        Assert.DoesNotContain("e-mail address", json);
        Assert.Contains("Es obligatorio.", json);
        Assert.Contains("No es un correo válido.", json);
    }

    [Fact]
    public async Task Validacion_JsonMalFormado_EnEspanol()
    {
        var c = Cliente("198.51.100.2");
        var r = await c.PostAsync("/auth/login", new StringContent("{\"email\": 5,", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var json = await r.Content.ReadAsStringAsync();
        Assert.DoesNotContain("could not be converted", json);
        Assert.Contains("El formato no es válido.", json);
    }

    [Fact]
    public async Task Contacto_ResponderVaAQuienEscribio()
    {
        var r = await Cliente("198.51.100.3").PostAsJsonAsync("/contacto", new { name = "Ana", email = "ana@ex.com", message = "Hola" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("ana@ex.com", api.Correos.ReplyTo);
    }
}
