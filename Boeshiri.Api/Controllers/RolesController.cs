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
    /// <summary>Crea un rol adicional combinando permisos del catálogo (RF-RBAC-04).</summary>
    [HttpPost("roles")]
    public async Task<ActionResult> CreateRole(CreateRoleRequest request, CancellationToken ct)
    {
        var id = await roles.CreateRoleAsync(request, User.GetUserId(), ct);
        return Created($"/admin/roles/{id}", new { id });
    }

    /// <summary>Renombra o recolorea un rol creado a mano.</summary>
    [HttpPut("roles/{id:guid}")]
    public async Task<IActionResult> UpdateRole(Guid id, UpdateRoleRequest request, CancellationToken ct)
    {
        await roles.UpdateRoleAsync(id, request, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Reemplaza el mapa de permisos de un rol (RF-SA-02).</summary>
    [HttpPut("roles/{id:guid}/permisos")]
    public async Task<IActionResult> SetPermissions(Guid id, SetRolePermissionsRequest request, CancellationToken ct)
    {
        await roles.SetRolePermissionsAsync(id, request.Permissions, User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Elimina un rol creado a mano.</summary>
    [HttpDelete("roles/{id:guid}")]
    public async Task<ActionResult> DeleteRole(Guid id, CancellationToken ct)
    {
        var afectados = await roles.DeleteRoleAsync(id, User.GetUserId(), ct);
        return Ok(new
        {
            afectados,
            mensaje = afectados == 0
                ? "Rol eliminado."
                : $"Rol eliminado. {afectados} {(afectados == 1 ? "miembro perdió" : "miembros perdieron")} sus permisos.",
        });
    }

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
