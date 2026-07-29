namespace Boeshiri.Domain.Entities;

/// <summary>
/// Token de verificación de correo de un solo uso (RF-PUB-13b). El correo debe
/// confirmarse antes de poder postularse.
/// </summary>
public class VerificationToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool Used { get; set; }
}
