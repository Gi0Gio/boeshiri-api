namespace Boeshiri.Domain.Entities;

/// <summary>Etiqueta temática de publicaciones (M:N). Distinta de las etiquetas
/// sociales cosméticas del perfil (<see cref="SocialTag"/>).</summary>
public class Tag
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Name { get; set; }

    public ICollection<Publication> Publications { get; set; } = new List<Publication>();
}
