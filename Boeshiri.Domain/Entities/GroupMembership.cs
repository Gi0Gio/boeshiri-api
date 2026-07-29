using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Pertenencia de un usuario a un grupo con su rol contextual (Coordinador / Líder
/// / Integrante). Es la fuente de los permisos contextuales (ADR-0005).
/// </summary>
public class GroupMembership
{
    public Guid GroupId { get; set; }
    public Group Group { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public GroupRole Role { get; set; } = GroupRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
