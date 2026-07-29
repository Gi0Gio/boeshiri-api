using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Persistence.Seed;

/// <summary>
/// Semilla idempotente de los datos de referencia del RBAC: los 23 permisos del
/// catálogo (+ comodín "*"), los 6 roles de sistema y su mapeo rol→permisos
/// (Catálogo de Permisos §1 y §2). Seguro de ejecutar en cada arranque.
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>Catálogo cerrado de permisos globales: clave → descripción.</summary>
    private static readonly Dictionary<string, string> Permissions = new()
    {
        ["perfil.editar"] = "Editar el perfil propio, privacidad y redes",
        ["publicaciones.crear"] = "Crear Artículo/Foto/Video/Música (no Noticia)",
        ["publicaciones.gestionar_propias"] = "Ocultar/editar/eliminar/mostrar las propias",
        ["noticias.publicar"] = "Crear y publicar Noticias",
        ["grupos.solicitar"] = "Solicitar ingreso a una comisión",
        ["documentos.ver_comunidad"] = "Ver biblioteca Comunidad (miembros)",
        ["documentos.subir_comunidad"] = "Subir a la biblioteca Comunidad",
        ["documentos.ver_admin"] = "Ver documentos de nivel Administración",
        ["marketplace.gestionar_propio"] = "Alta + publicar/editar/ocultar productos propios",
        ["eventos.ver_exclusivos"] = "Ver contenido/eventos marcados exclusivos",
        ["panel_admin.ver"] = "Acceso al panel de administración",
        ["postulantes.decidir"] = "Aceptar/rechazar postulantes",
        ["miembros.gestionar_estado"] = "Cambiar estado de miembro",
        ["comisiones.ver_todas"] = "Ver contenido de comisiones/equipos sin ser miembro",
        ["eventos.gestionar"] = "Crear/ocultar/eliminar eventos",
        ["publicaciones.moderar"] = "Ocultar/eliminar publicaciones ajenas",
        ["productos.moderar"] = "Ver/ocultar/eliminar productos ajenos",
        ["finanzas.ver"] = "Ver el balance general",
        ["finanzas.editar"] = "Modificar movimientos y descripciones",
        ["transparencia.gestionar"] = "Crear/editar/ocultar/eliminar artículos oficiales",
        ["junta.espacio"] = "Acceso al espacio privado de la Junta",
        ["roles.gestionar"] = "Crear/editar roles y permisos; asignar roles",
        ["auditoria.ver"] = "Ver el historial de auditoría",
        ["*"] = "Comodín: concede todos los permisos (Super Administrador)"
    };

    /// <summary>Rol → permisos (Catálogo §2). Color para la UI.</summary>
    private static readonly (string Name, string Color, string[] Perms)[] Roles =
    [
        ("Miembro", "tea", [
            "perfil.editar", "publicaciones.crear", "publicaciones.gestionar_propias",
            "grupos.solicitar", "documentos.ver_comunidad", "documentos.subir_comunidad",
            "marketplace.gestionar_propio", "eventos.ver_exclusivos"
        ]),
        ("Periodista", "terracotta", ["noticias.publicar"]),
        ("Recursos Humanos", "caribbean", ["postulantes.decidir"]),
        ("Tesorero", "rainforest", ["panel_admin.ver", "finanzas.ver", "finanzas.editar"]),
        ("Junta Directiva", "jungle", [
            "panel_admin.ver", "postulantes.decidir", "miembros.gestionar_estado",
            "comisiones.ver_todas", "eventos.gestionar", "publicaciones.moderar",
            "productos.moderar", "noticias.publicar", "finanzas.ver",
            "transparencia.gestionar", "junta.espacio", "documentos.ver_admin"
        ]),
        ("Super Administrador", "candy", ["*"])
    ];

    /// <summary>Comisiones permanentes iniciales (RF-GRP-01).</summary>
    private static readonly string[] Commissions =
    [
        "Tecnología",
        "Cultura, Investigación y Eventos",
        "Comunicación y Diseño",
        "Finanzas"
    ];

    public static async Task SeedAsync(BoeshiriDbContext db, CancellationToken ct = default)
    {
        // 1) Permisos ausentes
        var existingKeys = await db.Permissions.Select(p => p.Key).ToListAsync(ct);
        foreach (var (key, desc) in Permissions)
        {
            if (!existingKeys.Contains(key))
                db.Permissions.Add(new Permission { Key = key, Description = desc });
        }
        await db.SaveChangesAsync(ct);

        var permByKey = await db.Permissions.ToDictionaryAsync(p => p.Key, ct);

        // 2) Roles ausentes + su mapeo de permisos
        foreach (var (name, color, perms) in Roles)
        {
            var role = await db.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.Name == name, ct);

            if (role is null)
            {
                role = new Role { Name = name, Color = color, IsSystem = true };
                db.Roles.Add(role);
            }

            var current = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();
            foreach (var key in perms)
            {
                var permId = permByKey[key].Id;
                if (!current.Contains(permId))
                    role.RolePermissions.Add(new RolePermission { Role = role, PermissionId = permId });
            }
        }
        await db.SaveChangesAsync(ct);

        // 3) Comisiones permanentes iniciales (RF-GRP-01)
        var existingCommissions = await db.Groups
            .Where(g => g.Type == GroupType.Commission)
            .Select(g => g.Name)
            .ToListAsync(ct);

        foreach (var name in Commissions)
        {
            if (!existingCommissions.Contains(name))
                db.Groups.Add(new Group { Name = name, Type = GroupType.Commission, Permanent = true });
        }
        await db.SaveChangesAsync(ct);
    }
}
