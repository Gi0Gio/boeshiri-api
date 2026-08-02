namespace Boeshiri.Application.Admin;

/// <summary>
/// Gestión de postulantes por la Junta / Recursos Humanos (§4.6, §10.1).
/// </summary>
public interface IPostulantesService
{
    /// <summary>Lista los postulantes pendientes (verificados y sin decidir).</summary>
    Task<IReadOnlyList<PostulanteDto>> ListPendingAsync(CancellationToken ct = default);

    /// <summary>
    /// Acepta o rechaza un postulante. Aceptar lo activa y le da el rol Miembro
    /// (RF-PUB-16); rechazar registra la fecha para la espera de 1 mes (RF-PUB-17).
    /// </summary>
    Task DecideAsync(Guid postulanteId, DecisionRequest request, Guid decidedBy, CancellationToken ct = default);

    /// <summary>
    /// Reemite el enlace de verificación y lo DEVUELVE, para entregarlo por otro
    /// canal cuando el correo no llega. Queda en auditoría: entrega una credencial.
    /// </summary>
    Task<VerificationLinkDto> IssueVerificationLinkAsync(Guid postulanteId, Guid actorId, CancellationToken ct = default);
}
