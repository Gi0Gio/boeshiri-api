using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Boeshiri.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Boeshiri.Infrastructure.Auth;

/// <summary>
/// Genera el JWT de acceso. Incluye los permisos efectivos del usuario como claims
/// "perm" (uno por permiso) y los roles como "role" (RBAC aditivo, D-1). El comodín
/// "*" del Super Administrador viaja como un claim "perm" = "*".
/// </summary>
public class JwtTokenGenerator(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    /// <summary>Requiere que UserRoles → Role → RolePermissions → Permission estén cargados.</summary>
    public (string Token, DateTime ExpiresAt) Generate(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.FullName),
            new("status", user.Status.ToString())
        };

        foreach (var userRole in user.UserRoles)
            claims.Add(new Claim("role", userRole.Role.Name));

        foreach (var permission in user.EffectivePermissions())
            claims.Add(new Claim("perm", permission));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
