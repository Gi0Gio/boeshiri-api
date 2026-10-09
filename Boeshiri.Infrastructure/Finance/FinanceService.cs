using Boeshiri.Infrastructure.Common;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Finance;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Finance;

/// <summary>Finanzas (§10.4). El balance se deriva de los movimientos; auditoría en cada cambio.</summary>
public class FinanceService(BoeshiriDbContext db, IAuditLogger audit) : IFinanceService
{
    public async Task<FinanceSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var movements = await db.FinancialMovements
            .Where(m => m.VoidedAt == null)
            .OrderByDescending(m => m.Date)
            .Select(m => new MovementDto(m.Id, m.Date, m.Concept, m.Type, m.Amount))
            .ToListAsync(ct);

        var income = movements.Where(m => m.Type == MovementType.Income).Sum(m => m.Amount);
        var expense = movements.Where(m => m.Type == MovementType.Expense).Sum(m => m.Amount);

        return new FinanceSummaryDto(income - expense, income, expense, movements);
    }

    public async Task<Guid> CreateMovementAsync(Guid userId, CreateMovementRequest request, CancellationToken ct = default)
    {
        var movement = new FinancialMovement
        {
            Date = Fechas.Utc(request.Date),
            Concept = request.Concept.Trim(),
            Type = request.Type,
            Amount = request.Amount,
            CreatedBy = userId
        };

        db.FinancialMovements.Add(movement);
        audit.Log(userId, "finanzas.movimiento_creado", "FinancialMovement", movement.Id.ToString(),
            $"{movement.Type} {movement.Amount:0.00} — {movement.Concept}");
        await db.SaveChangesAsync(ct);
        return movement.Id;
    }

    public async Task UpdateMovementAsync(Guid id, Guid userId, UpdateMovementRequest request, CancellationToken ct = default)
    {
        var movement = await db.FinancialMovements.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw AppException.NotFound("El movimiento no existe.");
        if (movement.VoidedAt is not null)
            throw AppException.Conflict("El movimiento está anulado y ya no se edita.");

        // La auditoría guarda el antes y el después: sin el «antes», editar un
        // importe borraba el valor original sin dejar rastro.
        var antes = $"{movement.Date:yyyy-MM-dd} {movement.Type} {movement.Amount:0.00} — {movement.Concept}";

        movement.Date = Fechas.Utc(request.Date);
        movement.Concept = request.Concept.Trim();
        movement.Type = request.Type;
        movement.Amount = request.Amount;
        movement.UpdatedAt = DateTime.UtcNow;

        audit.Log(userId, "finanzas.movimiento_editado", "FinancialMovement", movement.Id.ToString(),
            $"{antes} → {movement.Date:yyyy-MM-dd} {movement.Type} {movement.Amount:0.00} — {movement.Concept}");
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteMovementAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var movement = await db.FinancialMovements.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw AppException.NotFound("El movimiento no existe.");

        if (movement.VoidedAt is not null)
            return; // ya estaba anulado

        movement.VoidedAt = DateTime.UtcNow;
        movement.VoidedBy = userId;
        audit.Log(userId, "finanzas.movimiento_anulado", "FinancialMovement", movement.Id.ToString(),
            $"{movement.Date:yyyy-MM-dd} {movement.Type} {movement.Amount:0.00} — {movement.Concept}");
        await db.SaveChangesAsync(ct);
    }
}
