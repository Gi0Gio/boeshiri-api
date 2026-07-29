using System.ComponentModel.DataAnnotations;

namespace Boeshiri.Application.Admin;

/// <summary>
/// Datos que la Junta / RRHH ven de un postulante para decidir (RF-PUB-15).
/// </summary>
public record PostulanteDto(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string? ApplicationReason,
    DateTime RegisteredAt);

/// <summary>Tipo de decisión sobre un postulante.</summary>
public enum DecisionType
{
    Aceptar,
    Rechazar
}

/// <summary>Decisión de la Junta / RRHH sobre un postulante (RF-PUB-15/16/17).</summary>
public record DecisionRequest
{
    [Required]
    public required DecisionType Decision { get; init; }

    /// <summary>Motivo opcional (uso interno; no se expone al postulante en v1).</summary>
    [MaxLength(500)]
    public string? Motivo { get; init; }
}
