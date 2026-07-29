using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Groups;

/// <summary>Comisión en un listado (RF-GRP-01).</summary>
public record CommissionDto(Guid Id, string Name, bool Permanent, int MemberCount, string? CoordinatorName);

/// <summary>Grupo al que pertenece el usuario, con su rol contextual (RF-MEM-09).</summary>
public record MyGroupDto(Guid Id, string Name, GroupType Type, GroupRole Role, Guid? ParentCommissionId);

/// <summary>Solicitud de ingreso para que el coordinador/Junta decida (RF-GRP-04).</summary>
public record JoinRequestDto(Guid Id, Guid UserId, string UserName, string UserEmail, DateTime CreatedAt);

/// <summary>Decisión sobre una solicitud de ingreso.</summary>
public enum JoinDecision
{
    Accept,
    Reject
}

public record JoinDecisionRequest
{
    [Required]
    public required JoinDecision Decision { get; init; }
}

/// <summary>Datos para crear un equipo dentro de una comisión (RF-TEAM-01/02).</summary>
public record CreateTeamRequest
{
    [Required, MaxLength(120)]
    public required string Name { get; init; }

    /// <summary>Usuario que quedará como líder del equipo.</summary>
    [Required]
    public required Guid LeaderUserId { get; init; }
}
