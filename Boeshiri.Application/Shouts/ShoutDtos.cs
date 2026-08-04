using System.ComponentModel.DataAnnotations;

namespace Boeshiri.Application.Shouts;

/// <summary>
/// Estado tal como lo pinta la interfaz: mezcla lo que decidió el autor con lo que
/// dictan el reloj y los cupos. Se resuelve en el servidor para que ni la web ni
/// un futuro cliente móvil tengan que volver a implementar la regla por su cuenta
/// —y llegar a conclusiones distintas.
/// </summary>
public enum ShoutState
{
    Open,
    Full,
    Expired,
    Closed,
    Cancelled
}

public record CreateShoutRequest
{
    [Required, MaxLength(160)]
    public required string Title { get; init; }

    [MaxLength(1000)]
    public string? Detail { get; init; }

    [Required, MaxLength(200)]
    public required string Place { get; init; }

    [Required]
    public DateTime HappensAt { get; init; }

    /// <summary>
    /// Mínimo 2: un grito es una llamada a que alguien te acompañe, y con una sola
    /// plaza —la de quien grita— no hay a quién llamar.
    /// </summary>
    [Range(2, 100)]
    public int Slots { get; init; }

    [Range(0, 10000)]
    public decimal? Fee { get; init; }
}

public record UpdateShoutRequest
{
    [Required, MaxLength(160)]
    public required string Title { get; init; }

    [MaxLength(1000)]
    public string? Detail { get; init; }

    [Required, MaxLength(200)]
    public required string Place { get; init; }

    [Required]
    public DateTime HappensAt { get; init; }

    [Range(2, 100)]
    public int Slots { get; init; }

    [Range(0, 10000)]
    public decimal? Fee { get; init; }
}

/// <summary>Acciones del autor (cerrar, cancelar) y de la moderación (eliminar).</summary>
public enum ShoutStatusAction
{
    Close,
    Cancel,
    Delete
}

public record ChangeShoutStatusRequest
{
    [Required]
    public required ShoutStatusAction Action { get; init; }
}

public record ShoutSummaryDto(
    Guid Id,
    string Title,
    string Place,
    DateTime HappensAt,
    int Slots,
    int Taken,
    decimal? Fee,
    ShoutState State,
    Guid AuthorId,
    string AuthorName,
    // Joined: ¿quien pregunta ya está apuntado? Decide si el botón dice «me apunto» o «me salgo».
    bool Joined,
    bool Mine,
    DateTime CreatedAt);

public record ShoutMemberDto(Guid Id, string FullName, string? PhotoUrl);

public record ShoutDetailDto(
    Guid Id,
    string Title,
    string? Detail,
    string Place,
    DateTime HappensAt,
    int Slots,
    int Taken,
    decimal? Fee,
    ShoutState State,
    Guid AuthorId,
    string AuthorName,
    bool Joined,
    bool Mine,
    DateTime CreatedAt,
    IReadOnlyList<ShoutMemberDto> Members);

/// <summary>
/// Lo único que sale sin sesión: cuántos gritos hay abiertos. Alcanza para que el
/// mural anónimo muestre las piezas veladas con «inicia sesión para ver» sin que
/// ningún dato de un miembro —ni el título, que suele llevar el plan— salga del
/// servidor.
/// </summary>
public record ShoutTeaserDto(int Open);
