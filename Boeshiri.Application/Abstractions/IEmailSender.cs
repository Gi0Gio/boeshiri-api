namespace Boeshiri.Application.Abstractions;

/// <summary>
/// Envío de correo transaccional. En dev se registra en el log; en producción lo
/// implementa Resend (RF-PUB-13b). La abstracción desacopla el dominio del proveedor.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
