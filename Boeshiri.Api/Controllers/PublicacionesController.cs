using Boeshiri.Api.Authorization;
using Boeshiri.Application.Publications;
using Boeshiri.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Publicaciones (§6). Lectura pública con reglas de visibilidad; creación y
/// gestión por el autor; moderación con el permiso <c>publicaciones.moderar</c>.
/// </summary>
[ApiController]
[Route("publicaciones")]
public class PublicacionesController(IPublicationService publications) : ControllerBase
{
    /// <summary>Listado público (RF-PUB-06/07). Autenticado incluye las exclusivas.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<PublicationDto>>> List([FromQuery] PublicationType? tipo, CancellationToken ct)
    {
        var authenticated = User.Identity?.IsAuthenticated ?? false;
        return Ok(await publications.ListPublicAsync(tipo, includeMembersOnly: authenticated, ct));
    }

    /// <summary>Detalle con reglas de visibilidad (RF-PUB-18/19/20).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicationDetailDto>> Detail(Guid id, CancellationToken ct)
    {
        var authenticated = User.Identity?.IsAuthenticated ?? false;
        return Ok(await publications.GetDetailAsync(id, authenticated, ct));
    }

    /// <summary>Publicaciones propias (RF-MEM-11).</summary>
    [Authorize]
    [HttpGet("mias")]
    public async Task<ActionResult<IReadOnlyList<PublicationDto>>> Mine(CancellationToken ct)
        => Ok(await publications.ListMineAsync(User.GetUserId(), ct));

    /// <summary>Cola de moderación: todas las publicaciones vivas (RF-ADM-07).</summary>
    [HasPermission("publicaciones.moderar")]
    [HttpGet("moderacion")]
    public async Task<ActionResult<IReadOnlyList<PublicationDto>>> Moderation(CancellationToken ct)
        => Ok(await publications.ListForModerationAsync(ct));

    /// <summary>Crea una publicación (RF-MEM-14). Noticia exige noticias.publicar.</summary>
    [HasPermission("publicaciones.crear")]
    [HttpPost]
    public async Task<ActionResult> Create(CreatePublicationRequest request, CancellationToken ct)
    {
        var canPublishNews = User.HasPermission("noticias.publicar");
        var id = await publications.CreateAsync(User.GetUserId(), request, canPublishNews, ct);
        return CreatedAtAction(nameof(Detail), new { id }, new { id });
    }

    /// <summary>Edita una publicación propia (RF-MEM-12).</summary>
    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdatePublicationRequest request, CancellationToken ct)
    {
        await publications.UpdateAsync(id, User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Oculta/muestra/elimina. Propia (RF-MEM-12) o moderación (RF-ADM-07).</summary>
    [Authorize]
    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeStatusRequest request, CancellationToken ct)
    {
        var canModerate = User.HasPermission("publicaciones.moderar");
        await publications.ChangeStatusAsync(id, request.Action, User.GetUserId(), canModerate, ct);
        return NoContent();
    }
}
