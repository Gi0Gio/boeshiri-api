using System.Linq.Expressions;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Notifications;
using Boeshiri.Application.Transparency;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Transparency;

/// <summary>Panel de transparencia (§10.7). Al publicar hace fan-out de avisos a los miembros (RF-TRA-02).</summary>
public class TransparencyService(
    BoeshiriDbContext db,
    INotificationService notifications,
    IAuditLogger audit) : ITransparencyService
{
    public async Task<IReadOnlyList<TransparencySummaryDto>> ListAsync(bool includeHidden, CancellationToken ct = default)
    {
        var query = includeHidden
            ? db.TransparencyArticles.Where(a => a.Status != ContentStatus.Deleted)
            : db.TransparencyArticles.Where(a => a.Status == ContentStatus.Published);

        return await query.OrderByDescending(a => a.CreatedAt).Select(ToSummary).ToListAsync(ct);
    }

    public async Task<TransparencyArticleDto> GetDetailAsync(Guid id, CancellationToken ct = default)
    {
        var a = await db.TransparencyArticles.Include(x => x.Author).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null || a.Status != ContentStatus.Published)
            throw AppException.NotFound("El artículo no está disponible.");

        return new TransparencyArticleDto(
            a.Id, a.Title, a.Body, a.Category, a.Status, a.AuthorId, a.Author.FullName, a.CreatedAt, a.UpdatedAt);
    }

    public async Task<Guid> CreateAsync(Guid userId, CreateTransparencyRequest request, CancellationToken ct = default)
    {
        var article = new TransparencyArticle
        {
            Title = request.Title.Trim(),
            Body = request.Body,
            Category = request.Category.Trim(),
            AuthorId = userId
        };
        db.TransparencyArticles.Add(article);

        // Fan-out: aviso in-app a cada miembro activo (RF-TRA-02), menos al autor.
        var memberIds = await db.Users
            .Where(u => u.Status == MemberStatus.Active && u.Id != userId)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var memberId in memberIds)
            notifications.Notify(memberId, "transparencia.publicada", $"Nuevo comunicado de la Junta: {article.Title}");

        audit.Log(userId, "transparencia.publicada", "TransparencyArticle", article.Id.ToString(), article.Title);
        await db.SaveChangesAsync(ct);
        return article.Id;
    }

    public async Task UpdateAsync(Guid id, Guid userId, UpdateTransparencyRequest request, CancellationToken ct = default)
    {
        var a = await db.TransparencyArticles.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El artículo no existe.");

        a.Title = request.Title.Trim();
        a.Body = request.Body;
        a.Category = request.Category.Trim();
        a.UpdatedAt = DateTime.UtcNow;

        audit.Log(userId, "transparencia.editada", "TransparencyArticle", a.Id.ToString(), a.Title);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(Guid id, TransparencyStatusAction action, Guid userId, CancellationToken ct = default)
    {
        var a = await db.TransparencyArticles.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El artículo no existe.");

        a.Status = action switch
        {
            TransparencyStatusAction.Hide => ContentStatus.Hidden,
            TransparencyStatusAction.Show => ContentStatus.Published,
            TransparencyStatusAction.Delete => ContentStatus.Deleted,
            _ => a.Status
        };

        audit.Log(userId, "transparencia.moderada", "TransparencyArticle", a.Id.ToString(), action.ToString());
        await db.SaveChangesAsync(ct);
    }

    private static readonly Expression<Func<TransparencyArticle, TransparencySummaryDto>> ToSummary = a =>
        new TransparencySummaryDto(a.Id, a.Title, a.Category, a.Status, a.Author.FullName, a.CreatedAt);
}
