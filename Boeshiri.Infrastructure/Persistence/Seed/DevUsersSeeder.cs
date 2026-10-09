using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Persistence.Seed;

/// <summary>
/// Cuentas de prueba para el entorno local (Database:SeedDevUsers=true, solo en
/// appsettings.Local.json). Una por perfil que cambia lo que se ve en el panel.
/// Idempotente: crea las que falten y no toca las existentes.
/// </summary>
public static class DevUsersSeeder
{
    public const string Password = "boeshiri-local";

    private static readonly (string Email, string Name, MemberStatus Status, string[] Roles)[] Cuentas =
    [
        ("super@local.test", "Super Local", MemberStatus.Active, ["Miembro", "Super Administrador"]),
        ("junta@local.test", "Junta Local", MemberStatus.Active, ["Miembro", "Junta Directiva"]),
        ("tesorero@local.test", "Tesorero Local", MemberStatus.Active, ["Miembro", "Tesorero"]),
        ("miembro@local.test", "Miembro Local", MemberStatus.Active, ["Miembro"]),
        ("postulante@local.test", "Postulante Local", MemberStatus.Applicant, []),
    ];

    public static async Task SeedAsync(BoeshiriDbContext db, CancellationToken ct = default)
    {
        var hasher = new PasswordHasher<User>();
        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, r => r.Id, ct);
        var existentes = await db.Users.Select(u => u.Email).ToListAsync(ct);

        foreach (var (email, name, status, roleNames) in Cuentas)
        {
            if (existentes.Contains(email))
                continue;

            var user = new User
            {
                Email = email,
                FullName = name,
                PasswordHash = "",
                EmailVerified = true,
                VerifiedAt = DateTime.UtcNow,
                Status = status,
                StatusChangedAt = DateTime.UtcNow,
                ApplicationReason = status == MemberStatus.Applicant ? "Cuenta de prueba local." : null,
            };
            user.PasswordHash = hasher.HashPassword(user, Password);

            foreach (var roleName in roleNames)
                user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roles[roleName] });

            db.Users.Add(user);
        }

        await db.SaveChangesAsync(ct);
    }
}
