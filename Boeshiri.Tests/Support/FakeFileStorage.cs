using Boeshiri.Application.Abstractions;

namespace Boeshiri.Tests.Support;

/// <summary>
/// Fake de <see cref="IFileStorage"/> que registra los borrados, para poder
/// comprobar que al reemplazar o eliminar no queda basura en el bucket.
/// </summary>
public sealed class FakeFileStorage : IFileStorage
{
    public List<string> Deleted { get; } = [];
    /// <summary>Contenido simulado del bucket, para los tests del gestor.</summary>
    public List<StoredObject> Objects { get; } = [];
    public bool Enabled => true;

    public Task<string> UploadAsync(Stream content, string fileName, string? contentType, string folder, CancellationToken ct = default)
        => Task.FromResult($"https://cdn.test/{folder}/{fileName}");

    public Task DeleteAsync(string publicUrl, CancellationToken ct = default)
    {
        Deleted.Add(publicUrl);
        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(string publicUrl, CancellationToken ct = default)
        => Task.FromResult<Stream?>(Objects.Any(o => o.Url == publicUrl) ? new MemoryStream("contenido"u8.ToArray()) : null);

    public Task<IReadOnlyList<StoredObject>> ListAsync(string? prefix, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StoredObject>>(
            string.IsNullOrEmpty(prefix) ? Objects : Objects.Where(o => o.Key.StartsWith(prefix)).ToList());

    /// <summary>Misma regla que R2FileStorage: archivo directo de la carpeta, sin «..».</summary>
    public bool IsOwnUrl(string? url, string folder)
    {
        var prefijo = $"https://cdn.test/{folder}/";
        if (url is null || !url.StartsWith(prefijo, StringComparison.Ordinal)) return false;
        var resto = url[prefijo.Length..];
        return resto.Length > 0 && !resto.Contains('/') && !resto.Contains("..") && !resto.Contains('?');
    }

    public Task DeleteByKeyAsync(string key, CancellationToken ct = default)
    {
        Deleted.Add(key);
        return Task.CompletedTask;
    }
}
