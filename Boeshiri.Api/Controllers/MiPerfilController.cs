using Boeshiri.Api.Authorization;
using Boeshiri.Application.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Perfil del propio miembro: datos, privacidad y redes (RF-MEM-01..05).
/// </summary>
[ApiController]
[Authorize]
[Route("mi")]
public class MiPerfilController(IProfileService profiles) : ControllerBase
{
    /// <summary>Perfil propio completo (vista de edición).</summary>
    [HttpGet("perfil")]
    public async Task<ActionResult<MyProfileDto>> Get(CancellationToken ct)
        => Ok(await profiles.GetMyProfileAsync(User.GetUserId(), ct));

    /// <summary>Actualiza nombre, descripción, disciplina, foto y etiquetas (RF-MEM-01).</summary>
    [HttpPut("perfil")]
    public async Task<IActionResult> Update(UpdateProfileRequest request, CancellationToken ct)
    {
        await profiles.UpdateProfileAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Actualiza las opciones de privacidad (RF-MEM-03).</summary>
    [HttpPut("perfil/privacidad")]
    public async Task<IActionResult> UpdatePrivacy(UpdatePrivacyRequest request, CancellationToken ct)
    {
        await profiles.UpdatePrivacyAsync(User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Reemplaza las redes del perfil (RF-MEM-04/05).</summary>
    [HttpPut("redes")]
    public async Task<IActionResult> UpdateSocialLinks(UpdateSocialLinksRequest request, CancellationToken ct)
    {
        await profiles.UpdateSocialLinksAsync(User.GetUserId(), request, ct);
        return NoContent();
    }
}
