using Boeshiri.Api.Authorization;
using Boeshiri.Application.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Gestión de postulantes por la Junta / Recursos Humanos. Todos los endpoints
/// exigen el permiso <c>postulantes.decidir</c> (RF-PUB-15, RF-ADM-02).
/// </summary>
[ApiController]
[Route("admin/postulantes")]
[HasPermission("postulantes.decidir")]
public class PostulantesController(IPostulantesService postulantes) : ControllerBase
{
    /// <summary>Lista los postulantes pendientes de decisión.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PostulanteDto>>> List(CancellationToken ct)
        => Ok(await postulantes.ListPendingAsync(ct));

    /// <summary>
    /// Emite un enlace de verificación para entregarlo a mano (WhatsApp) cuando el
    /// correo no llega. Anula los anteriores y queda en auditoría.
    /// </summary>
    [HttpPost("{id:guid}/enlace-verificacion")]
    public async Task<ActionResult<VerificationLinkDto>> IssueVerificationLink(Guid id, CancellationToken ct)
        => Ok(await postulantes.IssueVerificationLinkAsync(id, User.GetUserId(), ct));

    /// <summary>Acepta o rechaza un postulante.</summary>
    [HttpPost("{id:guid}/decidir")]
    public async Task<IActionResult> Decide(Guid id, DecisionRequest request, CancellationToken ct)
    {
        var decidedBy = Guid.TryParse(User.FindFirst("sub")?.Value, out var uid) ? uid : Guid.Empty;
        await postulantes.DecideAsync(id, request, decidedBy, ct);
        var resultado = request.Decision == DecisionType.Aceptar ? "aceptado" : "rechazado";
        return Ok(new { mensaje = $"Postulante {resultado}." });
    }
}
