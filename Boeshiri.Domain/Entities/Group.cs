using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Grupo del colectivo: comisión (permanente) o equipo (temporal) (§7). Los roles
/// dentro del grupo viven en <see cref="GroupMembership"/> (permisos contextuales).
/// </summary>
public class Group
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }
    public GroupType Type { get; set; }

    /// <summary>Solo comisiones: si es un área permanente (RF-GRP-01).</summary>
    public bool Permanent { get; set; }

    /// <summary>Solo equipos: comisión a la que pertenece (RF-TEAM-01).</summary>
    public Guid? ParentCommissionId { get; set; }
    public Group? ParentCommission { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GroupMembership> Memberships { get; set; } = new List<GroupMembership>();
    public ICollection<KanbanTask> Tasks { get; set; } = new List<KanbanTask>();
}
