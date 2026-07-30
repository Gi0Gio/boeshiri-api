using Boeshiri.Api.Authorization;
using Boeshiri.Application.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Roles y permisos (RBAC aditivo, D-1). Solo el Super Administrador, vía el
/// permiso <c>roles.gestionar</c> (RF-RBAC-01..04).
/// </summary>
[ApiController]
[Route("admin")]
[HasPermission("roles.gestionar")]
public class RolesController(IRoleService roles) : ControllerBase
{
    /// <summary>Roles con sus permisos y conteo de usuarios.</summary>
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> Roles(CancellationToken ct)
        => Ok(await roles.ListRolesAsync(ct));

    /// <summary>Catálogo de permisos.</summary>
    [HttpGet("permisos")]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> Permissions(CancellationToken ct)
        => Ok(await roles.ListPermissionsAsync(ct));

    /// <summary>Usuarios con sus roles (para asignar/retirar).</summary>
    [HttpGet("usuarios")]
    public async Task<ActionResult<IReadOnlyList<UserRolesDto>>> Users(CancellationToken ct)
        => Ok(await roles.ListUsersAsync(ct));

    /// <summary>Asigna un rol a un usuario.</summary>
    [HttpPost("usuarios/{userId:guid}/roles")]
    public async Task<IActionResult> Assign(Guid userId, AssignRoleRequest request, CancellationToken ct)
    {
        await roles.AssignRoleAsync(userId, request.RoleId, User.GetUserId(), ct);
        return Ok(new { mensaje = "Rol asignado." });
    }

    /// <summary>Retira un rol de un usuario.</summary>
    [HttpDelete("usuarios/{userId:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> Remove(Guid userId, Guid roleId, CancellationToken ct)
    {
        await roles.RemoveRoleAsync(userId, roleId, User.GetUserId(), ct);
        return NoContent();
    }
}
