using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Admin;

/// <summary>
/// Miembro tal como lo ve la administración para gestionar su estado (RF-ADM-03).
/// Incluye los roles como chips y las fechas que explican el estado actual.
/// </summary>
public record MemberAdminDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    MemberStatus Status,
    DateTime RegisteredAt,
    DateTime? StatusChangedAt,
    IReadOnlyList<RoleRefDto> Roles);

/// <summary>
/// Cambio de estado de un miembro (RF-ADM-03). No admite <c>Applicant</c>: el
/// alta de postulantes va por su propio flujo (RF-PUB-15).
/// </summary>
public record ChangeMemberStatusRequest
{
    [Required]
    public required MemberStatus Status { get; init; }

    /// <summary>Motivo del cambio; queda en la auditoría (uso interno).</summary>
    [MaxLength(500)]
    public string? Motivo { get; init; }
}
