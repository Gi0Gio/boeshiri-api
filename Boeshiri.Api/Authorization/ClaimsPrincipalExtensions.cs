using System.Security.Claims;

namespace Boeshiri.Api.Authorization;

/// <summary>Utilidades para leer identidad y permisos del usuario autenticado.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>Id del usuario (claim "sub"), o Guid.Empty si no está.</summary>
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirst("sub")?.Value, out var id) ? id : Guid.Empty;

    /// <summary>¿Sesión de un miembro activo? (ver <see cref="MiembroActivoAttribute"/>).</summary>
    public static bool EsMiembroActivo(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true &&
        user.FindFirst(MiembroActivoAttribute.ClaimEstado)?.Value == MiembroActivoAttribute.EstadoActivo;

    /// <summary>
    /// ¿El usuario tiene un permiso concreto? El comodín "*" concede todo. Los
    /// permisos solo cuentan para un miembro activo: un rol asignado a alguien en
    /// pausa no le deja actuar con él hasta que vuelva.
    /// </summary>
    public static bool HasPermission(this ClaimsPrincipal user, string permission)
    {
        if (!user.EsMiembroActivo())
            return false;
        var perms = user.FindAll("perm").Select(c => c.Value);
        return perms.Contains(Permisos.Comodin) || perms.Contains(permission);
    }

    /// <summary>¿Puede ver publicaciones y eventos marcados como exclusivos de miembros?</summary>
    public static bool PuedeVerExclusivos(this ClaimsPrincipal user) =>
        user.HasPermission(Permisos.VerExclusivos);
}
