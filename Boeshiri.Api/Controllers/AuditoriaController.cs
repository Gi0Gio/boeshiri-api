using Boeshiri.Api.Authorization;
using Boeshiri.Application.Audit;
using Microsoft.AspNetCore.Mvc;

namespace Boeshiri.Api.Controllers;

/// <summary>
/// Historial de auditoría. Visible únicamente para el Super Administrador
/// (RF-AUD-02), vía el permiso <c>auditoria.ver</c>.
/// </summary>
[ApiController]
[Route("admin/auditoria")]
[HasPermission("auditoria.ver")]
public class AuditoriaController(IAuditLogger audit) : ControllerBase
{
    /// <summary>Lista las acciones relevantes registradas (más recientes primero).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> List([FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await audit.QueryAsync(take, ct));
}
