namespace Boeshiri.Domain.Entities;

/// <summary>
/// Permiso atómico del catálogo cerrado (Catálogo §1). La clave sigue el patrón
/// "recurso.accion" (p. ej. "noticias.publicar"). El comodín "*" concede todo.
/// </summary>
public class Permission
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public required string Key { get; set; }
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
