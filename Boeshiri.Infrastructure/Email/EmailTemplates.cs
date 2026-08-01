using System.Net;

namespace Boeshiri.Infrastructure.Email;

/// <summary>
/// Plantillas HTML de los correos transaccionales, con la identidad de Boesh Irí.
///
/// Restricciones del medio, que explican por qué esto no se parece al front:
/// maquetación con &lt;table&gt; y estilos EN LÍNEA (Outlook ignora flex, grid y
/// casi todo &lt;style&gt;); sin SVG ni imágenes remotas (Gmail bloquea SVG y muchos
/// clientes ocultan las imágenes por defecto, así que la marca se construye con
/// tipografía y color); sin webfonts (Oswald/Montserrat no cargan, se imitan con
/// mayúsculas y letter-spacing sobre una pila segura).
/// </summary>
public static class EmailTemplates
{
    // Paleta oficial (index.css del front).
    private const string JungleDeep = "#00110e";
    private const string Jungle = "#002420";
    private const string JungleLine = "#00342c";
    private const string Caribbean = "#00e6bc";
    private const string Tea = "#d9f2c2";
    private const string Cream = "#f6fbef";

    private const string FontDisplay = "'Oswald','Helvetica Neue',Helvetica,Arial,sans-serif";
    private const string FontBody = "'Montserrat','Helvetica Neue',Helvetica,Arial,sans-serif";
    private const string FontMono = "'JetBrains Mono',Consolas,'Courier New',monospace";

    /// <summary>Correo de verificación de registro (RF-PUB-13b).</summary>
    public static string VerificationHtml(string fullName, string verifyUrl)
    {
        var nombre = WebUtility.HtmlEncode(PrimerNombre(fullName));
        var url = WebUtility.HtmlEncode(verifyUrl);

        return Shell(
            preheader: $"Confirma tu correo para completar tu postulación a Boesh Irí.",
            contenido: $"""
                <h1 style="margin:0;font-family:{FontDisplay};font-size:28px;line-height:1.2;font-weight:600;text-transform:uppercase;letter-spacing:1px;color:{Cream};">
                  Confirma tu correo
                </h1>

                <p style="margin:20px 0 0;font-family:{FontBody};font-size:16px;line-height:1.65;color:{Tea};">
                  Hola <strong style="color:{Cream};">{nombre}</strong>, gracias por querer sumarte al colectivo.
                </p>
                <p style="margin:14px 0 0;font-family:{FontBody};font-size:16px;line-height:1.65;color:{Tea};">
                  Solo falta un paso: confirma que esta dirección es tuya. Después, tu postulación
                  pasará a revisión de la Junta.
                </p>

                <!-- Botón: tabla en vez de <a> con padding, que Outlook recorta -->
                <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:32px 0 0;">
                  <tr>
                    <td align="center" bgcolor="{Caribbean}" style="border-radius:999px;">
                      <a href="{url}" style="display:inline-block;padding:16px 38px;font-family:{FontDisplay};font-size:14px;font-weight:600;text-transform:uppercase;letter-spacing:2px;color:{Jungle};text-decoration:none;border-radius:999px;">
                        Verificar mi correo
                      </a>
                    </td>
                  </tr>
                </table>

                <p style="margin:30px 0 0;font-family:{FontBody};font-size:13px;line-height:1.6;color:#8fae86;">
                  ¿El botón no funciona? Copia y pega este enlace en tu navegador:
                </p>
                <p style="margin:8px 0 0;font-family:{FontMono};font-size:12px;line-height:1.5;color:{Caribbean};word-break:break-all;">
                  {url}
                </p>

                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:28px 0 0;">
                  <tr>
                    <td style="padding:14px 18px;background-color:{JungleDeep};border-left:3px solid {Caribbean};border-radius:6px;">
                      <p style="margin:0;font-family:{FontBody};font-size:13px;line-height:1.6;color:{Tea};">
                        El enlace caduca en <strong style="color:{Cream};">24 horas</strong>.
                      </p>
                    </td>
                  </tr>
                </table>
                """,
            pie: "Recibiste este correo porque alguien usó esta dirección para postularse a Boesh Irí. Si no fuiste tú, puedes ignorarlo sin más.");
    }

    /// <summary>Alternativa en texto plano: mejora la entregabilidad y la accesibilidad.</summary>
    public static string VerificationText(string fullName, string verifyUrl) =>
        $"""
        BOESH IRÍ — Colectivo cultural · Chiriquí, Panamá

        Hola {PrimerNombre(fullName)}, gracias por querer sumarte al colectivo.

        Confirma que esta dirección es tuya abriendo este enlace:
        {verifyUrl}

        El enlace caduca en 24 horas. Después, tu postulación pasará a revisión
        de la Junta.

        Recibiste este correo porque alguien usó esta dirección para postularse
        a Boesh Irí. Si no fuiste tú, puedes ignorarlo sin más.
        """;

    /// <summary>
    /// Marco común: fondo, tarjeta, membrete y pie. El contenido llega ya maquetado.
    /// </summary>
    private static string Shell(string preheader, string contenido, string pie) =>
        $"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <meta name="color-scheme" content="dark">
          <title>Boesh Irí</title>
        </head>
        <body style="margin:0;padding:0;background-color:{JungleDeep};">
          <!-- Texto de vista previa: lo muestra la bandeja junto al asunto, sin verse en el cuerpo -->
          <div style="display:none;max-height:0;overflow:hidden;opacity:0;">{WebUtility.HtmlEncode(preheader)}</div>

          <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="background-color:{JungleDeep};">
            <tr>
              <td align="center" style="padding:32px 16px;">

                <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="600" style="width:100%;max-width:600px;background-color:{Jungle};border:1px solid {JungleLine};border-radius:16px;overflow:hidden;">

                  <!-- Filo superior de color: sustituye al logo, que no puede ser SVG -->
                  <tr><td style="height:4px;background-color:{Caribbean};line-height:4px;font-size:0;">&nbsp;</td></tr>

                  <!-- Membrete -->
                  <tr>
                    <td style="padding:36px 40px 0;">
                      <p style="margin:0;font-family:{FontDisplay};font-size:32px;font-weight:600;text-transform:uppercase;letter-spacing:5px;color:{Cream};">
                        Boesh <span style="color:{Caribbean};">Irí</span>
                      </p>
                      <p style="margin:10px 0 0;font-family:{FontMono};font-size:11px;text-transform:uppercase;letter-spacing:3px;color:#6f9a6a;">
                        Colectivo cultural · Chiriquí, Panamá
                      </p>
                      <table role="presentation" cellpadding="0" cellspacing="0" border="0" width="100%" style="margin:28px 0 0;">
                        <tr><td style="height:1px;background-color:{JungleLine};line-height:1px;font-size:0;">&nbsp;</td></tr>
                      </table>
                    </td>
                  </tr>

                  <!-- Contenido -->
                  <tr><td style="padding:32px 40px 40px;">{contenido}</td></tr>

                  <!-- Pie -->
                  <tr>
                    <td style="padding:24px 40px 32px;background-color:{JungleDeep};border-top:1px solid {JungleLine};">
                      <p style="margin:0;font-family:{FontBody};font-size:12px;line-height:1.6;color:#6f9a6a;">
                        {WebUtility.HtmlEncode(pie)}
                      </p>
                      <p style="margin:12px 0 0;font-family:{FontMono};font-size:11px;letter-spacing:1px;color:#4d7049;">
                        BOESH IRÍ · DAVID, CHIRIQUÍ — PANAMÁ
                      </p>
                    </td>
                  </tr>

                </table>

              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    /// <summary>El saludo usa solo el primer nombre; el nombre completo suena a formulario.</summary>
    private static string PrimerNombre(string fullName)
    {
        var limpio = fullName?.Trim();
        if (string.IsNullOrEmpty(limpio)) return "hola";
        var espacio = limpio.IndexOf(' ');
        return espacio > 0 ? limpio[..espacio] : limpio;
    }
}
