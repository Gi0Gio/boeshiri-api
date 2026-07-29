using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Solicitud de ingreso a una comisión (RF-GRP-04). La aprueba el coordinador de
/// la comisión (permiso contextual) o la Junta.
/// </summary>
public class JoinRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CommissionId { get; set; }
    public Group Commission { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
}
