using Boeshiri.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core de Boesh Irí. Contexto acotado inicial: Identidad + RBAC.
/// Convenciones: PK Guid v7, columnas snake_case (configurado en la DI),
/// enums persistidos como texto, timestamps en UTC.
/// </summary>
public class BoeshiriDbContext(DbContextOptions<BoeshiriDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<SocialTag> SocialTags => Set<SocialTag>();
    public DbSet<SocialLink> SocialLinks => Set<SocialLink>();
    public DbSet<VerificationToken> VerificationTokens => Set<VerificationToken>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<Publication> Publications => Set<Publication>();
    public DbSet<PublicationImage> PublicationImages => Set<PublicationImage>();
    public DbSet<PublicationLink> PublicationLinks => Set<PublicationLink>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<JoinRequest> JoinRequests => Set<JoinRequest>();
    public DbSet<KanbanTask> KanbanTasks => Set<KanbanTask>();
    public DbSet<KanbanTaskAssignee> KanbanTaskAssignees => Set<KanbanTaskAssignee>();
    public DbSet<KanbanTaskLink> KanbanTaskLinks => Set<KanbanTaskLink>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventImage> EventImages => Set<EventImage>();
    public DbSet<EventAttendee> EventAttendees => Set<EventAttendee>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<FinancialMovement> FinancialMovements => Set<FinancialMovement>();
    public DbSet<TransparencyArticle> TransparencyArticles => Set<TransparencyArticle>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ── User ─────────────────────────────────────────────────
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.FullName).HasMaxLength(160).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(32);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Bio).HasMaxLength(2000);
            e.Property(x => x.Discipline).HasMaxLength(120);
            e.Property(x => x.Location).HasMaxLength(160);
        });

        // ── Role ─────────────────────────────────────────────────
        b.Entity<Role>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Color).HasMaxLength(40);
        });

        // ── Permission ───────────────────────────────────────────
        b.Entity<Permission>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Key).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Description).HasMaxLength(240);
        });

        // ── UserRole (M:N con datos) ─────────────────────────────
        b.Entity<UserRole>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RoleId });
            e.HasOne(x => x.User).WithMany(u => u.UserRoles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── RolePermission (M:N) ─────────────────────────────────
        b.Entity<RolePermission>(e =>
        {
            e.HasKey(x => new { x.RoleId, x.PermissionId });
            e.HasOne(x => x.Role).WithMany(r => r.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Permission).WithMany(p => p.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── SocialTag (M:N implícita con User) ───────────────────
        b.Entity<SocialTag>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasMany(x => x.Users).WithMany(u => u.Tags);
        });

        // ── SocialLink ───────────────────────────────────────────
        b.Entity<SocialLink>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Value).HasMaxLength(320).IsRequired();
            e.HasOne(x => x.User).WithMany(u => u.SocialLinks).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── VerificationToken ────────────────────────────────────
        b.Entity<VerificationToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Token).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Notification ─────────────────────────────────────────
        b.Entity<Notification>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(80).IsRequired();
            e.Property(x => x.Message).HasMaxLength(1000).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Read });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── AuditEntry (append-only) ─────────────────────────────
        b.Entity<AuditEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).HasMaxLength(120).IsRequired();
            e.Property(x => x.ObjectType).HasMaxLength(80).IsRequired();
            e.Property(x => x.ObjectId).HasMaxLength(80);
            e.Property(x => x.Metadata).HasMaxLength(2000);
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.ActorId);
        });

        // ── Publication ──────────────────────────────────────────
        b.Entity<Publication>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(20000);
            e.Property(x => x.ExternalUrl).HasMaxLength(500);
            e.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Status, x.Visibility });
            e.HasIndex(x => x.AuthorId);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Tags).WithMany(t => t.Publications);
        });

        // ── PublicationImage ─────────────────────────────────────
        b.Entity<PublicationImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Publication).WithMany(p => p.Images).HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── PublicationLink ──────────────────────────────────────
        b.Entity<PublicationLink>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Publication).WithMany(p => p.Links).HasForeignKey(x => x.PublicationId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Tag ──────────────────────────────────────────────────
        b.Entity<Tag>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        // ── Group ────────────────────────────────────────────────
        b.Entity<Group>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Type, x.Name });
            e.HasOne(x => x.ParentCommission).WithMany().HasForeignKey(x => x.ParentCommissionId).OnDelete(DeleteBehavior.Restrict);
        });

        // ── GroupMembership ──────────────────────────────────────
        b.Entity<GroupMembership>(e =>
        {
            e.HasKey(x => new { x.GroupId, x.UserId });
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Group).WithMany(g => g.Memberships).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── JoinRequest ──────────────────────────────────────────
        b.Entity<JoinRequest>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.CommissionId, x.Status });
            e.HasOne(x => x.Commission).WithMany().HasForeignKey(x => x.CommissionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── KanbanTask ───────────────────────────────────────────
        b.Entity<KanbanTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.GroupId, x.Status });
            e.HasOne(x => x.Group).WithMany(g => g.Tasks).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── KanbanTaskAssignee ───────────────────────────────────
        b.Entity<KanbanTaskAssignee>(e =>
        {
            e.HasKey(x => new { x.TaskId, x.UserId });
            e.HasOne(x => x.Task).WithMany(t => t.Assignees).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── KanbanTaskLink ───────────────────────────────────────
        b.Entity<KanbanTaskLink>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Task).WithMany(t => t.Links).HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Event ────────────────────────────────────────────────
        b.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).HasMaxLength(80).IsRequired();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Location).HasMaxLength(200);
            e.Property(x => x.Cost).HasPrecision(10, 2);
            e.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Status, x.Visibility, x.Date });
        });

        // ── EventImage ───────────────────────────────────────────
        b.Entity<EventImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Event).WithMany(ev => ev.Images).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── EventAttendee ────────────────────────────────────────
        b.Entity<EventAttendee>(e =>
        {
            e.HasKey(x => new { x.EventId, x.UserId });
            e.HasOne(x => x.Event).WithMany(ev => ev.Attendees).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Product ──────────────────────────────────────────────
        b.Entity<Product>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Category).HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.DeliveryLocation).HasMaxLength(200);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => new { x.Status, x.Category });
            e.HasIndex(x => x.SellerId);
            e.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── ProductImage ─────────────────────────────────────────
        b.Entity<ProductImage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Product).WithMany(p => p.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Document ─────────────────────────────────────────────
        b.Entity<Document>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Category).HasMaxLength(80).IsRequired();
            e.Property(x => x.Library).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.AccessLevel).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.FileUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.ContentType).HasMaxLength(120);
            e.HasIndex(x => new { x.Library, x.AccessLevel });
            e.HasIndex(x => x.AuthorId);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── FinancialMovement ────────────────────────────────────
        b.Entity<FinancialMovement>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Concept).HasMaxLength(200).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.HasIndex(x => x.Date);
        });

        // ── TransparencyArticle ──────────────────────────────────
        b.Entity<TransparencyArticle>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(20000).IsRequired();
            e.Property(x => x.Category).HasMaxLength(80).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
