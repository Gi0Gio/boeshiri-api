using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Convocatoria abierta (p. ej. «Tallerista para Garabateo ARCANA»): la Junta la
/// arma con sus preguntas y cualquiera la responde desde el sitio público, con o
/// sin cuenta. Puede pertenecer a un evento.
/// </summary>
public class OpenCall
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Title { get; set; }
    public string? Description { get; set; }

    public OpenCallStatus Status { get; set; } = OpenCallStatus.Draft;

    /// <summary>Hasta cuándo se aceptan respuestas (UTC). Null = hasta que se cierre a mano.</summary>
    public DateTime? ClosesAt { get; set; }

    public Guid? EventId { get; set; }
    public Event? Event { get; set; }

    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<OpenCallQuestion> Questions { get; set; } = new List<OpenCallQuestion>();
    public ICollection<OpenCallResponse> Responses { get; set; } = new List<OpenCallResponse>();
}

/// <summary>Pregunta de una convocatoria, en el orden en que se muestra.</summary>
public class OpenCallQuestion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid OpenCallId { get; set; }
    public OpenCall OpenCall { get; set; } = null!;

    public int Order { get; set; }
    public required string Label { get; set; }
    public string? Help { get; set; }
    public OpenCallQuestionType Type { get; set; }
    public bool Required { get; set; }
}
