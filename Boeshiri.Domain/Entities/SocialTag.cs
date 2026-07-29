namespace Boeshiri.Domain.Entities;

/// <summary>
/// Etiqueta social cosmética (Músico, Pintor, Gamer…). Información pública del
/// perfil que NO otorga permisos (RF-RBAC-05). M:N con User.
/// </summary>
public class SocialTag
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
