using Boeshiri.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Resend;

namespace Boeshiri.Infrastructure.Email;

/// <summary>
/// Envío real de correo con Resend (ADR-0003, SDD §7).
///
/// No propaga los fallos: en el registro el usuario ya está guardado cuando se
/// envía el correo, así que dejar escapar la excepción devolvería un 500 sobre una
/// cuenta que sí quedó creada — y el reintento chocaría con el 409 de correo
/// duplicado. Un fallo de correo se registra como error y el alta sigue en pie.
/// </summary>
public class ResendEmailSender(
    IResend resend,
    IOptions<ResendOptions> options,
    ILogger<ResendEmailSender> logger) : IEmailSender
{
    private readonly ResendOptions _options = options.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default)
    {
        var message = new EmailMessage
        {
            From = _options.From,
            To = to,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody
        };

        try
        {
            var resp = await resend.EmailSendAsync(message, ct);
            logger.LogInformation("Correo enviado a {To} (Resend id {Id}): {Subject}", to, resp.Content, subject);
        }
        catch (Exception ex)
        {
            // Se vuelca el cuerpo en el log a propósito: mientras no haya un dominio
            // verificado, Resend solo entrega al correo dueño de la cuenta y el resto
            // falla. Sin este volcado se perdería el enlace de verificación y la
            // persona quedaría sin forma de activar su cuenta.
            logger.LogError(ex,
                "No se pudo enviar el correo a {To} con asunto '{Subject}'. Remitente: {From}. " +
                "Revisa que el dominio esté verificado en Resend y que la API key sea válida.\n" +
                "[CORREO NO ENVIADO — contenido de respaldo]\n{Body}",
                to, subject, _options.From, htmlBody);
        }
    }
}
