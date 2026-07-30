namespace Boeshiri.Application.Abstractions;

/// <summary>
/// Almacenamiento de archivos (imágenes de perfil/publicaciones, documentos).
/// En producción lo implementa Cloudflare R2 (S3-compatible); si no está
/// configurado, una implementación deshabilitada devuelve un error claro.
/// El archivo se sube a través del API (sin CORS ni llaves en el navegador).
/// </summary>
public interface IFileStorage
{
    /// <summary>¿Hay almacenamiento configurado y operativo?</summary>
    bool Enabled { get; }

    /// <summary>Sube el contenido y devuelve la URL pública final.</summary>
    Task<string> UploadAsync(Stream content, string fileName, string? contentType, string folder, CancellationToken ct = default);

    /// <summary>Borra un objeto por su URL pública (best-effort; no falla si no existe).</summary>
    Task DeleteAsync(string publicUrl, CancellationToken ct = default);

    /// <summary>Lista objetos del bucket (gestor del super admin), opcionalmente por prefijo/carpeta.</summary>
    Task<IReadOnlyList<StoredObject>> ListAsync(string? prefix, CancellationToken ct = default);

    /// <summary>Borra un objeto por su key exacta (gestor del super admin).</summary>
    Task DeleteByKeyAsync(string key, CancellationToken ct = default);
}

/// <summary>Objeto almacenado, para el gestor de archivos.</summary>
public record StoredObject(string Key, string Url, long Size, DateTime LastModified);
