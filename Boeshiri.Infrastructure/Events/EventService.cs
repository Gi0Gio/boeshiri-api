using System.Linq.Expressions;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Events;
using Boeshiri.Application.Notifications;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Events;

/// <summary>Eventos (§7.3): lectura pública con visibilidad, gestión y asistencia con auditoría.</summary>
public class EventService(
    BoeshiriDbContext db,
    INotificationService notifications,
    IAuditLogger audit) : IEventService
{
    public async Task<IReadOnlyList<EventSummaryDto>> ListPublicAsync(EventWhen when, bool includeMembersOnly, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = db.Events.Where(e => e.Status == ContentStatus.Published);
        if (!includeMembersOnly)
            query = query.Where(e => e.Visibility == Visibility.Public);

        query = when switch
        {
            EventWhen.Upcoming => query.Where(e => e.Date >= now).OrderBy(e => e.Date),
            EventWhen.Past => query.Where(e => e.Date < now).OrderByDescending(e => e.Date),
            _ => query.OrderByDescending(e => e.Date)
        };

        return await query.Select(ToSummary).ToListAsync(ct);
    }

    public async Task<EventDetailDto> GetDetailAsync(Guid id, bool authenticated, CancellationToken ct = default)
    {
        var e = await db.Events.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id, ct);

        if (e is null || e.Status != ContentStatus.Published)
            throw AppException.NotFound("El evento no está disponible.");

        if (e.Visibility == Visibility.Members && !authenticated)
            throw AppException.Unauthorized("Este evento es exclusivo para miembros. Inicia sesión para verlo.");

        var responsibleName = e.ResponsibleId is null
            ? null
            : await db.Users.Where(u => u.Id == e.ResponsibleId).Select(u => u.FullName).FirstOrDefaultAsync(ct);

        return new EventDetailDto(
            e.Id, e.Category, e.Title, e.Description, e.Date, e.Location, e.Cost,
            e.Visibility, e.Status, e.ResponsibleId, responsibleName, e.AttendanceCount,
            e.Images.OrderBy(i => i.Order).Select(i => i.Url).ToList());
    }

    public async Task<Guid> CreateAsync(Guid userId, CreateEventRequest request, CancellationToken ct = default)
    {
        if ((request.Images?.Count ?? 0) > 4)
            throw AppException.BadRequest("Máximo 4 imágenes por evento.");

        var ev = new Event
        {
            Category = request.Category.Trim(),
            Title = request.Title.Trim(),
            Description = request.Description,
            Date = request.Date,
            Location = request.Location,
            Cost = request.Cost,
            Visibility = request.Visibility,
            ResponsibleId = request.ResponsibleId,
            CreatedBy = userId
        };

        var order = 0;
        foreach (var url in request.Images ?? [])
            ev.Images.Add(new EventImage { Url = url, Order = order++ });

        db.Events.Add(ev);
        audit.Log(userId, "evento.creado", "Event", ev.Id.ToString(), ev.Title);
        await db.SaveChangesAsync(ct);
        return ev.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken ct = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El evento no existe.");

        ev.Category = request.Category.Trim();
        ev.Title = request.Title.Trim();
        ev.Description = request.Description;
        ev.Date = request.Date;
        ev.Location = request.Location;
        ev.Cost = request.Cost;
        ev.Visibility = request.Visibility;
        ev.ResponsibleId = request.ResponsibleId;

        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(Guid id, EventStatusAction action, Guid userId, CancellationToken ct = default)
    {
        var ev = await db.Events.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El evento no existe.");

        ev.Status = action switch
        {
            EventStatusAction.Hide => ContentStatus.Hidden,
            EventStatusAction.Show => ContentStatus.Published,
            EventStatusAction.Delete => ContentStatus.Deleted,
            _ => ev.Status
        };

        audit.Log(userId, "evento.moderado", "Event", ev.Id.ToString(), action.ToString());
        await db.SaveChangesAsync(ct);
    }

    public async Task RecordAttendanceAsync(Guid id, RecordAttendanceRequest request, Guid userId, CancellationToken ct = default)
    {
        var ev = await db.Events.Include(x => x.Attendees).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El evento no existe.");

        ev.AttendanceCount = request.Count;

        foreach (var memberId in (request.MemberIds ?? []).Distinct())
        {
            if (ev.Attendees.Any(a => a.UserId == memberId))
                continue;

            ev.Attendees.Add(new EventAttendee { EventId = ev.Id, UserId = memberId });
            notifications.Notify(memberId, "evento.asistencia", $"Se registró tu participación en el evento «{ev.Title}».");
        }

        audit.Log(userId, "evento.asistencia", "Event", ev.Id.ToString(), $"count={request.Count}");
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MyEventDto>> ListMyHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.EventAttendees
            .Where(a => a.UserId == userId && a.Event.Status != ContentStatus.Deleted)
            .OrderByDescending(a => a.Event.Date)
            .Select(a => new MyEventDto(a.EventId, a.Event.Title, a.Event.Category, a.Event.Date))
            .ToListAsync(ct);
    }

    private static readonly Expression<Func<Event, EventSummaryDto>> ToSummary = e => new EventSummaryDto(
        e.Id, e.Category, e.Title, e.Date, e.Location, e.Cost, e.Visibility, e.Status, e.AttendanceCount,
        e.Images.OrderBy(i => i.Order).Select(i => i.Url).FirstOrDefault());
}
