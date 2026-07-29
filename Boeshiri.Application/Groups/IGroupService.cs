namespace Boeshiri.Application.Groups;

/// <summary>
/// Comisiones, equipos y membresías (§7.1/7.2). La aprobación de ingresos y la
/// creación de equipos usan permisos CONTEXTUALES: el coordinador de esa comisión
/// (ADR-0005), o quien tenga el permiso global <c>comisiones.ver_todas</c>.
/// </summary>
public interface IGroupService
{
    Task<IReadOnlyList<CommissionDto>> ListCommissionsAsync(CancellationToken ct = default);

    /// <summary>Grupos a los que pertenece el usuario, con su rol (RF-MEM-09).</summary>
    Task<IReadOnlyList<MyGroupDto>> ListMyGroupsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Solicita ingreso a una comisión (RF-GRP-04).</summary>
    Task RequestJoinAsync(Guid commissionId, Guid userId, CancellationToken ct = default);

    /// <summary>Solicitudes pendientes de una comisión (coordinador contextual o global).</summary>
    Task<IReadOnlyList<JoinRequestDto>> ListJoinRequestsAsync(Guid commissionId, Guid userId, bool canManageGlobally, CancellationToken ct = default);

    /// <summary>Acepta/rechaza una solicitud; aceptar agrega al usuario como integrante.</summary>
    Task DecideJoinAsync(Guid requestId, JoinDecision decision, Guid deciderId, bool canManageGlobally, CancellationToken ct = default);

    /// <summary>Crea un equipo dentro de una comisión y designa su líder (RF-TEAM-01/02).</summary>
    Task<Guid> CreateTeamAsync(Guid commissionId, CreateTeamRequest request, Guid userId, bool canManageGlobally, CancellationToken ct = default);
}
