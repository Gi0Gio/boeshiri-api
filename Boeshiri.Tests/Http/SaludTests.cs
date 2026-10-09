using System.Net;
using System.Net.Http.Json;
using Boeshiri.Tests.Support;

namespace Boeshiri.Tests.Http;

/// <summary>La sonda de salud mira también la base.</summary>
public class SaludTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Health_ConBase_RespondeOkConDb()
    {
        var r = await api.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var cuerpo = await r.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ok", cuerpo!["db"]);
    }
}
