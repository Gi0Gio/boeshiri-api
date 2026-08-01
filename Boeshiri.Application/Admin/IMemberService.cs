namespace Boeshiri.Application.Admin;

/// <summary>
/// Gestión del ciclo de vida de los miembros por la administración (RF-ADM-03).
/// Exige el permiso <c>miembros.gestionar_estado</c> a nivel de endpoint.
/// </summary>
public interface IMemberService
{
    /// <summary>
    /// Lista los miembros ya decididos (todos menos los postulantes pendientes),
    /// con su estado y roles, para el panel de administración.
    /// </summary>
    Task<IReadOnlyList<MemberAdminDto>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Cambia el estado de un miembro (Activo/Inactivo/Suspendido/Retirado/Expulsado).
    /// Genera aviso in-app al afectado y entrada de auditoría.
    /// </summary>
    Task ChangeStatusAsync(Guid memberId, ChangeMemberStatusRequest request, Guid actorId, CancellationToken ct = default);
}
