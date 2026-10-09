using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Boeshiri.Api;

/// <summary>
/// Límites de peticiones por IP. Las rutas anónimas (login, registro, reenvío,
/// contacto, tarjetas) no tenían ninguno: se podían probar contraseñas sin fin,
/// inundar buzones o recomponer tarjetas en bucle.
/// </summary>
public static class Limites
{
    public const string Auth = "auth";
    public const string Renovar = "renovar";
    public const string Contacto = "contacto";
    public const string Tarjetas = "tarjetas";

    /// <summary>
    /// IP del cliente. Login, renovación y compartir llegan por el proxy de Netlify,
    /// que deja la IP real en x-nf-client-connection-ip; el resto llega por el borde
    /// de Railway, cuya X-Forwarded-For ya resolvió UseForwardedHeaders. La cabecera
    /// de Netlify se podría falsear llamando a Railway directamente, pero entonces
    /// sigue frenando el límite por cuenta de LoginThrottle.
    /// </summary>
    public static string IpCliente(HttpContext c)
    {
        var netlify = c.Request.Headers["x-nf-client-connection-ip"].ToString();
        if (IPAddress.TryParse(netlify, out var ip))
            return ip.ToString();
        return c.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    }

    private static RateLimitPartition<string> Ventana(HttpContext c, int permitidas, TimeSpan ventana) =>
        RateLimitPartition.GetFixedWindowLimiter(IpCliente(c), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitidas,
            Window = ventana,
            QueueLimit = 0,
        });

    public static IServiceCollection AddLimites(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, ct) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)espera.TotalSeconds).ToString();
                ctx.HttpContext.Response.ContentType = "application/problem+json";
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    title = "Demasiadas peticiones seguidas. Espera un momento y vuelve a intentarlo.",
                    status = 429,
                }, ct);
            };

            // Login, registro, verificación y recuperación: lo que se usa para probar
            // contraseñas o inundar buzones.
            o.AddPolicy(Auth, c => Ventana(c, 10, TimeSpan.FromMinutes(1)));
            // Renovar lo hace cada pestaña abierta: holgado, pero con tope.
            o.AddPolicy(Renovar, c => Ventana(c, 60, TimeSpan.FromMinutes(1)));
            o.AddPolicy(Contacto, c => Ventana(c, 3, TimeSpan.FromMinutes(10)));
            o.AddPolicy(Tarjetas, c => Ventana(c, 60, TimeSpan.FromMinutes(1)));

            // Colchón general contra ráfagas: holgado para una persona usando el panel.
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c =>
                Ventana(c, 600, TimeSpan.FromMinutes(1)));
        });
}
