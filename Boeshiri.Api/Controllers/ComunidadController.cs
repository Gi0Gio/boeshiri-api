using Boeshiri.Application.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Comunidad: perfiles públicos de los miembros como portafolio (RF-PUB-09). Cada
/// perfil se muestra respetando las opciones de privacidad del miembro (RF-MEM-03).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("comunidad")]
public class ComunidadController(IProfileService profiles) : ControllerBase
{
    /// <summary>
    /// Perfiles públicos de miembros activos. Con <c>?rol=</c> se acota a quienes
    /// llevan ese rol, que es como el sitio arma la sección de la Junta Directiva.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CommunityMemberDto>>> List([FromQuery] string? rol, CancellationToken ct)
        => Ok(await profiles.ListCommunityAsync(rol, ct));

    /// <summary>Perfil público de un miembro (filtrado por su privacidad).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicProfileDto>> Detail(Guid id, CancellationToken ct)
        => Ok(await profiles.GetPublicProfileAsync(id, ct));
}
