using Boeshiri.Api.Authorization;
using Boeshiri.Application.Transparency;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Panel de transparencia (§10.7). Los miembros leen los artículos publicados;
/// gestionarlos exige <c>transparencia.gestionar</c> (RF-TRA-01). Al publicar se
/// notifica a todos los miembros (RF-TRA-02).
/// </summary>
[ApiController]
[Authorize]
[Route("transparencia")]
public class TransparenciaController(ITransparencyService transparency) : ControllerBase
{
    private bool CanManage => User.HasPermission("transparencia.gestionar");

    /// <summary>Lista los artículos publicados (gestores ven también los ocultos).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransparencySummaryDto>>> List([FromQuery] bool incluirOcultos, CancellationToken ct)
        => Ok(await transparency.ListAsync(includeHidden: incluirOcultos && CanManage, ct));

    /// <summary>Detalle de un artículo publicado.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransparencyArticleDto>> Detail(Guid id, CancellationToken ct)
        => Ok(await transparency.GetDetailAsync(id, ct));

    /// <summary>Publica un artículo oficial y notifica a los miembros (RF-TRA-01/02).</summary>
    [HasPermission("transparencia.gestionar")]
    [HttpPost]
    public async Task<ActionResult> Create(CreateTransparencyRequest request, CancellationToken ct)
    {
        var id = await transparency.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Detail), new { id }, new { id });
    }

    /// <summary>Edita un artículo oficial.</summary>
    [HasPermission("transparencia.gestionar")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTransparencyRequest request, CancellationToken ct)
    {
        await transparency.UpdateAsync(id, User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Oculta / muestra / elimina un artículo oficial.</summary>
    [HasPermission("transparencia.gestionar")]
    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeTransparencyStatusRequest request, CancellationToken ct)
    {
        await transparency.ChangeStatusAsync(id, request.Action, User.GetUserId(), ct);
        return NoContent();
    }
}
