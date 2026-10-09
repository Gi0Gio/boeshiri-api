namespace Boeshiri.Api.Authorization;

/// <summary>
/// Claves del catálogo de permisos (ver DatabaseSeeder). Constantes en vez de
/// textos sueltos por los controladores: una errata en un texto dejaba un
/// endpoint protegido por un permiso que no existe, y eso no lo avisa nadie.
/// </summary>
public static class Permisos
{
    public const string PerfilEditar = "perfil.editar";
    public const string PublicacionesCrear = "publicaciones.crear";
    public const string PublicacionesGestionarPropias = "publicaciones.gestionar_propias";
    public const string NoticiasPublicar = "noticias.publicar";
    public const string GruposSolicitar = "grupos.solicitar";
    public const string DocumentosVerComunidad = "documentos.ver_comunidad";
    public const string DocumentosSubirComunidad = "documentos.subir_comunidad";
    public const string DocumentosVerAdmin = "documentos.ver_admin";
    public const string MarketplaceGestionarPropio = "marketplace.gestionar_propio";
    public const string VerExclusivos = "eventos.ver_exclusivos";
    public const string PostulantesDecidir = "postulantes.decidir";
    public const string MiembrosGestionarEstado = "miembros.gestionar_estado";
    public const string ComisionesVerTodas = "comisiones.ver_todas";
    public const string EventosGestionar = "eventos.gestionar";
    public const string PublicacionesModerar = "publicaciones.moderar";
    public const string ProductosModerar = "productos.moderar";
    public const string GritosPublicar = "gritos.publicar";
    public const string GritosModerar = "gritos.moderar";
    public const string FinanzasVer = "finanzas.ver";
    public const string FinanzasEditar = "finanzas.editar";
    public const string TransparenciaGestionar = "transparencia.gestionar";
    public const string RolesGestionar = "roles.gestionar";
    public const string AuditoriaVer = "auditoria.ver";
    public const string ArchivosGestionar = "archivos.gestionar";
    public const string ConvocatoriasGestionar = "convocatorias.gestionar";
    public const string Comodin = "*";
}
