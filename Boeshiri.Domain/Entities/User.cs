using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Usuario del sistema (Requerimientos §3, §5.1). Concentra credenciales, datos de
/// postulación, estado, perfil y flags de privacidad. Sus permisos NO se guardan:
/// se derivan de sus roles (RBAC aditivo, D-1).
/// </summary>
public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // ── Credenciales / verificación ──────────────────────────────
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }

    // ── Datos de postulación / identidad ─────────────────────────
    public required string FullName { get; set; }
    public string? Phone { get; set; }
    public string? ApplicationReason { get; set; }

    // ── Estado y su historia (RF-PUB-17: espera de 1 mes tras rechazo) ──
    public MemberStatus Status { get; set; } = MemberStatus.Applicant;
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime? StatusChangedAt { get; set; }
    public DateTime? RejectedAt { get; set; }

    // ── Perfil (RF-MEM-01/02) ────────────────────────────────────
    public string? Bio { get; set; }
    public string? PhotoUrl { get; set; }
    public string? Location { get; set; }
    public string? Discipline { get; set; }

    // ── Privacidad: ocultables por el miembro (RF-MEM-03) ────────
    public bool ShowPhone { get; set; }
    public bool ShowEmail { get; set; }
    public bool ShowWhatsapp { get; set; }
    public bool ShowCommittees { get; set; } = true;
    public bool ShowHistory { get; set; } = true;

    /// <summary>Se dio de alta en el marketplace para publicar productos (RF-MKT-03).</summary>
    public bool MarketplaceActive { get; set; }

    // ── Relaciones ───────────────────────────────────────────────
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<SocialLink> SocialLinks { get; set; } = new List<SocialLink>();
    public ICollection<SocialTag> Tags { get; set; } = new List<SocialTag>();

    /// <summary>
    /// Permisos efectivos = unión de los permisos de todos sus roles (RF-RBAC-02).
    /// Requiere que UserRoles → Role → RolePermissions → Permission estén cargados.
    /// El comodín "*" (Super Administrador) implica todos los permisos.
    /// </summary>
    public IReadOnlySet<string> EffectivePermissions()
    {
        return UserRoles
            .SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Key))
            .ToHashSet();
    }
}
