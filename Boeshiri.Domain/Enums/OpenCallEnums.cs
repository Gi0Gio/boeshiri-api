namespace Boeshiri.Domain.Enums;

/// <summary>Vida de una convocatoria: se prepara, se abre al público y se cierra.</summary>
public enum OpenCallStatus
{
    Draft,
    Open,
    Closed
}

/// <summary>Qué se pregunta. Cada tipo se valida distinto al recibir la respuesta.</summary>
public enum OpenCallQuestionType
{
    /// <summary>Una línea (hasta 300 caracteres).</summary>
    ShortText,
    /// <summary>Un párrafo o más (hasta 4000 caracteres): la propuesta.</summary>
    LongText,
    /// <summary>Un enlace http(s): portafolio, redes, un video.</summary>
    Link,
    /// <summary>Un monto en dólares: honorarios, costo de materiales.</summary>
    Amount
}

/// <summary>Evaluación de una respuesta por la Junta.</summary>
public enum OpenCallResponseStatus
{
    New,
    Shortlisted,
    Accepted,
    Rejected
}
