using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Boeshiri.Api;

/// <summary>
/// Pasa al español los mensajes de validación que genera el framework. Los propios
/// (AppException) ya están en español; estos salen de los atributos
/// [Required]/[MaxLength]/… y del lector de JSON.
/// </summary>
public static partial class MensajesValidacion
{
    public static string Traducir(ModelError e)
    {
        var m = e.ErrorMessage;
        if (string.IsNullOrEmpty(m) || e.Exception is not null)
            return "El formato no es válido.";

        if (Requerido().IsMatch(m)) return "Es obligatorio.";
        if (Maximo().Match(m) is { Success: true } max) return $"Máximo {max.Groups[1].Value} caracteres.";
        if (Minimo().Match(m) is { Success: true } min) return $"Mínimo {min.Groups[1].Value} caracteres.";
        if (Rango().Match(m) is { Success: true } r) return $"Tiene que estar entre {r.Groups[1].Value} y {r.Groups[2].Value}.";
        if (m.Contains("e-mail address", StringComparison.OrdinalIgnoreCase)) return "No es un correo válido.";
        if (m.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)
            || m.Contains("is invalid", StringComparison.OrdinalIgnoreCase)
            || m.Contains("JSON", StringComparison.Ordinal))
            return "El formato no es válido.";
        if (m.Contains("non-empty request body", StringComparison.OrdinalIgnoreCase)) return "Faltan los datos.";
        return m;
    }

    [GeneratedRegex(@"field is required\.?$")]
    private static partial Regex Requerido();

    [GeneratedRegex(@"maximum length of '?(\d+)'?")]
    private static partial Regex Maximo();

    [GeneratedRegex(@"minimum length of '?(\d+)'?")]
    private static partial Regex Minimo();

    [GeneratedRegex(@"must be between (\S+) and (\S+?)\.?$")]
    private static partial Regex Rango();
}
