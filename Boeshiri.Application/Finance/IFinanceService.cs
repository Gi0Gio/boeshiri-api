namespace Boeshiri.Application.Finance;

/// <summary>
/// Finanzas del colectivo (§10.4). Ver el balance exige <c>finanzas.ver</c>
/// (Junta/Tesorero); modificarlo exige <c>finanzas.editar</c> — solo el Tesorero
/// (RF-ADM-08). La autorización la aplica el controlador vía permisos.
/// </summary>
public interface IFinanceService
{
    Task<FinanceSummaryDto> GetSummaryAsync(CancellationToken ct = default);

    Task<Guid> CreateMovementAsync(Guid userId, CreateMovementRequest request, CancellationToken ct = default);

    Task UpdateMovementAsync(Guid id, Guid userId, UpdateMovementRequest request, CancellationToken ct = default);

    Task DeleteMovementAsync(Guid id, Guid userId, CancellationToken ct = default);
}
