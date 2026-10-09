namespace Boeshiri.Application.OpenCalls;

/// <summary>
/// Convocatorias: la Junta las arma y evalúa (permiso convocatorias.gestionar);
/// el público las ve y responde sin cuenta.
/// </summary>
public interface IOpenCallService
{
    // ── Junta ──
    Task<IReadOnlyList<OpenCallSummaryDto>> ListAsync(CancellationToken ct = default);
    Task<OpenCallAdminDto> GetAdminAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid userId, SaveOpenCallRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edita. Con respuestas recibidas no se quitan preguntas ni se cambia su tipo
    /// (las respuestas quedarían huérfanas o mal leídas); sí se reescriben y se añaden.
    /// </summary>
    Task UpdateAsync(Guid id, Guid userId, SaveOpenCallRequest request, CancellationToken ct = default);

    Task ChangeStatusAsync(Guid id, Guid userId, ChangeOpenCallStatusRequest request, CancellationToken ct = default);

    /// <summary>Solo sin respuestas: con respuestas se cierra, para no perder lo recibido.</summary>
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);

    Task ReviewAsync(Guid responseId, Guid userId, ReviewResponseRequest request, CancellationToken ct = default);

    // ── Público ──
    Task<IReadOnlyList<PublicOpenCallDto>> ListOpenAsync(CancellationToken ct = default);

    /// <summary>Una convocatoria abierta o cerrada; un borrador no existe para el público.</summary>
    Task<PublicOpenCallDto> GetPublicAsync(Guid id, CancellationToken ct = default);

    /// <summary>Con <paramref name="userId"/> el nombre y el contacto salen del perfil.</summary>
    Task SubmitAsync(Guid id, Guid? userId, SubmitResponseRequest request, CancellationToken ct = default);
}
