using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Net;
using Boeshiri.Application.Abstractions;
using Boeshiri.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boeshiri.Api.Controllers;

/// <summary>Mensaje del formulario público de contacto (RF-PUB-12).</summary>
public record ContactRequest
{
    [Required, MaxLength(120)]
    public required string Name { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [MaxLength(120)]
    public string? Subject { get; init; }

    [Required, MaxLength(4000)]
    public required string Message { get; init; }
}

/// <summary>
/// Formulario de contacto del sitio público (RF-PUB-12). Anónimo por necesidad.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("contacto")]
public class ContactoController(
    IEmailSender email,
    IOptions<AppOptions> app,
    ILogger<ContactoController> logger) : ControllerBase
{
    /// <summary>
    /// Último envío por IP. Un endpoint anónimo que dispara correos es un cañón de
    /// spam: sin freno, cualquiera inunda el buzón del colectivo y quema la cuota
    /// de Resend. En memoria basta — si el proceso reinicia, se pierde el registro
    /// y como mucho pasa un mensaje de más.
    /// </summary>
    private static readonly ConcurrentDictionary<string, DateTime> UltimoEnvio = new();
    private static readonly TimeSpan Espera = TimeSpan.FromMinutes(1);

    [HttpPost]
    public async Task<IActionResult> Send(ContactRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        var ahora = DateTime.UtcNow;

        if (UltimoEnvio.TryGetValue(ip, out var previo) && ahora - previo < Espera)
        {
            logger.LogInformation("Contacto frenado por espera mínima desde {Ip}", ip);
            return StatusCode(429, new { detail = "Espera un minuto antes de enviar otro mensaje." });
        }
        UltimoEnvio[ip] = ahora;

        var destino = app.Value.ContactEmail;
        if (string.IsNullOrWhiteSpace(destino))
        {
            // Sin buzón configurado el mensaje se perdería en silencio; al menos
            // queda en el log para no perder a quien escribió.
            logger.LogError("Falta App:ContactEmail. Mensaje de {Email}: {Mensaje}", request.Email, request.Message);
            return Ok(new { mensaje = "Mensaje recibido. Te responderemos pronto." });
        }

        var nombre = WebUtility.HtmlEncode(request.Name.Trim());
        var correo = WebUtility.HtmlEncode(request.Email.Trim());
        var asunto = WebUtility.HtmlEncode(request.Subject?.Trim() ?? "Sin asunto");
        var cuerpo = WebUtility.HtmlEncode(request.Message.Trim()).Replace("\n", "<br>");

        await email.SendAsync(
            destino,
            $"Contacto web: {asunto}",
            $"""
             <p><strong>{nombre}</strong> &lt;{correo}&gt; escribió desde el formulario del sitio:</p>
             <p style="white-space:pre-wrap">{cuerpo}</p>
             <hr>
             <p style="color:#666;font-size:12px">Responde directamente a {correo}.</p>
             """,
            $"{request.Name} <{request.Email}> escribió:\n\n{request.Message}\n\n--\nResponde a {request.Email}",
            ct);

        logger.LogInformation("Mensaje de contacto de {Email} enviado a {Destino}", request.Email, destino);
        return Ok(new { mensaje = "Mensaje enviado. Te responderemos pronto." });
    }
}
