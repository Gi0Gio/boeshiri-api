using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Respuesta a una convocatoria. Nombre y contacto se guardan aquí aunque quien
/// responda tenga cuenta: la respuesta tiene que seguir legible si la cuenta cambia
/// o se elimina (UserId queda solo como referencia).
/// </summary>
public class OpenCallResponse
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid OpenCallId { get; set; }
    public OpenCall OpenCall { get; set; } = null!;

    /// <summary>Si respondió con sesión iniciada.</summary>
    public Guid? UserId { get; set; }

    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Evaluación de la Junta; la nota es privada y no la ve quien respondió.</summary>
    public OpenCallResponseStatus Status { get; set; } = OpenCallResponseStatus.New;
    public string? Note { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public ICollection<OpenCallAnswer> Answers { get; set; } = new List<OpenCallAnswer>();
}

/// <summary>Lo contestado a una pregunta: texto (texto corto, largo o enlace) o monto.</summary>
public class OpenCallAnswer
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ResponseId { get; set; }
    public OpenCallResponse Response { get; set; } = null!;

    public Guid QuestionId { get; set; }
    public OpenCallQuestion Question { get; set; } = null!;

    public string? Text { get; set; }
    public decimal? Amount { get; set; }
}
