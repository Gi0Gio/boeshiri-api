namespace Boeshiri.Application.Abstractions;

/// <summary>
/// Envío de correo transaccional. En dev se registra en el log; en producción lo
/// implementa Resend (RF-PUB-13b). La abstracción desacopla el dominio del proveedor.
/// </summary>
public interface IEmailSender
{
    /// <param name="textBody">
    /// Alternativa en texto plano. Opcional, pero conviene enviarla: los filtros
    /// antispam penalizan los correos solo-HTML y algunos clientes no renderizan HTML.
    /// </param>
    Task SendAsync(string to, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default);
}
