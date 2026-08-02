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

    /// <summary>Inicio de sesión: devuelve un JWT con los permisos efectivos.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResult>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await authService.LoginAsync(request, ct));

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
