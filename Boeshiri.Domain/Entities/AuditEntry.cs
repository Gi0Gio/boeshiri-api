namespace Boeshiri.Domain.Entities;

/// <summary>
/// Registro append-only de una acción relevante (§11). Visible únicamente para el
/// Super Administrador (RF-AUD-02).
/// </summary>
public class AuditEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Quién ejecutó la acción (null si fue el sistema).</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Acción realizada, p. ej. "postulante.aceptado".</summary>
    public required string Action { get; set; }

    /// <summary>Tipo de objeto afectado, p. ej. "User".</summary>
    public required string ObjectType { get; set; }

    /// <summary>Identificador del objeto afectado (como texto).</summary>
    public string? ObjectId { get; set; }

    /// <summary>Detalle adicional opcional (texto o JSON).</summary>
    public string? Metadata { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
