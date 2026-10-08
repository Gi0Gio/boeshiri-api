using Boeshiri.Application.Auth;
using Boeshiri.Application.Common;
using Boeshiri.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Endpoints de autenticación: registro con verificación de correo, login y
/// consulta de la sesión (§4.6, §7 del SDD).
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService, IOptions<AppOptions> app) : ControllerBase
{
    /// <summary>
    /// Cookie del token de renovación. HttpOnly para que un script inyectado no pueda
    /// leerla, y limitada a /auth para que no viaje en el resto de peticiones.
    /// El front llama a estos endpoints por el proxy de Netlify, bajo su propio
    /// dominio: así la cookie es de primera parte y Safari no la bloquea.
    /// </summary>
    private const string SessionCookie = "boeshiri_sesion";
    private const string SessionCookiePath = "/auth";

    /// <summary>Registro / postulación (RF-PUB-13/13b).</summary>
    [HttpPost("registro")]
    public async Task<ActionResult<RegisterResult>> Register(RegisterRequest request, CancellationToken ct)
        => Ok(await authService.RegisterAsync(request, ct));

    /// <summary>
    /// Verificación de correo desde el enlace (RF-PUB-13b).
    ///
    /// Si lo abre un navegador, redirige a la página del front en vez de mostrar
    /// JSON crudo: hay enlaces repartidos que apuntan aquí directamente, y quien
    /// acaba de registrarse no debería aterrizar en una respuesta de API. El front
    /// llama con Accept: application/json y sigue recibiendo JSON.
    /// </summary>
    [HttpGet("verificar")]
    public async Task<IActionResult> Verify([FromQuery] string token, CancellationToken ct)
    {
        var esNavegacion = Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);

        try
        {
            await authService.VerifyEmailAsync(token, ct);
        }
        catch (AppException) when (esNavegacion)
        {
            return Redirect($"{FrontBaseUrl}/verificar?estado=invalido");
        }

        return esNavegacion
            ? Redirect($"{FrontBaseUrl}/verificar?estado=ok")
            : Ok(new { mensaje = "Correo verificado. Ya puedes iniciar sesión." });
    }

    private string FrontBaseUrl => app.Value.PublicBaseUrl.TrimEnd('/');

    /// <summary>Reenvía el enlace de verificación (RF-PUB-13b).</summary>
    [HttpPost("reenviar-verificacion")]
    public async Task<IActionResult> ResendVerification(ResendVerificationRequest request, CancellationToken ct)
    {
        await authService.ResendVerificationAsync(request.Email, ct);

        // Respuesta idéntica exista o no la cuenta: si variara, cualquiera podría
        // usar este endpoint para averiguar quién está registrado.
        return Ok(new { mensaje = "Si esa dirección tiene una cuenta sin verificar, te enviamos un enlace nuevo. Revisa tu correo." });
    }

    /// <summary>
    /// Inicio de sesión: devuelve un JWT con los permisos efectivos y deja la cookie
    /// de renovación.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResult>> Login(LoginRequest request, CancellationToken ct)
    {
        var session = await authService.LoginAsync(request, ct);
        SetSessionCookie(session);
        return Ok(session.Auth);
    }

    /// <summary>Canjea la cookie de renovación por un JWT nuevo.</summary>
    [HttpPost("renovar")]
    public async Task<ActionResult<AuthResult>> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[SessionCookie];
        if (string.IsNullOrEmpty(refreshToken))
            throw AppException.Unauthorized("Necesitas iniciar sesión.");

        var session = await authService.RefreshAsync(refreshToken, ct);
        SetSessionCookie(session);
        return Ok(session.Auth);
    }

    /// <summary>Cierra la sesión: revoca el token y borra la cookie.</summary>
    [HttpPost("salir")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[SessionCookie];
        if (!string.IsNullOrEmpty(refreshToken))
            await authService.LogoutAsync(refreshToken, ct);

        Response.Cookies.Delete(SessionCookie, SessionCookieOptions(expires: null));
        return NoContent();
    }

    private void SetSessionCookie(SessionResult session)
    {
        // Sin token nuevo, la cookie que ya tiene el navegador es la vigente.
        if (session.RefreshToken is null)
            return;

        Response.Cookies.Append(SessionCookie, session.RefreshToken, SessionCookieOptions(session.RefreshExpiresAt));
    }

    // Secure siempre: Railway termina el TLS en el borde, así que Request.IsHttps es
    // falso aunque el navegador hable HTTPS. Los navegadores aceptan cookies Secure
    // en http://localhost, de modo que en desarrollo también funciona.
    private static CookieOptions SessionCookieOptions(DateTime? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = SessionCookiePath,
        Expires = expires,
        IsEssential = true
    };

    /// <summary>Estado de la sesión y de la solicitud del usuario actual (RF-PUB-16).</summary>
    [Authorize]
    [HttpGet("yo")]
    public async Task<ActionResult<MeResult>> Me(CancellationToken ct)
    {
        var sub = User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized();

        return Ok(await authService.GetMeAsync(userId, ct));
    }
}
