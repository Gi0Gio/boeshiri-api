using System.ComponentModel.DataAnnotations;

namespace Boeshiri.Application.Profiles;

/// <summary>
/// Derechos sobre los datos propios (Ley 81 de 2019, Panamá): acceso y portabilidad
/// (descargarlos) y supresión (eliminar la cuenta).
/// </summary>
public interface IMisDatosService
{
    Task<MisDatosDto> ExportarAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Elimina la cuenta: borra los datos personales y retira lo publicado. Los
    /// registros que el colectivo necesita conservar (movimientos de finanzas,
    /// auditoría, documentos oficiales) quedan a nombre de «Cuenta eliminada».
    /// </summary>
    Task EliminarCuentaAsync(Guid userId, EliminarCuentaRequest request, CancellationToken ct = default);
}

public record EliminarCuentaRequest
{
    [Required, MaxLength(128)]
    public required string Password { get; init; }
}

public record MisDatosDto(
    DateTime GeneradoEn,
    MisDatosPerfil Perfil,
    IReadOnlyList<string> Roles,
    IReadOnlyList<MisDatosRed> Redes,
    IReadOnlyList<MisDatosGrupo> Grupos,
    IReadOnlyList<MisDatosContenido> Publicaciones,
    IReadOnlyList<MisDatosContenido> Anuncios,
    IReadOnlyList<MisDatosContenido> Gritos,
    IReadOnlyList<MisDatosContenido> EventosAsistidos,
    IReadOnlyList<MisDatosAviso> Avisos);

public record MisDatosPerfil(
    string Email, string NombreCompleto, string? Telefono, string? Disciplina, string? Ubicacion,
    string? Bio, string? Intro, string? FotoUrl, string? MotivoPostulacion,
    string Estado, DateTime RegistradoEn, DateTime? VerificadoEn,
    bool MuestraTelefono, bool MuestraCorreo, bool MuestraWhatsapp, bool MuestraComisiones, bool MuestraHistorial);

public record MisDatosRed(string Tipo, string Valor, bool Visible);

public record MisDatosGrupo(string Nombre, string Tipo, string Rol);

public record MisDatosContenido(string Titulo, string Estado, DateTime Fecha);

public record MisDatosAviso(string Mensaje, DateTime Fecha, bool Leido);
