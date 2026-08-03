using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Boeshiri.Infrastructure.Storage;

/// <summary>
/// Cruza los objetos del bucket con las tablas que guardan archivos, para saber
/// de quién es cada uno y si es seguro borrarlo.
/// </summary>
public class FileManagerService(
    BoeshiriDbContext db,
    IFileStorage storage,
    IAuditLogger audit,
    ILogger<FileManagerService> logger) : IFileManagerService
{
    /// <summary>Referencia encontrada en la base para una URL concreta.</summary>
    private record Referencia(string Url, FileUsage Usage, string OwnerType, string OwnerName, Guid OwnerId);

    public async Task<IReadOnlyList<ManagedFileDto>> ListAsync(string? prefix, CancellationToken ct = default)
    {
        var objetos = await storage.ListAsync(prefix, ct);
        var refs = await CargarReferenciasAsync(ct);

        return objetos.Select(o =>
        {
            // El índice se arma por URL porque es lo que guardan las tablas; la key
            // del bucket no aparece en ningún sitio de la base.
            if (refs.TryGetValue(o.Url, out var r))
                return new ManagedFileDto(o.Key, o.Url, o.Size, o.LastModified, r.Usage, r.OwnerType, r.OwnerName, r.OwnerId);

            return new ManagedFileDto(o.Key, o.Url, o.Size, o.LastModified, FileUsage.Orphan, null, null, null);
        }).ToList();
    }

    /// <summary>
    /// Carga TODAS las referencias de una vez y las indexa en memoria. Consultar
    /// por objeto sería una consulta por archivo: con miles, inviable.
    /// </summary>
    private async Task<Dictionary<string, Referencia>> CargarReferenciasAsync(CancellationToken ct)
    {
        var mapa = new Dictionary<string, Referencia>(StringComparer.Ordinal);

        void Añadir(IEnumerable<Referencia> items)
        {
            foreach (var r in items)
                if (!string.IsNullOrWhiteSpace(r.Url))
                    mapa[r.Url] = r;
        }

        // Perfiles: los usuarios no se borran, así que siempre están en uso.
        Añadir(await db.Users
            .Where(u => u.PhotoUrl != null)
            .Select(u => new Referencia(u.PhotoUrl!, FileUsage.InUse, "Perfil", u.FullName, u.Id))
            .ToListAsync(ct));

        Añadir(await db.PublicationImages
            .Select(i => new Referencia(
                i.Url,
                i.Publication.Status == ContentStatus.Deleted ? FileUsage.Trash : FileUsage.InUse,
                "Publicación", i.Publication.Title, i.PublicationId))
            .ToListAsync(ct));

        Añadir(await db.ProductImages
            .Select(i => new Referencia(
                i.Url,
                i.Product.Status == ProductStatus.Deleted ? FileUsage.Trash : FileUsage.InUse,
                "Producto", i.Product.Name, i.ProductId))
            .ToListAsync(ct));

        Añadir(await db.EventImages
            .Select(i => new Referencia(
                i.Url,
                i.Event.Status == ContentStatus.Deleted ? FileUsage.Trash : FileUsage.InUse,
                "Evento", i.Event.Title, i.EventId))
            .ToListAsync(ct));

        // Los documentos se borran de verdad (y su archivo con ellos), así que
        // cualquier fila que exista está en uso.
        Añadir(await db.Documents
            .Select(d => new Referencia(d.FileUrl, FileUsage.InUse, "Documento", d.Name, d.Id))
            .ToListAsync(ct));

        return mapa;
    }

    public async Task DeleteAsync(string key, Guid actorId, CancellationToken ct = default)
    {
        var archivo = (await ListAsync(null, ct)).FirstOrDefault(f => f.Key == key)
            ?? throw AppException.NotFound("El archivo no está en el bucket.");

        if (archivo.Usage == FileUsage.InUse)
            throw AppException.Conflict(
                $"Este archivo está en uso por {archivo.OwnerType?.ToLowerInvariant()} «{archivo.OwnerName}». " +
                "Elimina o edita esa entidad primero; así no queda un enlace roto en el sitio.");

        await BorrarAsync(archivo, actorId, ct);
    }

    public async Task<int> EmptyTrashAsync(Guid actorId, CancellationToken ct = default)
    {
        var papelera = (await ListAsync(null, ct)).Where(f => f.Usage == FileUsage.Trash).ToList();

        foreach (var f in papelera)
            await BorrarAsync(f, actorId, ct);

        logger.LogInformation("Papelera vaciada por {ActorId}: {Total} archivos", actorId, papelera.Count);
        return papelera.Count;
    }

    /// <summary>
    /// Quita la fila de imagen (si la hay) y luego el objeto. En ese orden: si
    /// falla el borrado remoto queda un huérfano, no una referencia rota.
    /// La entidad se conserva con su estado eliminado, para no perder el rastro.
    /// </summary>
    private async Task BorrarAsync(ManagedFileDto archivo, Guid actorId, CancellationToken ct)
    {
        if (archivo.Usage == FileUsage.Trash)
        {
            switch (archivo.OwnerType)
            {
                case "Publicación":
                    db.PublicationImages.RemoveRange(await db.PublicationImages.Where(i => i.Url == archivo.Url).ToListAsync(ct));
                    break;
                case "Producto":
                    db.ProductImages.RemoveRange(await db.ProductImages.Where(i => i.Url == archivo.Url).ToListAsync(ct));
                    break;
                case "Evento":
                    db.EventImages.RemoveRange(await db.EventImages.Where(i => i.Url == archivo.Url).ToListAsync(ct));
                    break;
            }

            audit.Log(actorId, "archivo.papelera_vaciada", archivo.OwnerType ?? "File", archivo.OwnerId?.ToString(), archivo.Key);
        }
        else
        {
            audit.Log(actorId, "archivo.huerfano_borrado", "File", null, archivo.Key);
        }

        await db.SaveChangesAsync(ct);
        await storage.DeleteByKeyAsync(archivo.Key, ct);
    }
}
