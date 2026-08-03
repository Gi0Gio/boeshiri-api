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

    public async Task<Guid> CreateRoleAsync(CreateRoleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var nombre = request.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name.ToLower() == nombre.ToLower(), ct))
            throw AppException.Conflict($"Ya existe un rol llamado «{nombre}».");

        var permisos = await ResolverPermisosAsync(request.Permissions ?? [], ct);

        var role = new Role { Name = nombre, Color = request.Color, IsSystem = false };
        foreach (var p in permisos)
            role.RolePermissions.Add(new RolePermission { Role = role, PermissionId = p.Id });

        db.Roles.Add(role);
        audit.Log(actorId, "rol.creado", "Role", role.Id.ToString(), $"{nombre} · {permisos.Count} permisos");
        await db.SaveChangesAsync(ct);
        return role.Id;
    }

    public async Task UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, Guid actorId, CancellationToken ct = default)
    {
        var role = await CargarEditableAsync(roleId, ct);

        var nombre = request.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Id != roleId && r.Name.ToLower() == nombre.ToLower(), ct))
            throw AppException.Conflict($"Ya existe un rol llamado «{nombre}».");

        role.Name = nombre;
        role.Color = request.Color;
        audit.Log(actorId, "rol.editado", "Role", role.Id.ToString(), nombre);
        await db.SaveChangesAsync(ct);
    }

    public async Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissions, Guid actorId, CancellationToken ct = default)
    {
        var role = await db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw AppException.NotFound("Rol no encontrado.");

        // Los de sistema no se editan porque la semilla repone sus permisos en cada
        // arranque: quitarlos aquí daría una reversión silenciosa tras el despliegue.
        if (role.IsSystem)
            throw AppException.Conflict(
                "Los roles del sistema no se editan: la semilla repondría sus permisos al reiniciar. Crea un rol nuevo con los permisos que necesites.");

        var permisos = await ResolverPermisosAsync(permissions, ct);

        role.RolePermissions.Clear();
        foreach (var p in permisos)
            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p.Id });

        audit.Log(actorId, "rol.permisos_cambiados", "Role", role.Id.ToString(),
            permisos.Count == 0 ? "sin permisos" : string.Join(", ", permisos.Select(p => p.Key)));
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteRoleAsync(Guid roleId, Guid actorId, CancellationToken ct = default)
    {
        var role = await CargarEditableAsync(roleId, ct);

        // Se permite aunque tenga usuarios, pero se informa a cuántos afecta: es una
        // pérdida de permisos y tiene que quedar visible y auditada.
        var afectados = await db.UserRoles.CountAsync(ur => ur.RoleId == roleId, ct);

        db.Roles.Remove(role);
        audit.Log(actorId, "rol.eliminado", "Role", roleId.ToString(), $"{role.Name} · {afectados} usuarios afectados");
        await db.SaveChangesAsync(ct);
        return afectados;
    }

    /// <summary>Carga un rol y rechaza los de sistema, que no se borran ni renombran.</summary>
    private async Task<Role> CargarEditableAsync(Guid roleId, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct)
            ?? throw AppException.NotFound("Rol no encontrado.");

        if (role.IsSystem)
            throw AppException.Conflict($"«{role.Name}» es un rol del sistema y no se puede modificar ni eliminar.");

        return role;
    }

    /// <summary>
    /// Valida las claves contra el catálogo. El comodín queda fuera: concederlo a un
    /// rol nuevo crearía un segundo Super Administrador saltándose el modelo.
    /// </summary>
    private async Task<List<Permission>> ResolverPermisosAsync(IReadOnlyList<string> keys, CancellationToken ct)
    {
        var limpias = keys.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (limpias.Contains(Wildcard))
            throw AppException.BadRequest("El comodín «*» no se concede a roles nuevos. Asigna el rol Super Administrador si eso es lo que buscas.");

        var encontrados = await db.Permissions.Where(p => limpias.Contains(p.Key)).ToListAsync(ct);

        var desconocidos = limpias.Except(encontrados.Select(p => p.Key), StringComparer.OrdinalIgnoreCase).ToList();
        if (desconocidos.Count > 0)
            throw AppException.BadRequest($"Permisos desconocidos: {string.Join(", ", desconocidos)}.");

        return encontrados;
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
