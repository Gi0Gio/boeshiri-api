namespace Boeshiri.Infrastructure.Common;

/// <summary>
/// Datos personales en los logs: lo justo para reconocer un caso sin dejar la
/// dirección completa de cada persona en un sistema que leen más manos.
/// </summary>
public static class Privacidad
{
    /// <summary>«giovanny@gmail.com» → «g***@gmail.com».</summary>
    public static string OcultarCorreo(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "(vacío)";
        var arroba = email.IndexOf('@');
        if (arroba <= 0)
            return "***";
        return $"{email[0]}***{email[arroba..]}";
    }
}
