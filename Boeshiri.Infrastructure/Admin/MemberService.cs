using Boeshiri.Application.Admin;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Notifications;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Boeshiri.Infrastructure.Admin;

/// <summary>
/// Cambios de estado de miembro (RF-ADM-03). El estado es ortogonal a los roles y
/// actúa como compuerta de sesión (Catálogo de Permisos §3): Suspendido, Retirado
/// y Expulsado impiden iniciar sesión. Cada cambio deja aviso in-app y auditoría,
/// atómicos con el cambio.
/// </summary>
public class MemberService(
    BoeshiriDbContext db,
    INotificationService notifications,
    IAuditLogger audit,
    ILogger<MemberService> logger) : IMemberService
{
    public async Task<IReadOnlyList<MemberAdminDto>> ListAsync(CancellationToken ct = default)
    {
        // Los postulantes sin decidir se gestionan en su propia pantalla (RF-PUB-15);
        // aquí solo aparecen quienes ya forman (o formaron) parte del colectivo.
        return await db.Users
            .Where(u => u.Status != MemberStatus.Applicant)
            .OrderBy(u => u.FullName)
            .Select(u => new MemberAdminDto(
                u.Id, u.FullName, u.Email, u.Phone, u.Status, u.RegisteredAt, u.StatusChangedAt,
                u.UserRoles.Select(ur => new RoleRefDto(ur.Role.Id, ur.Role.Name, ur.Role.Color)).ToList()))
            .ToListAsync(ct);
    }

    public async Task ChangeStatusAsync(Guid memberId, ChangeMemberStatusRequest request, Guid actorId, CancellationToken ct = default)
    {
        if (request.Status == MemberStatus.Applicant)
            throw AppException.BadRequest("No se puede devolver a un miembro al estado 'Postulante'.");

        // Sin esta guarda, un administrador podría suspenderse a sí mismo y perder
        // el acceso con el que revertirlo.
        if (memberId == actorId)
            throw AppException.Forbidden("No puedes cambiar tu propio estado.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == memberId, ct)
            ?? throw AppException.NotFound("El miembro no existe.");

        if (user.Status == MemberStatus.Applicant)
            throw AppException.Conflict("Es un postulante sin decidir: usa la pantalla de postulantes.");

        if (user.Status == request.Status)
            throw AppException.Conflict($"El miembro ya está en estado '{Etiqueta(request.Status)}'.");

        var anterior = user.Status;
        user.Status = request.Status;
        user.StatusChangedAt = DateTime.UtcNow;

        notifications.Notify(user.Id, "miembro.estado_cambiado", MensajeAviso(request.Status));
        audit.Log(actorId, "miembro.estado_cambiado", "User", user.Id.ToString(),
            $"{anterior} → {request.Status}{(string.IsNullOrWhiteSpace(request.Motivo) ? "" : $" · {request.Motivo}")}");

        await db.SaveChangesAsync(ct);

        logger.LogInformation("Estado de {Email} cambiado de {Anterior} a {Nuevo} por {ActorId}",
            user.Email, anterior, request.Status, actorId);
    }

    /// <summary>Aviso in-app que recibe el miembro afectado.</summary>
    private static string MensajeAviso(MemberStatus status) => status switch
    {
        MemberStatus.Active => "Tu cuenta vuelve a estar activa. Ya tienes acceso completo al panel.",
        MemberStatus.Inactive => "Tu membresía quedó en pausa. Escríbenos si quieres reactivarla.",
        MemberStatus.Suspended => "Tu cuenta fue suspendida temporalmente. Contacta a la Junta para más detalles.",
        MemberStatus.Retired => "Tu membresía figura como retirada. Gracias por tu paso por Boesh Irí.",
        MemberStatus.Expelled => "Tu membresía fue dada de baja por la administración.",
        _ => "El estado de tu membresía cambió."
    };

    /// <summary>Nombre del estado en español para los mensajes de error.</summary>
    private static string Etiqueta(MemberStatus status) => status switch
    {
        MemberStatus.Active => "Activo",
        MemberStatus.Inactive => "Inactivo",
        MemberStatus.Suspended => "Suspendido",
        MemberStatus.Retired => "Retirado",
        MemberStatus.Expelled => "Expulsado",
        _ => status.ToString()
    };
}
