using Boeshiri.Application.Audit;
using Boeshiri.Domain.Entities;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Audit;

/// <summary>Auditoría respaldada por EF Core (append-only). Consulta: Super Admin.</summary>
public class AuditLogger(BoeshiriDbContext db) : IAuditLogger
{
    public void Log(Guid? actorId, string action, string objectType, string? objectId = null, string? metadata = null)
    {
        db.AuditEntries.Add(new AuditEntry
        {
            ActorId = actorId,
            Action = action,
            ObjectType = objectType,
            ObjectId = objectId,
            Metadata = metadata
        });
    }

    public async Task<IReadOnlyList<AuditEntryDto>> QueryAsync(int take = 100, CancellationToken ct = default)
    {
        return await db.AuditEntries
            .OrderByDescending(a => a.Timestamp)
            .Take(Math.Clamp(take, 1, 500))
            // Join opcional con users para mostrar el correo del actor.
            .Select(a => new AuditEntryDto(
                a.Id,
                a.Timestamp,
                a.ActorId,
                db.Users.Where(u => u.Id == a.ActorId).Select(u => u.Email).FirstOrDefault(),
                a.Action,
                a.ObjectType,
                a.ObjectId,
                a.Metadata))
            .ToListAsync(ct);
    }
}
