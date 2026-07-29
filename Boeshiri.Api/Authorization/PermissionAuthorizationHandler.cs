using Microsoft.AspNetCore.Authorization;

namespace Boeshiri.Api.Authorization;

/// <summary>
/// Evalúa un <see cref="PermissionRequirement"/> contra los claims "perm" del JWT
/// (permisos efectivos, RBAC aditivo). El comodín "*" del Super Administrador
/// concede cualquier permiso.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var permissions = context.User.FindAll("perm").Select(c => c.Value);
        if (permissions.Contains("*") || permissions.Contains(requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
