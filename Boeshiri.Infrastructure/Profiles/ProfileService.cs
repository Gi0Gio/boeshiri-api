using System.Net.Mail;
using Boeshiri.Application.Common;
using Boeshiri.Application.Profiles;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Profiles;

/// <summary>Perfil del miembro y perfiles públicos de la Comunidad (RF-MEM-01..08, RF-PUB-09).</summary>
public class ProfileService(BoeshiriDbContext db) : IProfileService
{
    public async Task<MyProfileDto> GetMyProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.Tags)
            .Include(u => u.SocialLinks)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw AppException.Unauthorized("Usuario no encontrado.");

        return new MyProfileDto(
            user.Id, user.FullName, user.Email, user.Phone, user.Bio, user.PhotoUrl, user.Discipline, user.Location,
            new ProfilePrivacyDto(user.ShowPhone, user.ShowEmail, user.ShowWhatsapp, user.ShowCommittees, user.ShowHistory),
            user.Tags.Select(t => t.Name).ToList(),
            user.SocialLinks.Select(l => new SocialLinkDto(l.Type, l.Value, l.Visible)).ToList());
    }

    public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Tags).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw AppException.Unauthorized("Usuario no encontrado.");

        user.FullName = request.FullName.Trim();
        user.Bio = request.Bio;
        user.Discipline = request.Discipline;
        user.PhotoUrl = request.PhotoUrl;

        user.Tags.Clear();
        foreach (var name in NormalizeTags(request.Tags))
            user.Tags.Add(await GetOrCreateTagAsync(name, ct));

        await db.SaveChangesAsync(ct);
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
            user.Id, user.FullName, user.Bio, user.PhotoUrl, user.Discipline, user.Location,
            user.Tags.Select(t => t.Name).ToList(),
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
