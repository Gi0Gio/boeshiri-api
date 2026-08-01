using Boeshiri.Application.Abstractions;
using System.Net.Mail;
using Boeshiri.Application.Common;
using Boeshiri.Application.Profiles;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Profiles;

/// <summary>Perfil del miembro y perfiles públicos de la Comunidad (RF-MEM-01..08, RF-PUB-09).</summary>
public class ProfileService(BoeshiriDbContext db, IFileStorage storage) : IProfileService
{
    public async Task<MyProfileDto> GetMyProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Tags)
            .Include(u => u.SocialLinks)
            .Include(u => u.Skills)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw AppException.Unauthorized("Usuario no encontrado.");

        return new MyProfileDto(
            user.Id, user.FullName, user.Email, user.Phone, user.Bio, user.Intro, user.PhotoUrl, user.Discipline, user.Location,
            new ProfilePrivacyDto(user.ShowPhone, user.ShowEmail, user.ShowWhatsapp, user.ShowCommittees, user.ShowHistory),
            user.Tags.Select(t => t.Name).ToList(),
            user.Skills.OrderBy(s => s.Order).Select(s => new SkillDto(s.Name, s.Level)).ToList(),
            user.SocialLinks.Select(l => new SocialLinkDto(l.Type, l.Value, l.Visible)).ToList(),
            user.MarketplaceActive);
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Tags).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw AppException.Unauthorized("Usuario no encontrado.");

        user.FullName = request.FullName.Trim();
        user.Bio = request.Bio;
        user.Intro = request.Intro;
        user.Discipline = request.Discipline;
        user.Location = request.Location;

        // Al cambiar de foto hay que soltar la anterior del bucket: si no, cada
        // cambio de avatar deja un objeto que nadie volverá a referenciar y que
        // sigue ocupando (y contando para el límite gratuito de 10 GB).
        var fotoAnterior = user.PhotoUrl;
        user.PhotoUrl = request.PhotoUrl;

        user.Tags.Clear();
        foreach (var name in NormalizeTags(request.Tags))
            user.Tags.Add(await GetOrCreateTagAsync(name, ct));

        // Reemplazar habilidades (sobre el DbSet para evitar conflictos de navegación).
        var existingSkills = await db.ProfileSkills.Where(s => s.UserId == userId).ToListAsync(ct);
        db.ProfileSkills.RemoveRange(existingSkills);
        var order = 0;
        foreach (var sk in request.Skills ?? [])
        {
            var name = sk.Name.Trim();
            if (name.Length == 0) continue;
            db.ProfileSkills.Add(new ProfileSkill { UserId = userId, Name = name, Level = Math.Clamp(sk.Level, 1, 8), Order = order++ });
        }

        await db.SaveChangesAsync(ct);

        // Después de guardar: si el borrado remoto falla, el perfil ya quedó correcto
        // y solo sobra un objeto en el bucket. Al revés perderíamos la foto nueva.
        if (!string.IsNullOrWhiteSpace(fotoAnterior) && fotoAnterior != user.PhotoUrl)
            await storage.DeleteAsync(fotoAnterior, ct);
    }

    public async Task UpdatePrivacyAsync(Guid userId, UpdatePrivacyRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw AppException.Unauthorized("Usuario no encontrado.");

        user.ShowPhone = request.ShowPhone;
        user.ShowEmail = request.ShowEmail;
        user.ShowWhatsapp = request.ShowWhatsapp;
        user.ShowCommittees = request.ShowCommittees;
        user.ShowHistory = request.ShowHistory;

        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateSocialLinksAsync(Guid userId, UpdateSocialLinksRequest request, CancellationToken ct = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
            throw AppException.Unauthorized("Usuario no encontrado.");

        if (request.Links.Count(l => l.Type == SocialNetworkType.Whatsapp) > 2)
            throw AppException.BadRequest("Máximo 2 números de WhatsApp (RF-MEM-05).");

        // Validar/normalizar todo ANTES de tocar la BD (si algo falla, nada cambia).
        var normalized = request.Links
            .Select(l => new SocialLink { UserId = userId, Type = l.Type, Value = Normalize(l.Type, l.Value.Trim()), Visible = l.Visible })
            .ToList();

        var existing = await db.SocialLinks.Where(l => l.UserId == userId).ToListAsync(ct);
        db.SocialLinks.RemoveRange(existing);
        db.SocialLinks.AddRange(normalized);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CommunityMemberDto>> ListCommunityAsync(CancellationToken ct = default)
    {
        return await db.Users
            .Where(u => u.Status == MemberStatus.Active)
            .OrderBy(u => u.FullName)
            .Select(u => new CommunityMemberDto(u.Id, u.FullName, u.Discipline, u.PhotoUrl, u.Tags.Select(t => t.Name).ToList()))
            .ToListAsync(ct);
    }

    public async Task<PublicProfileDto> GetPublicProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Tags)
            .Include(u => u.SocialLinks)
            .Include(u => u.Skills)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        // Solo los miembros activos tienen perfil público (RF-PUB-09).
        if (user is null || user.Status != MemberStatus.Active)
            throw AppException.NotFound("Perfil no disponible.");

        // Redes visibles; WhatsApp además gobernado por el flag de privacidad (RF-MEM-03/04).
        var socialLinks = user.SocialLinks
            .Where(l => l.Visible && (l.Type != SocialNetworkType.Whatsapp || user.ShowWhatsapp))
            .Select(l => new PublicSocialLinkDto(l.Type, l.Value))
            .ToList();

        var commissions = user.ShowCommittees
            ? await db.GroupMemberships
                .Where(m => m.UserId == userId && m.Group.Type == GroupType.Commission)
                .Select(m => m.Group.Name)
                .ToListAsync(ct)
            : [];

        var history = user.ShowHistory
            ? await db.EventAttendees
                .Where(a => a.UserId == userId && a.Event.Status != ContentStatus.Deleted)
                .OrderByDescending(a => a.Event.Date)
                .Select(a => new ProfileEventDto(a.EventId, a.Event.Title, a.Event.Date))
                .ToListAsync(ct)
            : [];

        var gallery = await db.Publications
            .Where(p => p.AuthorId == userId && p.Visibility == Visibility.Public && p.Status == ContentStatus.Published)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProfileGalleryItemDto(
                p.Id, p.Type, p.Title,
                p.Images.OrderBy(i => i.Order).Select(i => i.Url).FirstOrDefault()))
            .ToListAsync(ct);

        return new PublicProfileDto(
            user.Id, user.FullName, user.Bio, user.Intro, user.PhotoUrl, user.Discipline, user.Location,
            user.UserRoles.Select(ur => ur.Role.Name).ToList(),
            user.Tags.Select(t => t.Name).ToList(),
            user.Skills.OrderBy(s => s.Order).Select(s => new SkillDto(s.Name, s.Level)).ToList(),
            user.ShowPhone ? user.Phone : null,
            user.ShowEmail ? user.Email : null,
            socialLinks, commissions, history, gallery);
    }

    // ── Helpers ──────────────────────────────────────────────────
    private static string Normalize(SocialNetworkType type, string value) => type switch
    {
        SocialNetworkType.Instagram or SocialNetworkType.Tiktok => value.StartsWith('@') ? value : "@" + value,
        SocialNetworkType.Mail => IsValidEmail(value) ? value : throw AppException.BadRequest("Correo inválido en las redes."),
        SocialNetworkType.Whatsapp => value.StartsWith('+') ? value : throw AppException.BadRequest("WhatsApp requiere código de país (ej. +507...)."),
        _ => value
    };

    private static bool IsValidEmail(string value) => MailAddress.TryCreate(value, out _);

    private static IEnumerable<string> NormalizeTags(IEnumerable<string>? tags) =>
        (tags ?? [])
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private async Task<SocialTag> GetOrCreateTagAsync(string name, CancellationToken ct)
    {
        var lowered = name.ToLowerInvariant();
        var existing = await db.SocialTags.FirstOrDefaultAsync(t => t.Name.ToLower() == lowered, ct);
        if (existing is not null)
            return existing;

        var tag = new SocialTag { Name = name };
        db.SocialTags.Add(tag);
        return tag;
    }
}
