using Boeshiri.Application.Admin;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Domain.Entities;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Admin;

/// <summary>Gestión de roles/permisos y asignación a usuarios (RBAC aditivo, D-1).</summary>
public class RoleService(BoeshiriDbContext db, IAuditLogger audit) : IRoleService
{
    private const string Wildcard = "*";

    public async Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default)
    {
        return await db.Roles
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .Select(r => new RoleDto(
                r.Id, r.Name, r.Color, r.IsSystem,
                r.RolePermissions.Select(rp => rp.Permission.Key).OrderBy(k => k).ToList(),
                r.UserRoles.Count))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default)
    {
        return await db.Permissions
            .OrderBy(p => p.Key)
            .Select(p => new PermissionDto(p.Key, p.Description))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UserRolesDto>> ListUsersAsync(CancellationToken ct = default)
    {
        return await db.Users
            .OrderBy(u => u.FullName)
            .Select(u => new UserRolesDto(
                u.Id, u.FullName, u.Email, u.Status,
                u.UserRoles.Select(ur => new RoleRefDto(ur.Role.Id, ur.Role.Name, ur.Role.Color)).ToList()))
            .ToListAsync(ct);
    }

    public async Task AssignRoleAsync(Guid userId, Guid roleId, Guid actorId, CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
            throw AppException.NotFound("Usuario no encontrado.");
        if (!await db.Roles.AnyAsync(r => r.Id == roleId, ct))
            throw AppException.NotFound("Rol no encontrado.");

        if (await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct))
            return; // ya lo tiene: idempotente

        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId, AssignedBy = actorId });
        audit.Log(actorId, "rol.asignado", "User", userId.ToString(), roleId.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveRoleAsync(Guid userId, Guid roleId, Guid actorId, CancellationToken ct = default)
    {
        var assignment = await db.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, ct);
        if (assignment is null)
            return; // no lo tiene: nada que hacer

        // Salvaguarda: no dejar al sistema sin ningún Super Administrador (rol con "*").
        var roleHasWildcard = await db.RolePermissions
            .AnyAsync(rp => rp.RoleId == roleId && rp.Permission.Key == Wildcard, ct);
        if (roleHasWildcard)
        {
            var superAdmins = await db.UserRoles
                .Where(ur => ur.Role.RolePermissions.Any(rp => rp.Permission.Key == Wildcard))
                .Select(ur => ur.UserId)
                .Distinct()
                .CountAsync(ct);
            if (superAdmins <= 1)
                throw AppException.BadRequest("No puedes quitar el último Super Administrador del sistema.");
        }

        db.UserRoles.Remove(assignment);
        audit.Log(actorId, "rol.retirado", "User", userId.ToString(), roleId.ToString());
        await db.SaveChangesAsync(ct);
    }
}
