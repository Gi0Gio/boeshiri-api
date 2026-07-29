namespace Boeshiri.Infrastructure.Auth;

/// <summary>Ajustes generales de la app (sección "App").</summary>
public class AppOptions
{
    public const string SectionName = "App";

    /// <summary>Base pública para construir enlaces (p. ej. el de verificación).</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:8080";
}
