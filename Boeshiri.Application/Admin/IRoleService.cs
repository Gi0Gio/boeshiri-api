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
    Task AssignRoleAsync(Guid userId, Guid roleId, Guid actorId, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid userId, Guid roleId, Guid actorId, CancellationToken ct = default);
}
