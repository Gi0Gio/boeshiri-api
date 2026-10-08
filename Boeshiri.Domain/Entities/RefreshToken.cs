namespace Boeshiri.Domain.Entities;

/// <summary>
/// Token de renovación de sesión. Viaja en una cookie HttpOnly y permite pedir un
/// JWT nuevo sin volver a escribir la contraseña. Se guarda solo su hash: quien
/// lea la base no puede usarlos. Es de un solo uso: cada renovación lo revoca y
/// emite otro (rotación), y reusar uno revocado delata un robo.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>SHA-256 en hex del valor que tiene el navegador.</summary>
    public required string TokenHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>El token que lo sustituyó al rotar; null si se revocó por otra causa.</summary>
    public Guid? ReplacedById { get; set; }
}
