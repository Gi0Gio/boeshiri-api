using System.Security.Claims;
using Boeshiri.Api.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Boeshiri.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext ContextWith(string requiredPermission, params string[] userPermissions)
    {
        var requirement = new PermissionRequirement(requiredPermission);
        var claims = userPermissions.Select(p => new Claim("perm", p));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return new AuthorizationHandlerContext([requirement], user, resource: null);
    }

    [Fact]
    public async Task Handle_UserHasExactPermission_Succeeds()
    {
        var context = ContextWith("postulantes.decidir", "perfil.editar", "postulantes.decidir");

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_UserHasWildcard_Succeeds()
    {
        var context = ContextWith("auditoria.ver", "*");

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_UserLacksPermission_DoesNotSucceed()
    {
        var context = ContextWith("auditoria.ver", "postulantes.decidir");

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
