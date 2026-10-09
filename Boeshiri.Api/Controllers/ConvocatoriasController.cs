using Boeshiri.Api.Authorization;
using Boeshiri.Application.OpenCalls;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Convocatorias en el sitio público: se ven y se responden sin cuenta. Si quien
/// responde trae sesión, su nombre y contacto salen del perfil.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("convocatorias")]
public class ConvocatoriasController(IOpenCallService calls) : ControllerBase
{
    /// <summary>Convocatorias abiertas ahora mismo.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PublicOpenCallDto>>> Open(CancellationToken ct)
        => Ok(await calls.ListOpenAsync(ct));

    /// <summary>Una convocatoria abierta o cerrada (un borrador da 404).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicOpenCallDto>> Detail(Guid id, CancellationToken ct)
        => Ok(await calls.GetPublicAsync(id, ct));

    /// <summary>Responder. Anónimo, con límite por IP y una trampa para robots.</summary>
    [EnableRateLimiting(Limites.Convocatorias)]
    [HttpPost("{id:guid}/respuestas")]
    public async Task<IActionResult> Submit(Guid id, SubmitResponseRequest request, CancellationToken ct)
    {
        Guid? userId = User.Identity?.IsAuthenticated == true ? User.GetUserId() : null;
        await calls.SubmitAsync(id, userId, request, ct);
        return Ok(new { mensaje = "¡Recibimos tu respuesta! La Junta la revisará y te contactará." });
    }
}

/// <summary>Convocatorias para la Junta: armarlas, abrirlas, cerrarlas y evaluar respuestas.</summary>
[ApiController]
[HasPermission(Permisos.ConvocatoriasGestionar)]
[Route("admin/convocatorias")]
public class ConvocatoriasAdminController(IOpenCallService calls) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OpenCallSummaryDto>>> List(CancellationToken ct)
        => Ok(await calls.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OpenCallAdminDto>> Detail(Guid id, CancellationToken ct)
        => Ok(await calls.GetAdminAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult> Create(SaveOpenCallRequest request, CancellationToken ct)
    {
        var id = await calls.CreateAsync(User.GetUserId(), request, ct);
        return Created($"/admin/convocatorias/{id}", new { id });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveOpenCallRequest request, CancellationToken ct)
    {
        await calls.UpdateAsync(id, User.GetUserId(), request, ct);
        return NoContent();
    }

    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeOpenCallStatusRequest request, CancellationToken ct)
    {
        await calls.ChangeStatusAsync(id, User.GetUserId(), request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await calls.DeleteAsync(id, User.GetUserId(), ct);
        return NoContent();
    }

    [HttpPatch("respuestas/{responseId:guid}")]
    public async Task<IActionResult> Review(Guid responseId, ReviewResponseRequest request, CancellationToken ct)
    {
        await calls.ReviewAsync(responseId, User.GetUserId(), request, ct);
        return NoContent();
    }
}
