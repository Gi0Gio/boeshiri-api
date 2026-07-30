namespace Boeshiri.Domain.Entities;

/// <summary>Habilidad del perfil con su nivel (1–8), para el portafolio (RF-MEM-01).</summary>
public class ProfileSkill
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public required string Name { get; set; }

    /// <summary>Nivel de 1 (básico) a 8 (experto).</summary>
    public int Level { get; set; }

    public int Order { get; set; }
}
