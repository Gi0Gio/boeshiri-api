namespace Boeshiri.Domain.Enums;

/// <summary>
/// Estados del miembro (Requerimientos §3.1). Ortogonal a los roles/permisos:
/// actúa como compuerta de sesión antes de evaluar la autorización.
/// </summary>
public enum MemberStatus
{
    /// <summary>Creó cuenta y solicitó ingreso; en espera de aprobación.</summary>
    Applicant,

    /// <summary>Aprobado y participando; acceso pleno de miembro.</summary>
    Active,

    /// <summary>Pertenece pero en pausa temporal, sin participación.</summary>
    Inactive,

    /// <summary>Acceso restringido temporalmente por medida disciplinaria.</summary>
    Suspended,

    /// <summary>Se fue voluntariamente (egresado).</summary>
    Retired,

    /// <summary>Removido por la administración por incumplimiento.</summary>
    Expelled
}
