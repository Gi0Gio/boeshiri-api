using Boeshiri.Application.Common;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Common;

/// <summary>
/// Comprobaciones sobre los ids de personas que llegan en una petición (responsables,
/// asignados, líderes). Sin ellas, un id inexistente terminaba en un 500 por la
/// clave foránea, y uno existente pero ajeno (un postulante, alguien de fuera del
/// grupo) se aceptaba sin más.
/// </summary>
public static class Personas
{
    /// <summary>Exige que todos los ids sean miembros ACTIVOS del colectivo.</summary>
    public static async Task ExigirActivosAsync(BoeshiriDbContext db, IEnumerable<Guid> ids, string campo, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return;
        var activos = await db.Users
            .Where(u => lista.Contains(u.Id) && u.Status == MemberStatus.Active)
            .CountAsync(ct);
        if (activos != lista.Count)
            throw AppException.BadRequest($"{campo}: hay personas que no son miembros activos del colectivo.");
    }

    /// <summary>¿El usuario tiene un rol con el comodín «*» (Super Administrador)?</summary>
    public static Task<bool> EsSuperAdminAsync(BoeshiriDbContext db, Guid userId, CancellationToken ct) =>
        db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.Role.RolePermissions.Any(rp => rp.Permission.Key == "*"), ct);
}
