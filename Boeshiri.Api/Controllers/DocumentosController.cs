using Boeshiri.Api.Authorization;
using Boeshiri.Application.Documents;
using Boeshiri.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Biblioteca de documentos (§8). Solo para miembros (RF-DOC-04). Los documentos
/// de nivel Administración exigen <c>documentos.ver_admin</c> (RF-DOC-05).
/// </summary>
[ApiController]
[Authorize]
[Route("documentos")]
public class DocumentosController(IDocumentService documents) : ControllerBase
{
    private bool CanViewAdmin => User.HasPermission("documentos.ver_admin");

    /// <summary>Lista documentos accesibles, filtrable por biblioteca y categoría (§8).</summary>
    [HasPermission("documentos.ver_comunidad")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List([FromQuery] DocumentLibrary? biblioteca, [FromQuery] string? categoria, CancellationToken ct)
        => Ok(await documents.ListAsync(biblioteca, categoria, CanViewAdmin, ct));

    /// <summary>Metadata y URL de descarga de un documento accesible.</summary>
    [HasPermission("documentos.ver_comunidad")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DocumentDto>> Get(Guid id, CancellationToken ct)
        => Ok(await documents.GetAsync(id, CanViewAdmin, ct));

    /// <summary>Sube un documento a la biblioteca (RF-DOC-01/03/06).</summary>
    [HttpPost]
    public async Task<ActionResult> Create(CreateDocumentRequest request, CancellationToken ct)
    {
        var id = await documents.CreateAsync(
            User.GetUserId(), request,
            canUploadCommunity: User.HasPermission("documentos.subir_comunidad"),
            canManageAdmin: CanViewAdmin, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    /// <summary>Reemplaza el archivo/metadata (sobrescribe, RF-DOC-01).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Replace(Guid id, ReplaceDocumentRequest request, CancellationToken ct)
    {
        await documents.ReplaceAsync(id, User.GetUserId(), request, CanViewAdmin, ct);
        return NoContent();
    }

    /// <summary>Elimina el documento (RF-DOC-01).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await documents.DeleteAsync(id, User.GetUserId(), CanViewAdmin, ct);
        return NoContent();
    }
}
