using Microsoft.AspNetCore.Authorization;

namespace Boeshiri.Api.Authorization;

/// <summary>
/// Evalúa un <see cref="PermissionRequirement"/> contra los claims "perm" del JWT
/// (permisos efectivos, RBAC aditivo). El comodín "*" del Super Administrador
/// concede cualquier permiso. Solo cuenta para miembros activos: ver
/// <see cref="ClaimsPrincipalExtensions.HasPermission"/>.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasPermission(requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
