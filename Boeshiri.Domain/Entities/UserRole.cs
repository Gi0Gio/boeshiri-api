namespace Boeshiri.Domain.Entities;

/// <summary>
/// Puente Usuario–Rol (M:N). Habilita la acumulación de roles (RF-RBAC-02) y
/// registra quién asignó el rol y cuándo (trazabilidad / auditoría).
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public Guid? AssignedBy { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
