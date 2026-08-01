using Boeshiri.Api.Authorization;
using Boeshiri.Application.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Gestión de los estados de miembro por la administración (RF-ADM-03, SDD §6).
/// Exige el permiso <c>miembros.gestionar_estado</c> (Junta Directiva).
/// </summary>
[ApiController]
[Route("admin/miembros")]
[HasPermission("miembros.gestionar_estado")]
public class MiembrosController(IMemberService members) : ControllerBase
{
    /// <summary>Lista los miembros con su estado y roles.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberAdminDto>>> List(CancellationToken ct)
        => Ok(await members.ListAsync(ct));

    /// <summary>Cambia el estado de un miembro (Activo/Inactivo/Suspendido/Retirado/Expulsado).</summary>
    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeMemberStatusRequest request, CancellationToken ct)
    {
        await members.ChangeStatusAsync(id, request, User.GetUserId(), ct);
        return Ok(new { mensaje = "Estado del miembro actualizado." });
    }
}
