namespace Boeshiri.Infrastructure.Email;

/// <summary>
/// Configuración de Resend (sección "Resend"). La clave va en secrets/env, nunca
/// en el repo. Si falta <see cref="ApiKey"/>, el correo cae al emisor de
/// desarrollo que solo escribe en el log (ADR-0003).
/// </summary>
public class ResendOptions
{
    public const string SectionName = "Resend";

    /// <summary>API key de Resend (empieza por "re_").</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Remitente, en forma "Nombre &lt;correo@dominio&gt;". El dominio debe estar
    /// verificado en Resend; <c>onboarding@resend.dev</c> sirve para pruebas pero
    /// solo entrega al correo dueño de la cuenta de Resend.
    /// </summary>
    public string From { get; set; } = "Boesh Irí <onboarding@resend.dev>";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
