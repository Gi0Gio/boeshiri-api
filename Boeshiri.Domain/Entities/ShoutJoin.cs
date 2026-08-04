namespace Boeshiri.Domain.Entities;

/// <summary>
/// Miembro apuntado a un grito. La clave compuesta (grito, usuario) es lo que
/// impide apuntarse dos veces: la garantía vive en la base de datos, no en una
/// comprobación del servicio que una condición de carrera puede saltarse.
/// </summary>
public class ShoutJoin
{
    public Guid ShoutId { get; set; }
    public Shout Shout { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
