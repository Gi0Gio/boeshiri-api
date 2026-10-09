using Boeshiri.Infrastructure.Common;
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

    public async Task SendAsync(string to, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default, string? replyTo = null)
    {
        var message = new EmailMessage
        {
            From = _options.From,
            To = to,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody
        };
        if (!string.IsNullOrWhiteSpace(replyTo))
            message.ReplyTo = replyTo;

        try
        {
            var resp = await resend.EmailSendAsync(message, ct);
            logger.LogInformation("Correo enviado a {To} (Resend id {Id}): {Subject}", Privacidad.OcultarCorreo(to), resp.Content, subject);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El cliente se fue: no es un fallo de envío y no debe volcar el cuerpo
            // del correo al log como si lo fuera.
            throw;
        }
        catch (Exception ex)
        {
            // El cuerpo NO va al log: lleva enlaces de un solo uso (verificar,
            // restablecer contraseña) y quien leyera los logs podría usarlos para
            // entrar en una cuenta ajena. Si falla una verificación, la Junta puede
            // emitir el enlace a mano desde Postulantes.
            logger.LogError(ex,
                "No se pudo enviar el correo a {To} con asunto '{Subject}'. Remitente: {From}. " +
                "Revisa que el dominio esté verificado en Resend y que la API key sea válida.",
                Privacidad.OcultarCorreo(to), subject, _options.From);
        }
    }
}
