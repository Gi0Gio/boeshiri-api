namespace Boeshiri.Application.Events;

/// <summary>
/// Eventos (§7.3). La lectura pública respeta visibilidad/estado (RF-PUB-10/11,
/// RF-PUB-18/19/20); la gestión (crear, editar, ocultar, asistencia) exige el
/// permiso global <c>eventos.gestionar</c> (RF-EVT-02, RF-ADM-06).
/// </summary>
public interface IEventService
{
    Task<IReadOnlyList<EventSummaryDto>> ListPublicAsync(EventWhen when, bool includeMembersOnly, CancellationToken ct = default);

    /// <summary>Listado de gestión: todos los eventos vivos (incl. ocultos) para quien administra (RF-EVT-02).</summary>
    Task<IReadOnlyList<EventSummaryDto>> ListManageAsync(EventWhen when, CancellationToken ct = default);

    /// <summary>Detalle para gestión (sin filtro de estado/visibilidad); para editar aunque esté oculto.</summary>
    Task<EventDetailDto> GetManageDetailAsync(Guid id, CancellationToken ct = default);

    Task<EventDetailDto> GetDetailAsync(Guid id, bool authenticated, CancellationToken ct = default);

    Task<Guid> CreateAsync(Guid userId, CreateEventRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateEventRequest request, CancellationToken ct = default);

    Task ChangeStatusAsync(Guid id, EventStatusAction action, Guid userId, CancellationToken ct = default);

    /// <summary>Registra el conteo de asistencia y a los integrantes participantes (RF-EVT-03).</summary>
    Task RecordAttendanceAsync(Guid id, RecordAttendanceRequest request, Guid userId, CancellationToken ct = default);

    /// <summary>Historial de eventos en los que participó el usuario (RF-MEM-08).</summary>
    Task<IReadOnlyList<MyEventDto>> ListMyHistoryAsync(Guid userId, CancellationToken ct = default);
}
