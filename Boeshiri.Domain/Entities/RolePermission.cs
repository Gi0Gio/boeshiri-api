namespace Boeshiri.Domain.Entities;

/// <summary>
/// Puente Rol–Permiso (M:N): los permisos se asignan estrictamente a los roles
/// (RF-RBAC-01).
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}
