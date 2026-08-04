using Boeshiri.Domain.Enums;

namespace Boeshiri.Domain.Entities;

/// <summary>
/// Grito: llamado abierto de un miembro para que otros lo acompañen a algo
/// («¿quién se apunta a la playa?», «faltan manos para montar la expo»). Es la
/// pieza roja del Mural de Explorar y solo se ve con sesión iniciada.
/// </summary>
public class Shout
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public required string Title { get; set; }

    public string? Detail { get; set; }

    /// <summary>Dónde es. Texto libre: «Las Lajas», «cancha de San Mateo».</summary>
    public required string Place { get; set; }

    /// <summary>
    /// Cuándo es. Obligatorio: es lo que hace que el grito caduque solo, y sin
    /// caducidad la pared se llena de planes muertos.
    /// </summary>
    public DateTime HappensAt { get; set; }

    /// <summary>
    /// Plazas totales, incluida la de quien grita: se apunta solo al crearlo, así
    /// que los cupos y los apuntados se cuentan de la misma forma desde el principio.
    /// </summary>
    public int Slots { get; set; }

    /// <summary>
    /// Cuota por persona, informativa. No se conecta con finanzas a propósito: en
    /// cuanto tocara el balance, alguien esperaría que cuadrara con la contabilidad
    /// del colectivo, y esto es plata que se acuerda entre ellos.
    /// </summary>
    public decimal? Fee { get; set; }

    public ShoutStatus Status { get; set; } = ShoutStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }

    public ICollection<ShoutJoin> Joins { get; set; } = new List<ShoutJoin>();
}
