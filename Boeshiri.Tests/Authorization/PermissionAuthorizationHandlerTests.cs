using System.Security.Claims;
using Boeshiri.Api.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Boeshiri.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext ContextWith(string requiredPermission, string status, params string[] userPermissions)
    {
        var requirement = new PermissionRequirement(requiredPermission);
        var claims = userPermissions.Select(p => new Claim("perm", p)).Append(new Claim("status", status));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        return new AuthorizationHandlerContext([requirement], user, resource: null);
    }

    [Fact]
    public async Task Handle_UserHasExactPermission_Succeeds()
    {
        var context = ContextWith("postulantes.decidir", "Active", "perfil.editar", "postulantes.decidir");

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_UserHasWildcard_Succeeds()
    {
        var context = ContextWith("auditoria.ver", "Active", "*");

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Handle_UserLacksPermission_DoesNotSucceed()
    {
        var context = ContextWith("auditoria.ver", "Active", "postulantes.decidir");

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    /// <summary>Un rol en una cuenta en pausa no da permisos hasta que vuelva a estar activa.</summary>
    [Theory]
    [InlineData("Inactive")]
    [InlineData("Applicant")]
    public async Task Handle_PermissionButNotActive_DoesNotSucceed(string status)
    {
        var context = ContextWith("publicaciones.crear", status, "publicaciones.crear");

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
