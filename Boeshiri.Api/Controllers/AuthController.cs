using Boeshiri.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Endpoints de autenticación: registro con verificación de correo, login y
/// consulta de la sesión (§4.6, §7 del SDD).
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    /// <summary>Registro / postulación (RF-PUB-13/13b).</summary>
    [HttpPost("registro")]
    public async Task<ActionResult<RegisterResult>> Register(RegisterRequest request, CancellationToken ct)
        => Ok(await authService.RegisterAsync(request, ct));

    /// <summary>Verificación de correo desde el enlace (RF-PUB-13b).</summary>
    [HttpGet("verificar")]
    public async Task<IActionResult> Verify([FromQuery] string token, CancellationToken ct)
    {
        await authService.VerifyEmailAsync(token, ct);
        return Ok(new { mensaje = "Correo verificado. Ya puedes iniciar sesión." });
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
