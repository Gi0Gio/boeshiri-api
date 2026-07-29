using Boeshiri.Application.Common;
using Boeshiri.Application.Finance;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Finance;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Finance;

public class FinanceServiceTests : IDisposable
{
    private readonly TestDb _db = new();
    private readonly Guid _treasurer = Guid.NewGuid();

    private FinanceService NewService(BoeshiriDbContext ctx) => new(ctx, new AuditLogger(ctx));

    private static CreateMovementRequest Mov(MovementType type, decimal amount, string concept = "x") =>
        new() { Date = DateTime.UtcNow, Concept = concept, Type = type, Amount = amount };

    [Fact]
    public async Task GetSummaryAsync_ComputesBalanceFromMovements()
    {
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.CreateMovementAsync(_treasurer, Mov(MovementType.Income, 300));
            await svc.CreateMovementAsync(_treasurer, Mov(MovementType.Income, 420));
            await svc.CreateMovementAsync(_treasurer, Mov(MovementType.Expense, 180));
            await svc.CreateMovementAsync(_treasurer, Mov(MovementType.Expense, 95));
        }

        await using var ctx2 = _db.CreateContext();
        var summary = await NewService(ctx2).GetSummaryAsync();

        Assert.Equal(720m, summary.TotalIncome);
        Assert.Equal(275m, summary.TotalExpense);
        Assert.Equal(445m, summary.Balance);
        Assert.Equal(4, summary.Movements.Count);
    }

    [Fact]
    public async Task CreateMovementAsync_CreatesAndAudits()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateMovementAsync(_treasurer, Mov(MovementType.Income, 250, "Aporte evento"));

        await using var check = _db.CreateContext();
        Assert.True(await check.FinancialMovements.AnyAsync(m => m.Id == id));
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "finanzas.movimiento_creado" && a.ActorId == _treasurer));
    }

    [Fact]
    public async Task UpdateMovementAsync_UpdatesAndSetsUpdatedAt()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateMovementAsync(_treasurer, Mov(MovementType.Expense, 100));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).UpdateMovementAsync(id, _treasurer,
                new UpdateMovementRequest { Date = DateTime.UtcNow, Concept = "Corregido", Type = MovementType.Expense, Amount = 120 });

        await using var check = _db.CreateContext();
        var m = await check.FinancialMovements.SingleAsync(x => x.Id == id);
        Assert.Equal(120m, m.Amount);
        Assert.Equal("Corregido", m.Concept);
        Assert.NotNull(m.UpdatedAt);
    }

    [Fact]
    public async Task DeleteMovementAsync_RemovesAndAudits()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateMovementAsync(_treasurer, Mov(MovementType.Income, 50));

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).DeleteMovementAsync(id, _treasurer);

        await using var check = _db.CreateContext();
        Assert.False(await check.FinancialMovements.AnyAsync(m => m.Id == id));
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "finanzas.movimiento_eliminado"));
    }

    [Fact]
    public async Task UpdateMovementAsync_NotFound_ThrowsNotFound()
    {
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).UpdateMovementAsync(Guid.NewGuid(), _treasurer,
                new UpdateMovementRequest { Date = DateTime.UtcNow, Concept = "x", Type = MovementType.Income, Amount = 1 }));
        Assert.Equal(404, ex.StatusCode);
    }

    public void Dispose() => _db.Dispose();
}
