using System.Linq.Expressions;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Publications;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Publications;

/// <summary>Publicaciones (§6). Reglas por tipo, visibilidad y moderación con auditoría.</summary>
public class PublicationService(BoeshiriDbContext db, IAuditLogger audit) : IPublicationService
{
    public async Task<Guid> CreateAsync(Guid authorId, CreatePublicationRequest request, bool canPublishNews, CancellationToken ct = default)
    {
        if (request.Type == PublicationType.News && !canPublishNews)
            throw AppException.Forbidden("Solo Periodistas y la Junta pueden publicar Noticias.");

        ValidateByType(request);

        var publication = new Publication
        {
            AuthorId = authorId,
            Type = request.Type,
            Title = request.Title.Trim(),
            Body = request.Body,
            ExternalUrl = request.ExternalUrl,
            Visibility = request.Visibility
        };

        var order = 0;
        foreach (var url in request.Images ?? [])
            publication.Images.Add(new PublicationImage { Url = url, Order = order++ });

        foreach (var link in request.Links ?? [])
            publication.Links.Add(new PublicationLink { Title = link.Title, Url = link.Url });

        foreach (var name in NormalizeTags(request.Tags))
            publication.Tags.Add(await GetOrCreateTagAsync(name, ct));

        db.Publications.Add(publication);
        await db.SaveChangesAsync(ct);
        return publication.Id;
    }

    public async Task<IReadOnlyList<PublicationDto>> ListPublicAsync(PublicationType? type, bool includeMembersOnly, CancellationToken ct = default)
    {
        var query = db.Publications.Where(p => p.Status == ContentStatus.Published);
        if (!includeMembersOnly)
            query = query.Where(p => p.Visibility == Visibility.Public);
        if (type is not null)
            query = query.Where(p => p.Type == type);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToSummary)
            .ToListAsync(ct);
    }

    public async Task<PublicationDetailDto> GetDetailAsync(Guid id, bool authenticated, CancellationToken ct = default)
    {
        var p = await db.Publications
            .Include(x => x.Author)
            .Include(x => x.Images)
            .Include(x => x.Links)
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        // Contenido inexistente, oculto o eliminado: mensaje genérico (RF-PUB-20).
        if (p is null || p.Status != ContentStatus.Published)
            throw AppException.NotFound("La publicación no está disponible.");

        // Exclusivo para miembros: el visitante debe iniciar sesión (RF-PUB-19).
        if (p.Visibility == Visibility.Members && !authenticated)
            throw AppException.Unauthorized("Este contenido es exclusivo para miembros. Inicia sesión para verlo.");

        return new PublicationDetailDto(
            p.Id, p.Type, p.Title, p.Body, p.ExternalUrl, p.AuthorId, p.Author.FullName,
            p.Visibility, p.Status, p.CreatedAt, p.EditedAt,
            p.Images.OrderBy(i => i.Order).Select(i => i.Url).ToList(),
            p.Links.Select(l => new LinkInput(l.Title, l.Url)).ToList(),
            p.Tags.Select(t => t.Name).ToList());
    }

    public async Task<IReadOnlyList<PublicationDto>> ListMineAsync(Guid authorId, CancellationToken ct = default)
    {
        return await db.Publications
            .Where(p => p.AuthorId == authorId && p.Status != ContentStatus.Deleted)
            .OrderByDescending(p => p.CreatedAt)
            .Select(ToSummary)
            .ToListAsync(ct);
    }

    public async Task UpdateAsync(Guid id, Guid authorId, UpdatePublicationRequest request, CancellationToken ct = default)
    {
        var p = await db.Publications
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == id && x.AuthorId == authorId, ct)
            ?? throw AppException.NotFound("La publicación no existe o no es tuya.");

        p.Title = request.Title.Trim();
        p.Body = request.Body;
        p.ExternalUrl = request.ExternalUrl;
        p.Visibility = request.Visibility;

        p.Tags.Clear();
        foreach (var name in NormalizeTags(request.Tags))
            p.Tags.Add(await GetOrCreateTagAsync(name, ct));

        p.EditedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(Guid id, StatusAction action, Guid userId, bool canModerate, CancellationToken ct = default)
    {
        var p = await db.Publications.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("La publicación no está disponible.");

        var isOwner = p.AuthorId == userId;

        // Mostrar solo lo puede hacer el autor; ocultar/eliminar, autor o moderador.
        var allowed = action == StatusAction.Show ? isOwner : isOwner || canModerate;
        if (!allowed)
            throw AppException.Forbidden("No tienes permiso para cambiar el estado de esta publicación.");

        p.Status = action switch
        {
            StatusAction.Hide => ContentStatus.Hidden,
            StatusAction.Show => ContentStatus.Published,
            StatusAction.Delete => ContentStatus.Deleted,
            _ => p.Status
        };

        // Auditar solo la moderación de contenido ajeno (RF-ADM-07).
        if (canModerate && !isOwner)
            audit.Log(userId, "publicacion.moderada", "Publication", p.Id.ToString(), action.ToString());

        await db.SaveChangesAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────
    /// <summary>Proyección a resumen. Es una Expression para que EF la traduzca a SQL.</summary>
    private static readonly Expression<Func<Publication, PublicationDto>> ToSummary = p => new PublicationDto(
        p.Id, p.Type, p.Title, p.AuthorId, p.Author.FullName, p.Visibility, p.Status,
        p.CreatedAt, p.EditedAt,
        p.Images.OrderBy(i => i.Order).Select(i => i.Url).FirstOrDefault(),
        p.Tags.Select(t => t.Name).ToList());

    private static void ValidateByType(CreatePublicationRequest r)
    {
        switch (r.Type)
        {
            case PublicationType.Article:
            case PublicationType.News:
                if (string.IsNullOrWhiteSpace(r.Body))
                    throw AppException.BadRequest("El cuerpo es obligatorio para artículos y noticias.");
                if ((r.Images?.Count ?? 0) > 3)
                    throw AppException.BadRequest("Máximo 3 imágenes.");
                if ((r.Links?.Count ?? 0) > 3)
                    throw AppException.BadRequest("Máximo 3 enlaces de referencia.");
                break;
            case PublicationType.Photo:
                if ((r.Images?.Count ?? 0) < 1)
                    throw AppException.BadRequest("Una publicación de tipo Foto requiere al menos una imagen.");
                break;
            case PublicationType.Video:
            case PublicationType.Music:
                if (string.IsNullOrWhiteSpace(r.ExternalUrl))
                    throw AppException.BadRequest("Video y Música requieren un enlace externo.");
                break;
        }
    }

    private static IEnumerable<string> NormalizeTags(IEnumerable<string>? tags) =>
        (tags ?? [])
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private async Task<Tag> GetOrCreateTagAsync(string name, CancellationToken ct)
    {
        // Reutiliza la etiqueta sin distinguir mayúsculas ("Arte" == "arte").
        var lowered = name.ToLowerInvariant();
        var existing = await db.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == lowered, ct);
        if (existing is not null)
            return existing;

        var tag = new Tag { Name = name };
        db.Tags.Add(tag);
        return tag;
    }
}
