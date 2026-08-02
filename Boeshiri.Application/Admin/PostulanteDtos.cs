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
    string? Discipline,
    string? ApplicationReason,
    DateTime RegisteredAt,
    /// <summary>
    /// Si es falso, la solicitud aún no puede decidirse (RF-PUB-13b), pero se lista
    /// igual para que la Junta sepa que la persona ya se registró.
    /// </summary>
    bool EmailVerified);

/// <summary>
/// Enlace de verificación reemitido para entregarlo por otro canal (WhatsApp)
/// cuando el correo no llega. Incluye el teléfono ya en formato internacional.
/// </summary>
public record VerificationLinkDto(string Link, string? Phone, string FullName);

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
