using Boeshiri.Infrastructure.Common;
using Boeshiri.Application.Abstractions;
using Boeshiri.Infrastructure.Storage;
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
    IAuditLogger audit,
    IFileStorage storage) : IEventService
{
    public async Task<IReadOnlyList<EventSummaryDto>> ListPublicAsync(EventWhen when, bool includeMembersOnly, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = db.Events.Where(e => e.Status == ContentStatus.Published);
        if (!includeMembersOnly)
            query = query.Where(e => e.Visibility == Visibility.Public);

        query = when switch
        {
            // Sin fecha todavía (en planeación) cuenta como próximo, después de los fechados.
            EventWhen.Upcoming => query.Where(e => e.Date == null || e.Date >= now).OrderBy(e => e.Date == null).ThenBy(e => e.Date),
            EventWhen.Past => query.Where(e => e.Date != null && e.Date < now).OrderByDescending(e => e.Date),
            _ => query.OrderBy(e => e.Date != null).ThenByDescending(e => e.Date)
        };

        return await query.Select(ToSummary).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<EventSummaryDto>> ListManageAsync(EventWhen when, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = db.Events.Where(e => e.Status != ContentStatus.Deleted);

        query = when switch
        {
            // Sin fecha todavía (en planeación) cuenta como próximo, después de los fechados.
            EventWhen.Upcoming => query.Where(e => e.Date == null || e.Date >= now).OrderBy(e => e.Date == null).ThenBy(e => e.Date),
            EventWhen.Past => query.Where(e => e.Date != null && e.Date < now).OrderByDescending(e => e.Date),
            _ => query.OrderBy(e => e.Date != null).ThenByDescending(e => e.Date)
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

        return await ToDetailAsync(e, ct);
    }

    public async Task<EventDetailDto> GetManageDetailAsync(Guid id, CancellationToken ct = default)
    {
        var e = await db.Events.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id && x.Status != ContentStatus.Deleted, ct)
            ?? throw AppException.NotFound("El evento no está disponible.");

        return await ToDetailAsync(e, ct);
    }

    private async Task<EventDetailDto> ToDetailAsync(Event e, CancellationToken ct)
    {
        var responsibleName = e.ResponsibleId is null
            ? null
            : await db.Users.Where(u => u.Id == e.ResponsibleId).Select(u => u.FullName).FirstOrDefaultAsync(ct);

        return new EventDetailDto(
            e.Id, e.Category, e.Title, e.Description, e.Planning, e.Date, e.EndsAt, e.Location, e.Cost,
            e.Visibility, e.Status, e.ResponsibleId, responsibleName, e.AttendanceCount,
            e.Images.OrderBy(i => i.Order).Select(i => i.Url).ToList());
    }

    public async Task<Guid> CreateAsync(Guid userId, CreateEventRequest request, CancellationToken ct = default)
    {
        if ((request.Images?.Count ?? 0) > 4)
            throw AppException.BadRequest("Máximo 4 imágenes por evento.");
        ArchivosGuard.ExigirPropias(storage, request.Images, null, ArchivosGuard.CarpetasImagen, "Imágenes");
        if (request.ResponsibleId is Guid resp)
            await Personas.ExigirActivosAsync(db, [resp], "Responsable", ct);
        ValidarCuandoYCuanto(request.Planning, request.Date, request.EndsAt, request.Cost);

        var ev = new Event
        {
            Category = request.Category.Trim(),
            Title = request.Title.Trim(),
            Description = request.Description,
            Planning = request.Planning,
            Date = request.Date is DateTime d ? Fechas.Utc(d) : null,
            EndsAt = request.EndsAt is DateTime f ? Fechas.Utc(f) : null,
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
        var ev = await db.Events.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw AppException.NotFound("El evento no existe.");

        if (request.ResponsibleId is Guid resp && resp != ev.ResponsibleId)
            await Personas.ExigirActivosAsync(db, [resp], "Responsable", ct);
        ValidarCuandoYCuanto(request.Planning, request.Date, request.EndsAt, request.Cost);
        if ((request.Images?.Count ?? 0) > 4)
            throw AppException.BadRequest("Máximo 4 imágenes por evento.");
        ArchivosGuard.ExigirPropias(storage, request.Images, ev.Images.Select(i => i.Url), ArchivosGuard.CarpetasImagen, "Imágenes");

        ev.Category = request.Category.Trim();
        ev.Title = request.Title.Trim();
        ev.Description = request.Description;
        ev.Planning = request.Planning;
        ev.Date = request.Date is DateTime d ? Fechas.Utc(d) : null;
        ev.EndsAt = request.EndsAt is DateTime f ? Fechas.Utc(f) : null;
        ev.Location = request.Location;
        ev.Cost = request.Cost;
        ev.Visibility = request.Visibility;
        ev.ResponsibleId = request.ResponsibleId;

        // Fotos: antes solo se podían poner al crear. Se compara la lista final con
        // las actuales (como en publicaciones) y lo que sale se borra del bucket.
        var eliminadas = new List<string>();
        if (request.Images is not null)
        {
            var actuales = ev.Images.ToList();
            eliminadas = actuales.Select(i => i.Url).Except(request.Images).ToList();
            foreach (var img in actuales.Where(i => eliminadas.Contains(i.Url)))
                db.EventImages.Remove(img);
            var order = 0;
            foreach (var url in request.Images)
            {
                var existente = actuales.FirstOrDefault(i => i.Url == url);
                if (existente is not null) existente.Order = order++;
                else db.EventImages.Add(new EventImage { EventId = ev.Id, Url = url, Order = order++ });
            }
        }

        await db.SaveChangesAsync(ct);
        foreach (var url in eliminadas)
            await ArchivosGuard.BorrarSiSinUsoAsync(db, storage, url, ct);
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

        if (ev.Date is null)
            throw AppException.BadRequest("Ponle fecha al evento antes de registrar la asistencia.");

        var nuevos = (request.MemberIds ?? []).Distinct().Where(id => ev.Attendees.All(a => a.UserId != id)).ToList();
        await Personas.ExigirActivosAsync(db, nuevos, "Asistentes", ct);

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

    /// <summary>
    /// En planeación puede faltar fecha o costo; confirmado, no. El fin, si lo hay,
    /// necesita inicio y va después de él.
    /// </summary>
    private static void ValidarCuandoYCuanto(bool planning, DateTime? inicio, DateTime? fin, decimal? costo)
    {
        if (!planning)
        {
            var faltan = new List<string>();
            if (inicio is null) faltan.Add("la fecha");
            if (costo is null) faltan.Add("el costo");
            if (faltan.Count > 0)
                throw AppException.BadRequest($"Para confirmarlo falta {string.Join(" y ", faltan)}. Déjalo en planeación mientras tanto.");
        }
        if (fin is not null && inicio is null)
            throw AppException.BadRequest("Pon la fecha de inicio antes que la hora de fin.");
        if (fin is not null && inicio is not null && Fechas.Utc(fin.Value) <= Fechas.Utc(inicio.Value))
            throw AppException.BadRequest("La hora de fin tiene que ser después del inicio.");
    }

    private static readonly Expression<Func<Event, EventSummaryDto>> ToSummary = e => new EventSummaryDto(
        e.Id, e.Category, e.Title, e.Planning, e.Date, e.EndsAt, e.Location, e.Cost, e.Visibility, e.Status, e.AttendanceCount,
        e.Images.OrderBy(i => i.Order).Select(i => i.Url).FirstOrDefault());
}
