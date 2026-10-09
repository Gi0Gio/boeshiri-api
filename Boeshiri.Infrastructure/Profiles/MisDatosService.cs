using Boeshiri.Application.Abstractions;
using Boeshiri.Application.Audit;
using Boeshiri.Application.Common;
using Boeshiri.Application.Profiles;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Common;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Profiles;

/// <inheritdoc />
public class MisDatosService(
    BoeshiriDbContext db,
    IPasswordHasher<User> passwordHasher,
    IFileStorage storage,
    IAuditLogger audit) : IMisDatosService
{
    public const string NombreEliminado = "Cuenta eliminada";

    public async Task<MisDatosDto> ExportarAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await db.Users.AsNoTracking()
            .Include(x => x.SocialLinks)
            .Include(x => x.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw AppException.NotFound("La cuenta no existe.");

        var perfil = new MisDatosPerfil(
            u.Email, u.FullName, u.Phone, u.Discipline, u.Disciplines, u.Location, u.Bio, u.Intro, u.PhotoUrl, u.ApplicationReason,
            u.Status.ToString(), u.RegisteredAt, u.VerifiedAt,
            u.ShowPhone, u.ShowEmail, u.ShowWhatsapp, u.ShowCommittees, u.ShowHistory);

        var grupos = await db.GroupMemberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new MisDatosGrupo(m.Group.Name, m.Group.Type.ToString(), m.Role.ToString()))
            .ToListAsync(ct);

        var publicaciones = await db.Publications.AsNoTracking()
            .Where(p => p.AuthorId == userId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new MisDatosContenido(p.Title, p.Status.ToString(), p.CreatedAt))
            .ToListAsync(ct);

        var anuncios = await db.Products.AsNoTracking()
            .Where(p => p.SellerId == userId)
            .OrderBy(p => p.CreatedAt)
            .Select(p => new MisDatosContenido(p.Name, p.Status.ToString(), p.CreatedAt))
            .ToListAsync(ct);

        var gritos = await db.Shouts.AsNoTracking()
            .Where(s => s.AuthorId == userId)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new MisDatosContenido(s.Title, s.Status.ToString(), s.HappensAt))
            .ToListAsync(ct);

        var eventos = await db.EventAttendees.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Event.Date)
            .Select(a => new MisDatosContenido(a.Event.Title, "Asistió", a.Event.Date ?? a.Event.CreatedAt))
            .ToListAsync(ct);

        var avisos = await db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new MisDatosAviso(n.Message, n.CreatedAt, n.Read))
            .ToListAsync(ct);

        return new MisDatosDto(
            DateTime.UtcNow, perfil,
            u.UserRoles.Select(ur => ur.Role.Name).ToList(),
            u.SocialLinks.Select(l => new MisDatosRed(l.Type.ToString(), l.Value, l.Visible)).ToList(),
            grupos, publicaciones, anuncios, gritos, eventos, avisos);
    }

    public async Task EliminarCuentaAsync(Guid userId, EliminarCuentaRequest request, CancellationToken ct = default)
    {
        var u = await db.Users
            .Include(x => x.SocialLinks)
            .Include(x => x.Tags)
            .Include(x => x.Skills)
            .Include(x => x.UserRoles)
            .FirstOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw AppException.NotFound("La cuenta no existe.");

        if (passwordHasher.VerifyHashedPassword(u, u.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            throw AppException.BadRequest("La contraseña no es correcta.");

        // Que no se quede el sistema sin nadie que lo administre.
        if (await Personas.EsSuperAdminAsync(db, userId, ct))
        {
            var otros = await db.UserRoles.AnyAsync(ur => ur.UserId != userId
                && ur.Role.RolePermissions.Any(rp => rp.Permission.Key == "*"), ct);
            if (!otros)
                throw AppException.Conflict("Eres el único Super Administrador. Nombra a otra persona antes de eliminar tu cuenta.");
        }

        // Lo que dirige: hay que pasarlo a alguien antes (si no, el grupo queda sin cabeza).
        var dirige = await db.GroupMemberships
            .Where(m => m.UserId == userId && (m.Role == GroupRole.Coordinator || m.Role == GroupRole.Leader))
            .Select(m => m.Group.Name)
            .ToListAsync(ct);
        if (dirige.Count > 0)
            throw AppException.Conflict($"Estás al frente de {string.Join(", ", dirige)}. Nombra a otra persona antes de eliminar tu cuenta.");

        var foto = u.PhotoUrl;

        // Todo o nada: una cuenta a medio borrar sería peor que ninguna de las dos.
        var estrategia = db.Database.CreateExecutionStrategy();
        await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var ahora = DateTime.UtcNow;

            // Datos personales fuera. El registro queda (lo referencian finanzas,
            // auditoría y documentos), pero sin nada que identifique a la persona.
            u.Email = $"eliminada-{u.Id:N}@cuentas.invalid";
            u.FullName = NombreEliminado;
            u.PasswordHash = passwordHasher.HashPassword(u, Tokens.Nuevo());
            u.Phone = null;
            u.ApplicationReason = null;
            u.Bio = null;
            u.Intro = null;
            u.PhotoUrl = null;
            u.Location = null;
            u.Discipline = null;
            u.Disciplines = [];
            u.ShowPhone = u.ShowEmail = u.ShowWhatsapp = u.ShowCommittees = u.ShowHistory = false;
            u.MarketplaceActive = false;
            u.EmailVerified = false;
            u.Status = MemberStatus.Retired;
            u.StatusChangedAt = ahora;
            u.SocialLinks.Clear();
            u.Tags.Clear();
            u.Skills.Clear();
            u.UserRoles.Clear();

            // Lo publicado se retira.
            await db.Publications.Where(p => p.AuthorId == userId && p.Status != ContentStatus.Deleted)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, ContentStatus.Deleted), ct);
            await db.Products.Where(p => p.SellerId == userId && p.Status != ProductStatus.Deleted)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, ProductStatus.Deleted), ct);
            await db.Shouts.Where(s => s.AuthorId == userId && s.Status == ShoutStatus.Open)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ShoutStatus.Cancelled), ct);

            // Vínculos y rastros personales.
            await db.GroupMemberships.Where(m => m.UserId == userId).ExecuteDeleteAsync(ct);
            await db.JoinRequests.Where(r => r.UserId == userId).ExecuteDeleteAsync(ct);
            await db.ShoutJoins.Where(j => j.UserId == userId && j.Shout.HappensAt > ahora).ExecuteDeleteAsync(ct);
            await db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync(ct);
            await db.VerificationTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
            await db.PasswordResetTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
            await db.RefreshTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);

            audit.Log(userId, "cuenta.eliminada", "User", userId.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        await ArchivosGuard.BorrarSiSinUsoAsync(db, storage, foto, ct);
    }
}
