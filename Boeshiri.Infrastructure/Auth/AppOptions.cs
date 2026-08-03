namespace Boeshiri.Infrastructure.Auth;

/// <summary>Ajustes generales de la app (sección "App").</summary>
public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>
    /// URL base del FRONTEND (no de la API). Los enlaces de los correos apuntan aquí
    /// para que el usuario aterrice en una página de la marca y no en el JSON del
    /// endpoint. En producción debe ser el dominio del sitio, p. ej. https://boeshiri.org
    /// </summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>
    /// Buzón que recibe el formulario de contacto (RF-PUB-12). Si está vacío, el
    /// mensaje solo queda en el log.
    /// </summary>
    public string ContactEmail { get; set; } = string.Empty;
}
