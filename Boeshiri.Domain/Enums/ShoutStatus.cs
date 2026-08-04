namespace Boeshiri.Domain.Enums;

/// <summary>
/// Estado de un grito: solo lo que decidió una persona. «Lleno» y «vencido» no
/// están aquí a propósito — se calculan al leer, a partir de los cupos ocupados y
/// de la fecha del plan. Guardarlos exigiría un proceso que los mantuviera al día,
/// y no existe ninguno: un estado almacenado que nadie actualiza se vuelve mentira
/// en cuestión de horas.
/// </summary>
public enum ShoutStatus
{
    Open,

    /// <summary>El autor lo cerró antes de tiempo: ya tiene con quién.</summary>
    Closed,

    /// <summary>
    /// El plan se cayó. Se distingue de <see cref="Closed"/> porque a los apuntados
    /// hay que avisarles: cerrar es buena noticia, cancelar no.
    /// </summary>
    Cancelled,

    Deleted
}
