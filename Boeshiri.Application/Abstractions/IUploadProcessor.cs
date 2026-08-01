namespace Boeshiri.Application.Abstractions;

/// <summary>Archivo ya validado y normalizado, listo para subir al almacenamiento.</summary>
public sealed record ProcessedUpload(Stream Content, string FileName, string ContentType) : IDisposable
{
    public void Dispose() => Content.Dispose();
}

/// <summary>
/// Valida y normaliza lo que se sube ANTES de que llegue al bucket (RF-IMG-01).
///
/// Es la única barrera real: la compresión del navegador se salta con un POST
/// directo al endpoint, así que el tipo, el tamaño y el formato se comprueban aquí
/// mirando el CONTENIDO del archivo, nunca el Content-Type que declara el cliente.
/// </summary>
public interface IUploadProcessor
{
    /// <summary>
    /// Devuelve el archivo listo para almacenar: las imágenes se reencodan a WebP y
    /// se redimensionan; los PDF pasan tal cual. Lanza <c>AppException</c> si el
    /// archivo no cumple la política de la carpeta.
    /// </summary>
    Task<ProcessedUpload> ProcessAsync(Stream input, string fileName, string folder, CancellationToken ct = default);
}
