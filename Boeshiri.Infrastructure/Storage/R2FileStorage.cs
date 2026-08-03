using Amazon.S3;
using Amazon.S3.Model;
using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boeshiri.Infrastructure.Storage;

/// <summary>
/// Almacenamiento en Cloudflare R2 vía la API S3-compatible. La subida pasa por
/// el servidor (PutObject); la lectura es por la URL pública del bucket.
/// </summary>
public sealed class R2FileStorage : IFileStorage, IDisposable
{
    private static readonly HashSet<string> AllowedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "avatars", "publicaciones", "documentos", "productos", "misc"
    };

    private readonly R2Options _o;
    private readonly AmazonS3Client _client;
    private readonly ILogger<R2FileStorage> _logger;

    public R2FileStorage(IOptions<R2Options> options, ILogger<R2FileStorage> logger)
    {
        _o = options.Value;
        _logger = logger;
        var config = new AmazonS3Config
        {
            ServiceURL = $"https://{_o.AccountId}.r2.cloudflarestorage.com",
            ForcePathStyle = true,
            // R2 usa la región "auto" para la firma SigV4.
            AuthenticationRegion = "auto",
        };
        _client = new AmazonS3Client(_o.AccessKeyId, _o.SecretAccessKey, config);
    }

    public bool Enabled => true;

    public async Task<string> UploadAsync(Stream content, string fileName, string? contentType, string folder, CancellationToken ct = default)
    {
        var safeFolder = AllowedFolders.Contains(folder) ? folder.ToLowerInvariant() : "misc";
        var ext = Path.GetExtension(fileName);
        if (ext.Length > 10) ext = ""; // extensión sospechosa
        var key = $"{safeFolder}/{Guid.CreateVersion7():n}{ext}";

        var request = new PutObjectRequest
        {
            BucketName = _o.Bucket,
            Key = key,
            InputStream = content,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            DisablePayloadSigning = true, // requerido por R2 en subidas por stream
        };

        try
        {
            await _client.PutObjectAsync(request, ct);
        }
        catch (AmazonS3Exception ex)
        {
            // El detalle de R2 (endpoint, bucket, motivo de credenciales) va al log,
            // nunca al cliente: RF-PUB-20. Y es 502, no 400: la petición era válida,
            // quien falló fue el almacenamiento.
            _logger.LogError(ex, "Fallo al subir {Key} a R2 (bucket {Bucket})", key, _o.Bucket);
            throw AppException.Upstream("No se pudo guardar el archivo. Inténtalo de nuevo en un momento.");
        }

        return $"{_o.PublicBaseUrl.TrimEnd('/')}/{key}";
    }

    public Task DeleteAsync(string publicUrl, CancellationToken ct = default)
    {
        var prefix = _o.PublicBaseUrl.TrimEnd('/') + "/";
        if (string.IsNullOrWhiteSpace(publicUrl) || !publicUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask; // no es un objeto nuestro; nada que borrar

        return DeleteByKeyAsync(publicUrl[prefix.Length..], ct);
    }

    public async Task<IReadOnlyList<StoredObject>> ListAsync(string? prefix, CancellationToken ct = default)
    {
        var request = new ListObjectsV2Request { BucketName = _o.Bucket, Prefix = prefix, MaxKeys = 1000 };
        var baseUrl = _o.PublicBaseUrl.TrimEnd('/');
        var result = new List<StoredObject>();
        ListObjectsV2Response response;
        do
        {
            response = await _client.ListObjectsV2Async(request, ct);
            foreach (var o in response.S3Objects ?? [])
                result.Add(new StoredObject(o.Key, $"{baseUrl}/{o.Key}", o.Size ?? 0, o.LastModified?.ToUniversalTime() ?? default));
            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated == true && result.Count < 5000);

        return result.OrderByDescending(o => o.LastModified).ToList();
    }

    public async Task DeleteByKeyAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        try
        {
            await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _o.Bucket, Key = key }, ct);
        }
        catch (AmazonS3Exception ex)
        {
            // Best-effort para no interrumpir el flujo, pero NO en silencio: si las
            // credenciales caducan, cada borrado falla y los huérfanos se acumulan
            // sin que nadie se entere hasta ver la factura.
            _logger.LogWarning(ex, "No se pudo borrar {Key} de R2 (bucket {Bucket})", key, _o.Bucket);
        }
    }

    public void Dispose() => _client.Dispose();
}

/// <summary>Implementación de reserva cuando R2 no está configurado.</summary>
public sealed class DisabledFileStorage : IFileStorage
{
    public bool Enabled => false;

    public Task<string> UploadAsync(Stream content, string fileName, string? contentType, string folder, CancellationToken ct = default)
        => throw AppException.BadRequest("El almacenamiento de archivos no está configurado todavía.");

    public Task DeleteAsync(string publicUrl, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<StoredObject>> ListAsync(string? prefix, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StoredObject>>([]);

    public Task DeleteByKeyAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
}
