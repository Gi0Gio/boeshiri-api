using System.Security.Claims;

namespace Boeshiri.Api.Authorization;

/// <summary>Utilidades para leer identidad y permisos del usuario autenticado.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Id del usuario (claim "sub"), o Guid.Empty si no está.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

    /// <summary>¿El usuario tiene un permiso concreto? El comodín "*" concede todo.</summary>
    public static bool HasPermission(this ClaimsPrincipal user, string permission)
    {
        var perms = user.FindAll("perm").Select(c => c.Value);
        return perms.Contains("*") || perms.Contains(permission);
    }
}
