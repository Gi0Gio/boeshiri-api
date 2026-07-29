namespace Boeshiri.Domain.Enums;

/// <summary>Tipo de grupo (§7).</summary>
public enum GroupType
{
    /// <summary>Comisión: área permanente de trabajo (RF-GRP-01).</summary>
    Commission,

    /// <summary>Equipo: grupo temporal para una actividad concreta (RF-TEAM-01).</summary>
    Team
}

/// <summary>Rol de un usuario DENTRO de un grupo (fuente de permisos contextuales, ADR-0005).</summary>
public enum GroupRole
{
    /// <summary>Coordinador de una comisión (RF-GRP-02).</summary>
    Coordinator,

    /// <summary>Líder de un equipo (RF-TEAM-02).</summary>
    Leader,

    /// <summary>Integrante.</summary>
    Member
}

/// <summary>Estado de una solicitud de ingreso a comisión (RF-GRP-04).</summary>
public enum JoinRequestStatus
{
    Pending,
    Accepted,
    Rejected
}

/// <summary>Columnas fijas del tablero Kanban (RF-KAN-01). No configurables (RF-KAN-04).</summary>
public enum KanbanStatus
{
    Pending,
    InProgress,
    InReview,
    Done
}
