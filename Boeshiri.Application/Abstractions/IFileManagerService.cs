namespace Boeshiri.Application.Abstractions;

/// <summary>Situación de un archivo del bucket respecto a lo que lo referencia.</summary>
public enum FileUsage
{
    /// <summary>Lo usa algo visible en el sitio. Borrarlo dejaría un enlace roto.</summary>
    InUse,

    /// <summary>Su entidad está eliminada (borrado lógico). Nadie lo ve: es seguro borrarlo.</summary>
    Trash,

    /// <summary>Ninguna fila lo referencia. Sobra de reemplazos antiguos; seguro de borrar.</summary>
    Orphan
}

/// <summary>Archivo del bucket con lo que se sabe de su dueño.</summary>
public record ManagedFileDto(
    string Key,
    string Url,
    long Size,
    DateTime LastModified,
    FileUsage Usage,
    /// <summary>"Perfil", "Publicación", "Producto", "Evento", "Documento" o null si es huérfano.</summary>
    string? OwnerType,
    string? OwnerName,
    Guid? OwnerId);

/// <summary>
/// Gestor de archivos del bucket (solo <c>archivos.gestionar</c>).
///
/// La regla que lo gobierna: un archivo nunca se borra por su cuenta. Se borra
/// junto con la fila que lo referencia, y siempre después de guardar en la base;
/// si el borrado remoto falla queda un huérfano —inofensivo— en vez de una
/// referencia rota, que sí se vería en el sitio.
/// </summary>
public interface IFileManagerService
{
    Task<IReadOnlyList<ManagedFileDto>> ListAsync(string? prefix, CancellationToken ct = default);

    /// <summary>
    /// Borra un archivo seguro (papelera o huérfano) y su fila de imagen si la tiene.
    /// Rechaza los que están en uso: para esos hay que actuar sobre la entidad.
    /// </summary>
    Task DeleteAsync(string key, Guid actorId, CancellationToken ct = default);

    /// <summary>Borra de una vez todo lo que está en la papelera. Devuelve cuántos salieron.</summary>
    Task<int> EmptyTrashAsync(Guid actorId, CancellationToken ct = default);
}
