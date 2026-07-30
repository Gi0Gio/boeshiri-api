using System.ComponentModel.DataAnnotations;
using Boeshiri.Domain.Enums;

namespace Boeshiri.Application.Admin;

/// <summary>Rol con sus permisos y cuántos usuarios lo tienen (RF-RBAC-01/02).</summary>
public record RoleDto(Guid Id, string Name, string? Color, bool IsSystem, IReadOnlyList<string> Permissions, int UserCount);

/// <summary>Permiso del catálogo (RF-RBAC-03).</summary>
public record PermissionDto(string Key, string? Description);

/// <summary>Referencia ligera a un rol (para pintarlo como chip).</summary>
public record RoleRefDto(Guid Id, string Name, string? Color);

/// <summary>Usuario con sus roles, para la asignación (RF-RBAC-04).</summary>
public record UserRolesDto(Guid Id, string FullName, string Email, MemberStatus Status, IReadOnlyList<RoleRefDto> Roles);

/// <summary>Asigna un rol a un usuario.</summary>
public record AssignRoleRequest
{
    [Required]
    public required Guid RoleId { get; init; }
}
