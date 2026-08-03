using Boeshiri.Api.Authorization;
using Boeshiri.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Subida de archivos a través del servidor (imágenes de perfil/publicaciones,
/// documentos). Devuelve la URL pública ya lista para guardar. Requiere sesión.
/// </summary>
[ApiController]
[Authorize]
[Route("archivos")]
public class ArchivosController(IFileStorage storage, IUploadProcessor processor, IFileManagerService manager) : ControllerBase
{
    // Tope de transporte: corta la petición antes de leerla entera. Los límites
    // reales por tipo (5 MB imagen, 10 MB PDF) los aplica el IUploadProcessor.
    private const long MaxBytes = 10 * 1024 * 1024;

    /// <summary>Sube un archivo y devuelve su URL pública.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    public async Task<ActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? folder, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "No se recibió ningún archivo." });

        await using var stream = file.OpenReadStream();

        // Se valida y normaliza ANTES de tocar el bucket: lo que no cumple la política
        // no llega a ocupar espacio. El Content-Type del cliente se ignora a propósito.
        using var listo = await processor.ProcessAsync(stream, file.FileName, folder ?? "misc", ct);

        var url = await storage.UploadAsync(listo.Content, listo.FileName, listo.ContentType, folder ?? "misc", ct);
        return Ok(new { url });
    }

    /// <summary>
    /// Gestor de archivos: lista los objetos con su dueño y si es seguro borrarlos.
    /// </summary>
    [HasPermission("archivos.gestionar")]
    [HttpGet("gestor")]
    public async Task<ActionResult> List([FromQuery] string? prefix, CancellationToken ct)
    {
        var objetos = await manager.ListAsync(string.IsNullOrWhiteSpace(prefix) ? null : prefix, ct);
        return Ok(new { enabled = storage.Enabled, objects = objetos });
    }

    /// <summary>
    /// Borra un archivo. Solo si está en la papelera o huérfano: los que están en
    /// uso se rechazan con 409 para no dejar enlaces rotos en el sitio.
    /// </summary>
    [HasPermission("archivos.gestionar")]
    [HttpDelete("gestor")]
    public async Task<IActionResult> DeleteObject([FromQuery] string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { detail = "Falta la key del objeto." });

        await manager.DeleteAsync(key, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Vacía la papelera: los archivos de entidades ya eliminadas.</summary>
    [HasPermission("archivos.gestionar")]
    [HttpPost("gestor/vaciar-papelera")]
    public async Task<ActionResult> EmptyTrash(CancellationToken ct)
    {
        var total = await manager.EmptyTrashAsync(User.GetUserId(), ct);
        return Ok(new { total, mensaje = total == 0 ? "La papelera ya estaba vacía." : $"Se liberaron {total} archivos." });
    }
}
