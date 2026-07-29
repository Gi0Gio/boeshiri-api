using Microsoft.AspNetCore.Authorization;

namespace Boeshiri.Api.Authorization;

/// <summary>Requisito de autorización: el usuario debe tener un permiso concreto.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
