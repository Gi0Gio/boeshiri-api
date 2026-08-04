using Boeshiri.Api.Authorization;
using Boeshiri.Application.Shouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Gritos: llamados abiertos entre miembros, la pieza roja del Mural de Explorar.
/// Todo exige sesión salvo <see cref="Summary"/>, que solo devuelve cuántos hay.
/// </summary>
[ApiController]
[Route("gritos")]
[Authorize]
public class GritosController(IShoutService shouts) : ControllerBase
{
    /// <summary>
    /// Cuántos gritos hay abiertos. Lo único anónimo: alimenta las piezas veladas
    /// del mural sin que salga del servidor ningún dato de un miembro.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("resumen")]
    public async Task<ActionResult<ShoutTeaserDto>> Summary(CancellationToken ct)
        => Ok(new ShoutTeaserDto(await shouts.CountOpenAsync(ct)));

    /// <summary>Gritos vivos, del que ocurre primero al que ocurre después.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShoutSummaryDto>>> List(CancellationToken ct)
        => Ok(await shouts.ListOpenAsync(User.GetUserId(), ct));

    /// <summary>Los propios, en cualquier estado.</summary>
    [HttpGet("mios")]
    public async Task<ActionResult<IReadOnlyList<ShoutSummaryDto>>> Mine(CancellationToken ct)
        => Ok(await shouts.ListMineAsync(User.GetUserId(), ct));

    /// <summary>Detalle, con quiénes van.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShoutDetailDto>> Detail(Guid id, CancellationToken ct)
        => Ok(await shouts.GetDetailAsync(id, User.GetUserId(), ct));

    /// <summary>Echa un grito.</summary>
    [HasPermission("gritos.publicar")]
    [HttpPost]
    public async Task<ActionResult> Create(CreateShoutRequest request, CancellationToken ct)
    {
        var id = await shouts.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Detail), new { id }, new { id });
    }

    /// <summary>Edita un grito propio, mientras siga abierto.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateShoutRequest request, CancellationToken ct)
    {
        await shouts.UpdateAsync(id, User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Toma una plaza.</summary>
    [HttpPost("{id:guid}/apuntarme")]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct)
    {
        await shouts.JoinAsync(id, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Suelta la plaza.</summary>
    [HttpDelete("{id:guid}/apuntarme")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        await shouts.LeaveAsync(id, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Cerrar / cancelar (autor) o eliminar (autor o <c>gritos.moderar</c>).</summary>
    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeShoutStatusRequest request, CancellationToken ct)
    {
        var canModerate = User.HasPermission("gritos.moderar");
        await shouts.ChangeStatusAsync(id, request.Action, User.GetUserId(), canModerate, ct);
        return NoContent();
    }
}
