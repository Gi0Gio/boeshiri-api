using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Notifications;
using Boeshiri.Application.Shouts;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Shouts;

/// <summary>
/// Gritos: llamados abiertos entre miembros. Lo guardado es solo lo que decidió
/// una persona; «lleno» y «vencido» se calculan al leer (ver <see cref="StateOf"/>).
/// </summary>
public class ShoutService(
    BoeshiriDbContext db,
    INotificationService notifications,
    IAuditLogger audit) : IShoutService
{
    /// <summary>
    /// Tope de gritos abiertos por miembro. El mural reparte cuota por familia para
    /// que ninguna inunde la pared; sin este tope, una sola persona con muchas ganas
    /// rompe ese reparto desde dentro de su propia familia.
    /// </summary>
    private const int MaxOpenPerMember = 3;

    // ── Lectura ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<ShoutSummaryDto>> ListOpenAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var rows = await db.Shouts
            .Where(s => s.Status == ShoutStatus.Open && s.HappensAt > now)
            .OrderBy(s => s.HappensAt)
            .Select(s => new Row(
                s.Id, s.Title, s.Place, s.HappensAt, s.Slots, s.Joins.Count, s.Fee,
                s.Status, s.AuthorId, s.Author.FullName,
                s.Joins.Any(j => j.UserId == userId), s.CreatedAt))
            .ToListAsync(ct);

        return rows.Select(r => r.ToSummary(userId, now)).ToList();
    }

    public async Task<IReadOnlyList<ShoutSummaryDto>> ListMineAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var rows = await db.Shouts
            .Where(s => s.AuthorId == userId && s.Status != ShoutStatus.Deleted)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new Row(
                s.Id, s.Title, s.Place, s.HappensAt, s.Slots, s.Joins.Count, s.Fee,
                s.Status, s.AuthorId, s.Author.FullName,
                s.Joins.Any(j => j.UserId == userId), s.CreatedAt))
            .ToListAsync(ct);

        return rows.Select(r => r.ToSummary(userId, now)).ToList();
    }

    public async Task<ShoutDetailDto> GetDetailAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var s = await db.Shouts
            .Include(x => x.Author)
            .Include(x => x.Joins).ThenInclude(j => j.User)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (s is null || s.Status == ShoutStatus.Deleted)
            throw AppException.NotFound("El grito no está disponible.");

        var now = DateTime.UtcNow;
        var taken = s.Joins.Count;

        return new ShoutDetailDto(
            s.Id, s.Title, s.Detail, s.Place, s.HappensAt, s.Slots, taken, s.Fee,
            StateOf(s.Status, s.HappensAt, taken, s.Slots, now),
            s.AuthorId, s.Author.FullName,
            s.Joins.Any(j => j.UserId == userId),
            s.AuthorId == userId,
            s.CreatedAt,
            s.Joins.OrderBy(j => j.JoinedAt)
                .Select(j => new ShoutMemberDto(j.UserId, j.User.FullName, j.User.PhotoUrl))
                .ToList());
    }

    public async Task<int> CountOpenAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await db.Shouts.CountAsync(s => s.Status == ShoutStatus.Open && s.HappensAt > now, ct);
    }

    // ── Escritura ────────────────────────────────────────────────

    public async Task<Guid> CreateAsync(Guid userId, CreateShoutRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var happensAt = Normalize(request.HappensAt);

        if (happensAt <= now)
            throw AppException.BadRequest("El grito tiene que ser para una fecha futura.");

        var abiertos = await db.Shouts.CountAsync(
            s => s.AuthorId == userId && s.Status == ShoutStatus.Open && s.HappensAt > now, ct);

        if (abiertos >= MaxOpenPerMember)
            throw AppException.Conflict(
                $"Ya tienes {MaxOpenPerMember} gritos abiertos. Cierra alguno antes de echar otro.");

        var shout = new Shout
        {
            AuthorId = userId,
            Title = request.Title.Trim(),
            Detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim(),
            Place = request.Place.Trim(),
            HappensAt = happensAt,
            Slots = request.Slots,
            Fee = request.Fee
        };

        // Quien grita también va: así los cupos y los apuntados se cuentan igual
        // desde el primer momento, y «faltan N» nunca incluye a quien convocó.
        shout.Joins.Add(new ShoutJoin { UserId = userId });

        db.Shouts.Add(shout);
        await db.SaveChangesAsync(ct);
        return shout.Id;
    }

    public async Task UpdateAsync(Guid id, Guid userId, UpdateShoutRequest request, CancellationToken ct = default)
    {
        var s = await db.Shouts
            .Include(x => x.Joins)
            .FirstOrDefaultAsync(x => x.Id == id && x.AuthorId == userId, ct)
            ?? throw AppException.NotFound("El grito no existe o no es tuyo.");

        if (s.Status != ShoutStatus.Open)
            throw AppException.Conflict("Este grito ya no está abierto.");

        var happensAt = Normalize(request.HappensAt);
        if (happensAt <= DateTime.UtcNow)
            throw AppException.BadRequest("El grito tiene que ser para una fecha futura.");

        // Bajar los cupos por debajo de la gente ya apuntada dejaría a alguien fuera
        // sin decírselo. Si quiere menos gente, tiene que hablarlo, no editarlo.
        if (request.Slots < s.Joins.Count)
            throw AppException.Conflict(
                $"Ya hay {s.Joins.Count} apuntados: no puedes dejar menos cupos que eso.");

        s.Title = request.Title.Trim();
        s.Detail = string.IsNullOrWhiteSpace(request.Detail) ? null : request.Detail.Trim();
        s.Place = request.Place.Trim();
        s.HappensAt = happensAt;
        s.Slots = request.Slots;
        s.Fee = request.Fee;
        s.EditedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task JoinAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Bloquea la fila del grito hasta el commit. Sin esto, dos personas que tocan
        // «me apunto» a la vez sobre la última plaza cuentan ambas un cupo libre y
        // entran las dos: el número de la pieza roja pasaría a mentir.
        // En SQLite (tests) no hay FOR UPDATE, pero su escritura ya es exclusiva.
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlAsync($"SELECT 1 FROM shouts WHERE id = {id} FOR UPDATE", ct);

        var s = await db.Shouts
            .Include(x => x.Joins)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El grito no está disponible.");

        EnsureLive(s);

        if (s.Joins.Any(j => j.UserId == userId))
            throw AppException.Conflict("Ya estás apuntado a este grito.");

        if (s.Joins.Count >= s.Slots)
            throw AppException.Conflict("Ya no quedan cupos en este grito.");

        var quien = await db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct)
            ?? throw AppException.Unauthorized("Usuario no encontrado.");

        s.Joins.Add(new ShoutJoin { ShoutId = s.Id, UserId = userId });

        var quedan = s.Slots - s.Joins.Count;
        notifications.Notify(s.AuthorId, "grito.apuntado",
            quedan == 0
                ? $"{quien} se apuntó a «{s.Title}». Ya se llenó."
                : $"{quien} se apuntó a «{s.Title}». Quedan {quedan}.");

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task LeaveAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var s = await db.Shouts
            .Include(x => x.Joins)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El grito no está disponible.");

        // Quien convocó no puede «salirse» de su propio plan: lo que corresponde es
        // cancelarlo, que además avisa a los demás.
        if (s.AuthorId == userId)
            throw AppException.BadRequest("Es tu grito: si ya no vas, cancélalo para avisarle a los demás.");

        var join = s.Joins.FirstOrDefault(j => j.UserId == userId)
            ?? throw AppException.Conflict("No estabas apuntado a este grito.");

        var quien = await db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);

        db.Remove(join);
        notifications.Notify(s.AuthorId, "grito.desapuntado", $"{quien} se salió de «{s.Title}».");

        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(Guid id, ShoutStatusAction action, Guid userId, bool canModerate, CancellationToken ct = default)
    {
        var s = await db.Shouts
            .Include(x => x.Joins)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El grito no está disponible.");

        var isOwner = s.AuthorId == userId;

        // Cerrar y cancelar son decisiones sobre el propio plan; eliminar es la
        // herramienta de moderación, y esa sí alcanza a los gritos ajenos.
        var allowed = action switch
        {
            ShoutStatusAction.Delete => isOwner || canModerate,
            _ => isOwner
        };
        if (!allowed)
            throw AppException.Forbidden("No tienes permiso para cambiar el estado de este grito.");

        if (action == ShoutStatusAction.Cancel)
        {
            foreach (var j in s.Joins.Where(j => j.UserId != s.AuthorId))
                notifications.Notify(j.UserId, "grito.cancelado", $"Se canceló «{s.Title}».");
        }

        s.Status = action switch
        {
            ShoutStatusAction.Close => ShoutStatus.Closed,
            ShoutStatusAction.Cancel => ShoutStatus.Cancelled,
            ShoutStatusAction.Delete => ShoutStatus.Deleted,
            _ => s.Status
        };

        if (canModerate && !isOwner)
            audit.Log(userId, "grito.moderado", "Shout", s.Id.ToString(), action.ToString());

        await db.SaveChangesAsync(ct);
    }

    // ── Helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Estado efectivo. El orden importa: un grito cancelado sigue cancelado aunque
    /// además ya haya pasado la fecha, y lo que necesita leer quien lo mira es que
    /// se canceló, no que venció.
    /// </summary>
    private static ShoutState StateOf(ShoutStatus status, DateTime happensAt, int taken, int slots, DateTime now)
    {
        if (status == ShoutStatus.Cancelled) return ShoutState.Cancelled;
        if (status == ShoutStatus.Closed) return ShoutState.Closed;
        if (happensAt <= now) return ShoutState.Expired;
        if (taken >= slots) return ShoutState.Full;
        return ShoutState.Open;
    }

    /// <summary>Un grito solo admite gente mientras esté abierto y no haya pasado.</summary>
    private static void EnsureLive(Shout s)
    {
        if (s.Status == ShoutStatus.Cancelled) throw AppException.Conflict("Este grito se canceló.");
        if (s.Status != ShoutStatus.Open) throw AppException.Conflict("Este grito ya no está abierto.");
        if (s.HappensAt <= DateTime.UtcNow) throw AppException.Conflict("Este grito ya pasó.");
    }

    /// <summary>
    /// La fecha se guarda en UTC. Un cliente puede mandarla sin zona (Unspecified);
    /// tratarla como local haría que el mismo grito venciera a horas distintas
    /// según dónde corra el servidor.
    /// </summary>
    private static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    /// <summary>
    /// Fila cruda desde la base. El estado efectivo se calcula fuera de la consulta:
    /// la regla tiene ramas que EF no sabe traducir a SQL, y forzarla ahí obligaría
    /// a escribirla de dos formas distintas.
    /// </summary>
    private record Row(
        Guid Id, string Title, string Place, DateTime HappensAt, int Slots, int Taken, decimal? Fee,
        ShoutStatus Status, Guid AuthorId, string AuthorName, bool Joined, DateTime CreatedAt)
    {
        public ShoutSummaryDto ToSummary(Guid userId, DateTime now) => new(
            Id, Title, Place, HappensAt, Slots, Taken, Fee,
            StateOf(Status, HappensAt, Taken, Slots, now),
            AuthorId, AuthorName, Joined, AuthorId == userId, CreatedAt);
    }
}
