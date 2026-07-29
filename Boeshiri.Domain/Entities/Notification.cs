namespace Boeshiri.Domain.Entities;

/// <summary>
/// Aviso in-app dirigido a un usuario (RF-TRA-02, RF-PUB-16). En la v1 los avisos
/// son solo en el panel; el correo se difiere (salvo la verificación de registro).
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Tipo/categoría del aviso, p. ej. "solicitud.aceptada".</summary>
    public required string Type { get; set; }
    public required string Message { get; set; }
    public bool Read { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
