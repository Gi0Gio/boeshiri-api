using System.Linq.Expressions;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Documents;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Documents;

/// <summary>Biblioteca de documentos (§8) con control de acceso por nivel y biblioteca.</summary>
public class DocumentService(BoeshiriDbContext db, IAuditLogger audit) : IDocumentService
{
    public async Task<IReadOnlyList<DocumentDto>> ListAsync(DocumentLibrary? library, string? category, bool canViewAdmin, CancellationToken ct = default)
    {
        var query = db.Documents.AsQueryable();

        // Los documentos de nivel Administración solo los ve quien tenga el permiso.
        if (!canViewAdmin)
            query = query.Where(d => d.AccessLevel == DocumentAccessLevel.Members);

        if (library is not null)
            query = query.Where(d => d.Library == library);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(d => d.Category == category);

        return await query.OrderByDescending(d => d.CreatedAt).Select(ToDto).ToListAsync(ct);
    }

    public async Task<DocumentDto> GetAsync(Guid id, bool canViewAdmin, CancellationToken ct = default)
    {
        var doc = await db.Documents.Include(d => d.Author).FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw AppException.NotFound("El documento no existe.");

        if (doc.AccessLevel == DocumentAccessLevel.Administration && !canViewAdmin)
            throw AppException.Forbidden("Este documento es de nivel Administración.");

        return Map(doc);
    }

    public async Task<Guid> CreateAsync(Guid userId, CreateDocumentRequest request, bool canUploadCommunity, bool canManageAdmin, CancellationToken ct = default)
    {
        // Autorización según destino (RF-DOC-03/05).
        var needsAdmin = request.Library == DocumentLibrary.Administration
                         || request.AccessLevel == DocumentAccessLevel.Administration;

        if (needsAdmin)
        {
            if (!canManageAdmin)
                throw AppException.Forbidden("Solo Administración gestiona documentos de ese nivel/biblioteca.");
        }
        else if (!canUploadCommunity)
        {
            throw AppException.Forbidden("No tienes permiso para subir a la biblioteca Comunidad.");
        }

        var doc = new Document
        {
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            Library = request.Library,
            AccessLevel = request.AccessLevel,
            FileUrl = request.FileUrl,
            FileName = request.FileName,
            ContentType = request.ContentType,
            SizeBytes = request.SizeBytes,
            AuthorId = userId,
            PublicationId = request.PublicationId
        };

        db.Documents.Add(doc);
        audit.Log(userId, "documento.subido", "Document", doc.Id.ToString(), $"{doc.Library}/{doc.AccessLevel}");
        await db.SaveChangesAsync(ct);
        return doc.Id;
    }

    public async Task ReplaceAsync(Guid id, Guid userId, ReplaceDocumentRequest request, bool canManageAdmin, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw AppException.NotFound("El documento no existe.");

        EnsureCanManage(doc, userId, canManageAdmin);

        // Sobrescribe: no se conservan versiones anteriores (RF-DOC-01).
        doc.Name = request.Name.Trim();
        doc.Category = request.Category.Trim();
        doc.FileUrl = request.FileUrl;
        doc.FileName = request.FileName;
        doc.ContentType = request.ContentType;
        doc.SizeBytes = request.SizeBytes;
        doc.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid userId, bool canManageAdmin, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw AppException.NotFound("El documento no existe.");

        EnsureCanManage(doc, userId, canManageAdmin);

        db.Documents.Remove(doc);
        audit.Log(userId, "documento.eliminado", "Document", doc.Id.ToString(), doc.Name);
        await db.SaveChangesAsync(ct);
    }

    private static void EnsureCanManage(Document doc, Guid userId, bool canManageAdmin)
    {
        if (doc.AuthorId != userId && !canManageAdmin)
            throw AppException.Forbidden("Solo el autor o Administración pueden gestionar este documento.");
    }

    private static DocumentDto Map(Document d) => new(
        d.Id, d.Name, d.Category, d.Library, d.AccessLevel, d.FileUrl, d.FileName, d.ContentType, d.SizeBytes,
        d.AuthorId, d.Author.FullName, d.PublicationId, d.CreatedAt, d.UpdatedAt);

    private static readonly Expression<Func<Document, DocumentDto>> ToDto = d => new DocumentDto(
        d.Id, d.Name, d.Category, d.Library, d.AccessLevel, d.FileUrl, d.FileName, d.ContentType, d.SizeBytes,
        d.AuthorId, d.Author.FullName, d.PublicationId, d.CreatedAt, d.UpdatedAt);
}
