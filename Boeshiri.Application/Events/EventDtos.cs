using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Events;

/// <summary>Filtro temporal para el listado de eventos (RF-PUB-10).</summary>
public enum EventWhen
{
    All,
    Upcoming,
    Past
}

/// <summary>Acción de moderación de un evento (RF-EVT-02).</summary>
public enum EventStatusAction
{
    Hide,
    Show,
    Delete
}

public record CreateEventRequest
{
    [Required, MaxLength(80)]
    public required string Category { get; init; }

    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [MaxLength(4000)]
    public string? Description { get; init; }

    /// <summary>En planeación puede faltar la fecha o el costo; confirmado, no.</summary>
    public bool Planning { get; init; }

    /// <summary>Inicio. Null = «fecha por confirmar» (solo en planeación).</summary>
    public DateTime? Date { get; init; }

    /// <summary>Fin, opcional. Tiene que ser después del inicio.</summary>
    public DateTime? EndsAt { get; init; }

    [MaxLength(200)]
    public string? Location { get; init; }

    /// <summary>0 = gratis; null = «por definir» (solo en planeación).</summary>
    [Range(0, 100000)]
    public decimal? Cost { get; init; }

    public Visibility Visibility { get; init; } = Visibility.Public;
    public Guid? ResponsibleId { get; init; }

    public List<string>? Images { get; init; }
}

public record UpdateEventRequest
{
    [Required, MaxLength(80)]
    public required string Category { get; init; }

    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [MaxLength(4000)]
    public string? Description { get; init; }

    /// <summary>En planeación puede faltar la fecha o el costo; confirmado, no.</summary>
    public bool Planning { get; init; }

    /// <summary>Inicio. Null = «fecha por confirmar» (solo en planeación).</summary>
    public DateTime? Date { get; init; }

    /// <summary>Fin, opcional. Tiene que ser después del inicio.</summary>
    public DateTime? EndsAt { get; init; }

    [MaxLength(200)]
    public string? Location { get; init; }

    /// <summary>0 = gratis; null = «por definir» (solo en planeación).</summary>
    [Range(0, 100000)]
    public decimal? Cost { get; init; }

    public Visibility Visibility { get; init; } = Visibility.Public;
    public Guid? ResponsibleId { get; init; }

    /// <summary>Lista final de imágenes (la primera es la portada). Null = dejarlas como están.</summary>
    public List<string>? Images { get; init; }
}

public record ChangeEventStatusRequest
{
    [Required]
    public required EventStatusAction Action { get; init; }
}

/// <summary>Registro de asistencia (RF-EVT-03): conteo total + integrantes participantes.</summary>
public record RecordAttendanceRequest
{
    [Range(0, 1000000)]
    public int Count { get; init; }

    /// <summary>Integrantes cuya participación alimenta su historial (RF-MEM-08).</summary>
    public List<Guid>? MemberIds { get; init; }
}

public record EventSummaryDto(
    Guid Id,
    string Category,
    string Title,
    bool Planning,
    DateTime? Date,
    DateTime? EndsAt,
    string? Location,
    decimal? Cost,
    Visibility Visibility,
    ContentStatus Status,
    int AttendanceCount,
    string? CoverImage);

public record EventDetailDto(
    Guid Id,
    string Category,
    string Title,
    string? Description,
    bool Planning,
    DateTime? Date,
    DateTime? EndsAt,
    string? Location,
    decimal? Cost,
    Visibility Visibility,
    ContentStatus Status,
    Guid? ResponsibleId,
    string? ResponsibleName,
    int AttendanceCount,
    IReadOnlyList<string> Images);

/// <summary>Evento del historial del miembro (RF-MEM-08).</summary>
public record MyEventDto(Guid Id, string Title, string Category, DateTime? Date);
