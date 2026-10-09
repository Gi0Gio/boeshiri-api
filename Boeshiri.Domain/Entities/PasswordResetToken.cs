namespace Boeshiri.Domain.Entities;

/// <summary>
/// Enlace para restablecer la contraseña. Se guarda el HASH del token, nunca el
/// token: quien lea la base no puede usarlo. Un solo uso y una hora de vida.
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
