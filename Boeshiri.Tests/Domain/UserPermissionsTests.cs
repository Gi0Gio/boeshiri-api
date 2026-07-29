using Boeshiri.Domain.Entities;

namespace Boeshiri.Tests.Domain;

public class UserPermissionsTests
{
    private static Role RoleWith(string name, params string[] permissionKeys)
    {
        var role = new Role { Name = name };
        foreach (var key in permissionKeys)
            role.RolePermissions.Add(new RolePermission { Role = role, Permission = new Permission { Key = key } });
        return role;
    }

    private static User UserWith(params Role[] roles)
    {
        var user = new User { Email = "e@ex.com", PasswordHash = "h", FullName = "F" };
        foreach (var role in roles)
            user.UserRoles.Add(new UserRole { Role = role });
        return user;
    }

    [Fact]
    public void EffectivePermissions_MultipleRoles_ReturnsUnion()
    {
        var user = UserWith(
            RoleWith("Miembro", "perfil.editar", "publicaciones.crear"),
            RoleWith("Periodista", "noticias.publicar"));

        var permissions = user.EffectivePermissions();

        Assert.Equal(
            new[] { "noticias.publicar", "perfil.editar", "publicaciones.crear" },
            permissions.OrderBy(p => p));
    }

    [Fact]
    public void EffectivePermissions_PermissionInTwoRoles_IsNotDuplicated()
    {
        var user = UserWith(
            RoleWith("A", "finanzas.ver"),
            RoleWith("B", "finanzas.ver", "finanzas.editar"));

        var permissions = user.EffectivePermissions();

        Assert.Equal(2, permissions.Count);
        Assert.Contains("finanzas.ver", permissions);
        Assert.Contains("finanzas.editar", permissions);
    }

    [Fact]
    public void EffectivePermissions_NoRoles_IsEmpty()
    {
        var user = UserWith();

        Assert.Empty(user.EffectivePermissions());
    }
}
