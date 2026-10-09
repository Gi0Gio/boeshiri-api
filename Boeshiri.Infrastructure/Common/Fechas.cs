namespace Boeshiri.Infrastructure.Common;

/// <summary>
/// Todas las fechas se guardan en UTC. Un cliente puede mandarlas sin zona
/// (Kind=Unspecified): Npgsql se niega a escribirlas en una columna timestamptz y
/// la petición acababa en 500. Sin zona se interpretan como UTC.
/// </summary>
public static class Fechas
{
    public static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
