using Boeshiri.Application.Common;
using Boeshiri.Application.Profiles;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Profiles;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Profiles;

public class ProfileServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private readonly FakeFileStorage _storage = new();

    private ProfileService NewService(BoeshiriDbContext ctx) => new(ctx, _storage);

    // ── Edición ──────────────────────────────────────────────────
    [Fact]
    public async Task UpdateProfileAsync_UpdatesFieldsAndTags()
    {
        var id = await AddUserAsync("m@ex.com");

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).UpdateProfileAsync(id, new UpdateProfileRequest
            {
                FullName = "Ana López",
                Bio = "Muralista",
                Discipline = "Muralismo",
                Tags = ["Pintora", "Muralista", "pintora"] // dedupe case-insensitive
            });

        await using var check = _db.CreateContext();
        var user = await check.Users.Include(u => u.Tags).SingleAsync(u => u.Id == id);
        Assert.Equal("Ana López", user.FullName);
        Assert.Equal("Muralismo", user.Discipline);
        Assert.Equal(2, user.Tags.Count);
    }

    // ── Validaciones de redes (RF-MEM-05) ────────────────────────
    [Fact]
    public async Task UpdateSocialLinksAsync_MoreThanTwoWhatsapp_ThrowsBadRequest()
    {
        var id = await AddUserAsync("m@ex.com");
        var req = new UpdateSocialLinksRequest
        {
            Links =
            [
                new() { Type = SocialNetworkType.Whatsapp, Value = "+50760000001" },
                new() { Type = SocialNetworkType.Whatsapp, Value = "+50760000002" },
                new() { Type = SocialNetworkType.Whatsapp, Value = "+50760000003" }
            ]
        };

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).UpdateSocialLinksAsync(id, req));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateSocialLinksAsync_InstagramPrependsAt()
    {
        var id = await AddUserAsync("m@ex.com");
        var req = new UpdateSocialLinksRequest
        {
            Links = [new() { Type = SocialNetworkType.Instagram, Value = "ana.murales" }]
        };

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).UpdateSocialLinksAsync(id, req);

        await using var check = _db.CreateContext();
        var link = await check.SocialLinks.SingleAsync(l => l.UserId == id);
        Assert.Equal("@ana.murales", link.Value);
    }

    [Fact]
    public async Task UpdateSocialLinksAsync_InvalidEmail_ThrowsBadRequest()
    {
        var id = await AddUserAsync("m@ex.com");
        var req = new UpdateSocialLinksRequest
        {
            Links = [new() { Type = SocialNetworkType.Mail, Value = "no-es-correo" }]
        };

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).UpdateSocialLinksAsync(id, req));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateSocialLinksAsync_WhatsappWithoutCountryCode_ThrowsBadRequest()
    {
        var id = await AddUserAsync("m@ex.com");
        var req = new UpdateSocialLinksRequest
        {
            Links = [new() { Type = SocialNetworkType.Whatsapp, Value = "60001234" }]
        };

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).UpdateSocialLinksAsync(id, req));
        Assert.Equal(400, ex.StatusCode);
    }

    // ── Privacidad en el perfil público (RF-MEM-03) ──────────────
    [Fact]
    public async Task GetPublicProfileAsync_FlagsOff_HidesPhoneAndEmail()
    {
        var id = await AddUserAsync("secreto@ex.com", cfg: u =>
        {
            u.Phone = "+50760000000";
            u.ShowPhone = false;
            u.ShowEmail = false;
        });

        await using var ctx = _db.CreateContext();
        var profile = await NewService(ctx).GetPublicProfileAsync(id);

        Assert.Null(profile.Phone);
        Assert.Null(profile.Email);
    }

    [Fact]
    public async Task GetPublicProfileAsync_FlagsOn_ShowsPhoneAndEmail()
    {
        var id = await AddUserAsync("visible@ex.com", cfg: u =>
        {
            u.Phone = "+50760000000";
            u.ShowPhone = true;
            u.ShowEmail = true;
        });

        await using var ctx = _db.CreateContext();
        var profile = await NewService(ctx).GetPublicProfileAsync(id);

        Assert.Equal("+50760000000", profile.Phone);
        Assert.Equal("visible@ex.com", profile.Email);
    }

    [Fact]
    public async Task GetPublicProfileAsync_ReturnsOnlyVisibleSocialLinks()
    {
        var id = await AddUserAsync("m@ex.com");
        await using (var ctx = _db.CreateContext())
        {
            ctx.SocialLinks.Add(new SocialLink { UserId = id, Type = SocialNetworkType.Instagram, Value = "@pub", Visible = true });
            ctx.SocialLinks.Add(new SocialLink { UserId = id, Type = SocialNetworkType.Discord, Value = "priv#1", Visible = false });
            await ctx.SaveChangesAsync();
        }

        await using var check = _db.CreateContext();
        var profile = await NewService(check).GetPublicProfileAsync(id);

        Assert.Single(profile.SocialLinks);
        Assert.Equal(SocialNetworkType.Instagram, profile.SocialLinks[0].Type);
    }

    [Fact]
    public async Task GetPublicProfileAsync_NonActiveMember_ThrowsNotFound()
    {
        var id = await AddUserAsync("inactivo@ex.com", MemberStatus.Inactive);

        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() => NewService(ctx).GetPublicProfileAsync(id));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task ListCommunityAsync_ReturnsOnlyActiveMembers()
    {
        await AddUserAsync("activo@ex.com");
        await AddUserAsync("postulante@ex.com", MemberStatus.Applicant);
        await AddUserAsync("retirado@ex.com", MemberStatus.Retired);

        await using var ctx = _db.CreateContext();
        var community = await NewService(ctx).ListCommunityAsync();

        Assert.Single(community);
        Assert.Equal("activo@ex.com", community[0].FullName);
    }

    private async Task<Guid> AddUserAsync(string email, MemberStatus status = MemberStatus.Active, Action<User>? cfg = null)
    {
        await using var ctx = _db.CreateContext();
        var user = new User { Email = email, PasswordHash = "x", FullName = email, EmailVerified = true, Status = status };
        cfg?.Invoke(user);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        return user.Id;
    }

    public void Dispose() => _db.Dispose();
}
