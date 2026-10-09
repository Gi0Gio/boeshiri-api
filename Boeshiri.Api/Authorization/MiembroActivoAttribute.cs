using Microsoft.AspNetCore.Authorization;

namespace Boeshiri.Api.Authorization;

/// <summary>
/// Exige sesión de un miembro ACTIVO, no solo una sesión. Un postulante verifica su
/// correo e inicia sesión para ver cómo va su solicitud, y un miembro en pausa
/// (Inactivo) también puede entrar; ninguno de los dos es miembro activo. Con un
/// <c>[Authorize]</c> a secas ambos llegaban a gritos, comisiones, transparencia y
/// a la subida de archivos.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class MiembroActivoAttribute : AuthorizeAttribute
{
    public const string Politica = "miembro-activo";
    public const string ClaimEstado = "status";
    public const string EstadoActivo = "Active";

    public MiembroActivoAttribute() => Policy = Politica;
}
