namespace Boeshiri.Application.Admin;

/// <summary>
/// Gestión de roles y permisos (RBAC aditivo, D-1). Solo el Super Administrador
/// (permiso <c>roles.gestionar</c>). Los usuarios acumulan permisos al sumar roles.
/// </summary>
public interface IRoleService
{
    Task<IReadOnlyList<RoleDto>> ListRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PermissionDto>> ListPermissionsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<UserRolesDto>> ListUsersAsync(CancellationToken ct = default);
    /// <summary>Crea un rol adicional (RF-RBAC-04). Devuelve su id.</summary>
    Task<Guid> CreateRoleAsync(CreateRoleRequest request, Guid actorId, CancellationToken ct = default);

    /// <summary>Renombra o recolorea un rol. Los de sistema no se tocan.</summary>
    Task UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Reemplaza el mapa de permisos de un rol. Solo en roles creados a mano: los
    /// de sistema los repone la semilla en cada arranque.
    /// </summary>
    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissions, Guid actorId, CancellationToken ct = default);

    /// <summary>Elimina un rol creado a mano. Devuelve a cuántos usuarios se lo quitó.</summary>
    Task<int> DeleteRoleAsync(Guid roleId, Guid actorId, CancellationToken ct = default);

    Task AssignRoleAsync(Guid userId, Guid roleId, Guid actorId, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid userId, Guid roleId, Guid actorId, CancellationToken ct = default);
}
