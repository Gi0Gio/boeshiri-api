using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.OpenCalls;

// ── Junta: armar la convocatoria ─────────────────────────────────

/// <summary>Pregunta al guardar. Con Id edita una existente; sin Id es nueva.</summary>
public record QuestionInput
{
    public Guid? Id { get; init; }

    [Required, MaxLength(200)]
    public required string Label { get; init; }

    [MaxLength(500)]
    public string? Help { get; init; }

    public OpenCallQuestionType Type { get; init; } = OpenCallQuestionType.ShortText;

    public bool Required { get; init; }
}

/// <summary>Alta o edición de una convocatoria. Las preguntas van en el orden de la lista.</summary>
public record SaveOpenCallRequest
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }

    [MaxLength(8000)]
    public string? Description { get; init; }

    public Guid? EventId { get; init; }

    public DateTime? ClosesAt { get; init; }

    [Required, MaxLength(20)]
    public required List<QuestionInput> Questions { get; init; }
}

public record ChangeOpenCallStatusRequest
{
    public required OpenCallStatus Status { get; init; }
}

/// <summary>Evaluación de una respuesta: estado y nota privada.</summary>
public record ReviewResponseRequest
{
    public required OpenCallResponseStatus Status { get; init; }

    [MaxLength(2000)]
    public string? Note { get; init; }
}

public record QuestionDto(Guid Id, string Label, string? Help, OpenCallQuestionType Type, bool Required);

public record AnswerDto(Guid QuestionId, string? Text, decimal? Amount);

public record ResponseDto(
    Guid Id,
    Guid? UserId,
    bool IsMember,
    string Name,
    string Email,
    string? Phone,
    DateTime CreatedAt,
    OpenCallResponseStatus Status,
    string? Note,
    IReadOnlyList<AnswerDto> Answers);

/// <summary>Convocatoria en el listado de la Junta, con cuántas respuestas esperan revisión.</summary>
public record OpenCallSummaryDto(
    Guid Id,
    string Title,
    OpenCallStatus Status,
    DateTime? ClosesAt,
    bool IsOpen,
    Guid? EventId,
    string? EventTitle,
    int ResponseCount,
    int NewCount,
    DateTime CreatedAt);

/// <summary>Convocatoria completa para la Junta: preguntas y respuestas.</summary>
public record OpenCallAdminDto(
    Guid Id,
    string Title,
    string? Description,
    OpenCallStatus Status,
    DateTime? ClosesAt,
    bool IsOpen,
    Guid? EventId,
    string? EventTitle,
    IReadOnlyList<QuestionDto> Questions,
    IReadOnlyList<ResponseDto> Responses);

// ── Público: ver y responder ─────────────────────────────────────

public record PublicOpenCallDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime? ClosesAt,
    bool IsOpen,
    string? EventTitle,
    DateTime? EventDate,
    IReadOnlyList<QuestionDto> Questions);

public record AnswerInput
{
    public required Guid QuestionId { get; init; }

    [MaxLength(4000)]
    public string? Text { get; init; }

    public decimal? Amount { get; init; }
}

/// <summary>
/// Respuesta del formulario público. Nombre y correo solo hacen falta sin sesión:
/// con sesión se toman del perfil. «Website» es una trampa para robots: el campo
/// está oculto, una persona lo deja vacío.
/// </summary>
public record SubmitResponseRequest
{
    [MaxLength(120)]
    public string? Name { get; init; }

    [EmailAddress, MaxLength(320)]
    public string? Email { get; init; }

    [MaxLength(32)]
    public string? Phone { get; init; }

    [MaxLength(30)]
    public List<AnswerInput> Answers { get; init; } = [];

    [MaxLength(200)]
    public string? Website { get; init; }
}
