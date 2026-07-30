using Boeshiri.Api.Authorization;
using Boeshiri.Application.Groups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Comisiones, equipos y membresías (§7). La gestión (aprobar ingresos, crear
/// equipos) usa permisos contextuales: el coordinador de esa comisión o quien
/// tenga <c>comisiones.ver_todas</c> (ADR-0005).
/// </summary>
[ApiController]
[Authorize]
[Route("grupos")]
public class GruposController(IGroupService groups) : ControllerBase
{
    private bool CanManageGlobally => User.HasPermission("comisiones.ver_todas");

    /// <summary>Lista las comisiones.</summary>
    [HttpGet("comisiones")]
    public async Task<ActionResult<IReadOnlyList<CommissionDto>>> Commissions(CancellationToken ct)
        => Ok(await groups.ListCommissionsAsync(ct));

    /// <summary>Detalle de una comisión: integrantes y equipos (RF-GRP-02).</summary>
    [HttpGet("comisiones/{id:guid}")]
    public async Task<ActionResult<CommissionDetailDto>> CommissionDetail(Guid id, CancellationToken ct)
        => Ok(await groups.GetCommissionDetailAsync(id, ct));

    /// <summary>Crea una comisión (RF-GRP-01). Requiere gestión global.</summary>
    [HasPermission("comisiones.ver_todas")]
    [HttpPost("comisiones")]
    public async Task<ActionResult> CreateCommission(CreateCommissionRequest request, CancellationToken ct)
    {
        var id = await groups.CreateCommissionAsync(request, User.GetUserId(), CanManageGlobally, ct);
        return Created($"/grupos/comisiones/{id}", new { id });
    }

    /// <summary>Designa al coordinador de una comisión (RF-GRP-03).</summary>
    [HttpPost("comisiones/{id:guid}/coordinador")]
    public async Task<IActionResult> AssignCoordinator(Guid id, AssignCoordinatorRequest request, CancellationToken ct)
    {
        await groups.AssignCoordinatorAsync(id, request.UserId, User.GetUserId(), CanManageGlobally, ct);
        return Ok(new { mensaje = "Coordinador designado." });
    }

    /// <summary>Grupos a los que pertenece el usuario (RF-MEM-09).</summary>
    [HttpGet("mias")]
    public async Task<ActionResult<IReadOnlyList<MyGroupDto>>> Mine(CancellationToken ct)
        => Ok(await groups.ListMyGroupsAsync(User.GetUserId(), ct));

    /// <summary>Solicita ingreso a una comisión (RF-GRP-04).</summary>
    [HasPermission("grupos.solicitar")]
    [HttpPost("comisiones/{id:guid}/solicitar")]
    public async Task<IActionResult> RequestJoin(Guid id, CancellationToken ct)
    {
        await groups.RequestJoinAsync(id, User.GetUserId(), ct);
        return Ok(new { mensaje = "Solicitud enviada. Un coordinador o la Junta la revisará." });
    }

    /// <summary>Solicitudes pendientes de una comisión (coordinador o Junta).</summary>
    [HttpGet("comisiones/{id:guid}/solicitudes")]
    public async Task<ActionResult<IReadOnlyList<JoinRequestDto>>> JoinRequests(Guid id, CancellationToken ct)
        => Ok(await groups.ListJoinRequestsAsync(id, User.GetUserId(), CanManageGlobally, ct));

    /// <summary>Acepta o rechaza una solicitud de ingreso.</summary>
    [HttpPost("solicitudes/{id:guid}/decidir")]
    public async Task<IActionResult> DecideJoin(Guid id, JoinDecisionRequest request, CancellationToken ct)
    {
        await groups.DecideJoinAsync(id, request.Decision, User.GetUserId(), CanManageGlobally, ct);
        return Ok(new { mensaje = "Solicitud procesada." });
    }

    /// <summary>Crea un equipo dentro de una comisión y designa su líder (RF-TEAM-01/02).</summary>
    [HttpPost("comisiones/{id:guid}/equipos")]
    public async Task<IActionResult> CreateTeam(Guid id, CreateTeamRequest request, CancellationToken ct)
    {
        var teamId = await groups.CreateTeamAsync(id, request, User.GetUserId(), CanManageGlobally, ct);
        return Created($"/grupos/{teamId}", new { id = teamId });
    }
}
