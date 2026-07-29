using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Documents;

/// <summary>
/// Biblioteca de documentos (§8). Es solo para miembros (RF-DOC-04). Los docs de
/// nivel Administración exigen <c>documentos.ver_admin</c> (RF-DOC-05); subir a
/// Comunidad exige <c>documentos.subir_comunidad</c>; gestionar la biblioteca de
/// Administración exige <c>documentos.ver_admin</c>. Sin versiones (RF-DOC-01).
/// </summary>
public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> ListAsync(DocumentLibrary? library, string? category, bool canViewAdmin, CancellationToken ct = default);

    Task<DocumentDto> GetAsync(Guid id, bool canViewAdmin, CancellationToken ct = default);

    /// <summary>Sube un documento (directo o como anexo de artículo, RF-DOC-06).</summary>
    Task<Guid> CreateAsync(Guid userId, CreateDocumentRequest request, bool canUploadCommunity, bool canManageAdmin, CancellationToken ct = default);

    /// <summary>Reemplaza el archivo/metadata (sobrescribe, RF-DOC-01). Autor o admin.</summary>
    Task ReplaceAsync(Guid id, Guid userId, ReplaceDocumentRequest request, bool canManageAdmin, CancellationToken ct = default);

    /// <summary>Elimina el documento (RF-DOC-01). Autor o admin.</summary>
    Task DeleteAsync(Guid id, Guid userId, bool canManageAdmin, CancellationToken ct = default);
}
