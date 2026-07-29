using Boeshiri.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Boeshiri.Infrastructure.Email;

/// <summary>
/// Implementación de desarrollo de <see cref="IEmailSender"/>: escribe el correo en
/// el log (incluye el enlace de verificación) para poder probar sin proveedor real.
/// La implementación con Resend la sustituye cuando haya API key (ADR-0003).
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("[DEV EMAIL] Para: {To} | Asunto: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
