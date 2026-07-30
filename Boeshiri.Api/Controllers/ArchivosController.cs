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
public class ArchivosController(IFileStorage storage) : ControllerBase
{
    private const long MaxBytes = 20 * 1024 * 1024; // 20 MB

    /// <summary>Sube un archivo y devuelve su URL pública.</summary>
    [HttpPost]
    [RequestSizeLimit(MaxBytes)]
    public async Task<ActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? folder, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "No se recibió ningún archivo." });

        if (file.Length > MaxBytes)
            return BadRequest(new { detail = "El archivo supera el límite de 20 MB." });

        await using var stream = file.OpenReadStream();
        var url = await storage.UploadAsync(stream, file.FileName, file.ContentType, folder ?? "misc", ct);
        return Ok(new { url });
    }

    /// <summary>Gestor de archivos: lista objetos del bucket. Solo super admin.</summary>
    [HasPermission("archivos.gestionar")]
    [HttpGet("gestor")]
    public async Task<ActionResult> List([FromQuery] string? prefix, CancellationToken ct)
    {
        var objetos = await storage.ListAsync(string.IsNullOrWhiteSpace(prefix) ? null : prefix, ct);
        return Ok(new { enabled = storage.Enabled, objects = objetos });
    }

    /// <summary>Gestor de archivos: elimina un objeto del bucket por su key. Solo super admin.</summary>
    [HasPermission("archivos.gestionar")]
    [HttpDelete("gestor")]
    public async Task<IActionResult> DeleteObject([FromQuery] string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest(new { detail = "Falta la key del objeto." });

        await storage.DeleteByKeyAsync(key, ct);
        return NoContent();
    }
}
