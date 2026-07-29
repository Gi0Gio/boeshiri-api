using Boeshiri.Application.Admin;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Notifications;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Boeshiri.Infrastructure.Admin;

/// <summary>
/// Aprobación/rechazo de postulantes (RF-PUB-15/16/17, RF-ADM-02). La aceptación
/// activa al miembro y le asigna el rol base "Miembro". Cada decisión genera un
/// aviso in-app al postulante y una entrada de auditoría (atómico con la decisión).
/// </summary>
public class PostulantesService(
    BoeshiriDbContext db,
    INotificationService notifications,
    IAuditLogger audit,
    ILogger<PostulantesService> logger) : IPostulantesService
{
    private const string BaseMemberRole = "Miembro";

    public async Task<IReadOnlyList<PostulanteDto>> ListPendingAsync(CancellationToken ct = default)
    {
        return await db.Users
            .Where(u => u.Status == MemberStatus.Applicant && u.EmailVerified && u.RejectedAt == null)
            .OrderBy(u => u.RegisteredAt)
            .Select(u => new PostulanteDto(u.Id, u.FullName, u.Email, u.Phone, u.ApplicationReason, u.RegisteredAt))
            .ToListAsync(ct);
    }

    public async Task DecideAsync(Guid postulanteId, DecisionRequest request, Guid decidedBy, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == postulanteId, ct)
            ?? throw AppException.BadRequest("Postulante no encontrado.");

        if (user.Status != MemberStatus.Applicant || user.RejectedAt != null)
            throw AppException.Conflict("La solicitud ya fue decidida o no corresponde a un postulante.");

        if (!user.EmailVerified)
            throw AppException.Conflict("El postulante aún no ha verificado su correo.");

        if (request.Decision == DecisionType.Aceptar)
        {
            user.Status = MemberStatus.Active;
            user.StatusChangedAt = DateTime.UtcNow;

            var memberRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == BaseMemberRole, ct)
                ?? throw AppException.Conflict($"No existe el rol base '{BaseMemberRole}'.");

            if (user.UserRoles.All(ur => ur.RoleId != memberRole.Id))
            {
                user.UserRoles.Add(new UserRole
                {
                    UserId = user.Id,
                    RoleId = memberRole.Id,
                    AssignedBy = decidedBy,
                    AssignedAt = DateTime.UtcNow
                });
            }

            notifications.Notify(user.Id, "solicitud.aceptada",
                "¡Tu solicitud fue aprobada! Ya eres miembro activo de Boesh Irí.");
            audit.Log(decidedBy, "postulante.aceptado", "User", user.Id.ToString(), user.Email);
            logger.LogInformation("Postulante {Email} ACEPTADO por {DecidedBy}", user.Email, decidedBy);
        }
        else
        {
            user.RejectedAt = DateTime.UtcNow;
            user.StatusChangedAt = DateTime.UtcNow;

            notifications.Notify(user.Id, "solicitud.rechazada",
                "Tu solicitud fue revisada y no fue aprobada en esta ocasión.");
            audit.Log(decidedBy, "postulante.rechazado", "User", user.Id.ToString(), user.Email);
            logger.LogInformation("Postulante {Email} RECHAZADO por {DecidedBy}", user.Email, decidedBy);
        }

        await db.SaveChangesAsync(ct);
    }
}
