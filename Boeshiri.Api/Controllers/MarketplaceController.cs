using Boeshiri.Api.Authorization;
using Boeshiri.Application.Marketplace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Marketplace (§9). Catálogo público (RF-MKT-01/02); alta y gestión por el
/// miembro (RF-MKT-03/04/05); moderación con <c>productos.moderar</c> (RF-MKT-07/08).
/// </summary>
[ApiController]
[Route("marketplace")]
public class MarketplaceController(IMarketplaceService marketplace) : ControllerBase
{
    /// <summary>Catálogo público, filtrable por nombre y categoría (RF-MKT-01).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductSummaryDto>>> List([FromQuery] string? nombre, [FromQuery] string? categoria, CancellationToken ct)
        => Ok(await marketplace.ListPublicAsync(nombre, categoria, ct));

    /// <summary>Productos propios (todos los estados).</summary>
    [Authorize]
    [HttpGet("mios")]
    public async Task<ActionResult<IReadOnlyList<ProductSummaryDto>>> Mine(CancellationToken ct)
        => Ok(await marketplace.ListMineAsync(User.GetUserId(), ct));

    /// <summary>Cola de moderación: todos los productos vivos (RF-MKT-07/08).</summary>
    [HasPermission("productos.moderar")]
    [HttpGet("moderacion")]
    public async Task<ActionResult<IReadOnlyList<ProductSummaryDto>>> Moderation(CancellationToken ct)
        => Ok(await marketplace.ListForModerationAsync(ct));

    /// <summary>Detalle con los datos de contacto del vendedor (RF-MKT-02).</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductDetailDto>> Detail(Guid id, CancellationToken ct)
        => Ok(await marketplace.GetDetailAsync(id, ct));

    /// <summary>Enlace + imagen para compartir en redes (RF-MKT-05).</summary>
    [HttpGet("{id:guid}/compartir")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductShareDto>> Share(Guid id, CancellationToken ct)
        => Ok(await marketplace.GetShareAsync(id, ct));

    /// <summary>Alta del miembro en el marketplace (RF-MKT-03).</summary>
    [HasPermission("marketplace.gestionar_propio")]
    [HttpPost("alta")]
    public async Task<IActionResult> Enroll(CancellationToken ct)
    {
        await marketplace.EnrollAsync(User.GetUserId(), ct);
        return Ok(new { mensaje = "Te diste de alta en el marketplace." });
    }

    /// <summary>Publica un producto (RF-MKT-04).</summary>
    [HasPermission("marketplace.gestionar_propio")]
    [HttpPost]
    public async Task<ActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        var id = await marketplace.CreateAsync(User.GetUserId(), request, ct);
        return CreatedAtAction(nameof(Detail), new { id }, new { id });
    }

    /// <summary>Edita un producto propio (RF-MKT-04).</summary>
    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        await marketplace.UpdateAsync(id, User.GetUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Ocultar / mostrar / vender / eliminar (RF-MKT-05/08).</summary>
    [Authorize]
    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeProductStatusRequest request, CancellationToken ct)
    {
        var canModerate = User.HasPermission("productos.moderar");
        await marketplace.ChangeStatusAsync(id, request.Action, User.GetUserId(), canModerate, ct);
        return NoContent();
    }
}
