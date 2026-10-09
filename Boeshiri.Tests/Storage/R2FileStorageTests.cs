using Boeshiri.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Boeshiri.Tests.Storage;

public class R2FileStorageTests
{
    private static R2FileStorage Storage() => new(Options.Create(new R2Options
    {
        AccountId = "cuenta", AccessKeyId = "k", SecretAccessKey = "s", Bucket = "b",
        PublicBaseUrl = "https://pub.r2.dev/",
    }), NullLogger<R2FileStorage>.Instance);

    [Theory]
    [InlineData("https://pub.r2.dev/avatars/abc.webp", "avatars", true)]
    [InlineData("https://pub.r2.dev/documentos/abc.pdf", "avatars", false)]
    [InlineData("https://pub.r2.dev/avatars/../documentos/abc.pdf", "avatars", false)]
    [InlineData("https://pub.r2.dev/avatars/sub/abc.webp", "avatars", false)]
    [InlineData("https://pub.r2.dev/avatars/", "avatars", false)]
    [InlineData("https://otro.test/avatars/abc.webp", "avatars", false)]
    [InlineData("https://pub.r2.dev/avatars/abc.webp", "no-existe", false)]
    [InlineData(null, "avatars", false)]
    public void IsOwnUrl(string? url, string carpeta, bool esperado) =>
        Assert.Equal(esperado, Storage().IsOwnUrl(url, carpeta));
}
