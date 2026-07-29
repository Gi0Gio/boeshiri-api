namespace Boeshiri.Application.Audit;

/// <summary>Entrada de auditoría tal como la consulta el Super Administrador.</summary>
public record AuditEntryDto(
    Guid Id,
    DateTime Timestamp,
    Guid? ActorId,
    string? ActorEmail,
    string Action,
    string ObjectType,
    string? ObjectId,
    string? Metadata);

/// <summary>
/// Registro de auditoría (§11). <see cref="Log"/> encola la entrada en la unidad
/// de trabajo actual (no guarda); la consulta la restringe el permiso
/// <c>auditoria.ver</c> a nivel de endpoint.
/// </summary>
public interface IAuditLogger
{
    /// <summary>Encola una entrada de auditoría. No llama a SaveChanges (lo hace el caller).</summary>
    void Log(Guid? actorId, string action, string objectType, string? objectId = null, string? metadata = null);

    Task<IReadOnlyList<AuditEntryDto>> QueryAsync(int take = 100, CancellationToken ct = default);
}
