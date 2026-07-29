using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Red social del perfil (RF-MEM-04/05). Cada una tiene interruptor de
/// visibilidad y se oculta si está vacía o apagada.
/// </summary>
public class SocialLink
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public SocialNetworkType Type { get; set; }
    public required string Value { get; set; }
    public bool Visible { get; set; } = true;
}
