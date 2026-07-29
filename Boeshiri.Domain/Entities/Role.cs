namespace Boeshiri.Domain.Entities;

/// <summary>
/// Rol RBAC: agrupación de permisos globales (Catálogo §2). Los usuarios los
/// acumulan (RF-RBAC-02/03). Los roles de sistema (IsSystem) no se borran.
/// </summary>
public class Role
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public string? Color { get; set; }
    public bool IsSystem { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
