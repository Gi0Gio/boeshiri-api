using Boeshiri.Api.Authorization;
using Boeshiri.Application.Finance;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Finanzas del colectivo (§10.4). Ver el balance exige <c>finanzas.ver</c>
/// (Junta/Tesorero); modificarlo exige <c>finanzas.editar</c> — solo el Tesorero
/// (RF-ADM-08).
/// </summary>
[ApiController]
[Route("finanzas")]
public class FinanzasController(IFinanceService finance) : ControllerBase
{
    /// <summary>Balance general y movimientos (RF-ADM-08).</summary>
    [HasPermission("finanzas.ver")]
    [HttpGet]
    public async Task<ActionResult<FinanceSummaryDto>> Summary(CancellationToken ct)
        => Ok(await finance.GetSummaryAsync(ct));

    /// <summary>Registra un movimiento (solo Tesorero).</summary>
    [HasPermission("finanzas.editar")]
    [HttpPost("movimientos")]
    public async Task<ActionResult> Create(CreateMovementRequest request, CancellationToken ct)
    {
        var id = await finance.CreateMovementAsync(User.GetUserId(), request, ct);
        return Created($"/finanzas/movimientos/{id}", new { id });
    }

    /// <summary>Edita un movimiento (solo Tesorero).</summary>
    [HasPermission("finanzas.editar")]
    [HttpPut("movimientos/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateMovementRequest request, CancellationToken ct)
    {
        await finance.UpdateMovementAsync(id, User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Elimina un movimiento (solo Tesorero).</summary>
    [HasPermission("finanzas.editar")]
    [HttpDelete("movimientos/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await finance.DeleteMovementAsync(id, User.GetUserId(), ct);
        return NoContent();
    }
}
