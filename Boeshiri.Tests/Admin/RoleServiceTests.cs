using Boeshiri.Application.Admin;
using Boeshiri.Application.Common;
using Boeshiri.Domain.Entities;
using Boeshiri.Infrastructure.Admin;
using Boeshiri.Infrastructure.Audit;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Persistence.Seed;
using Boeshiri.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Admin;

public class RoleServiceTests : IDisposable
{
    private readonly TestDb _db = new();

    private static RoleService NewService(BoeshiriDbContext ctx) => new(ctx, new AuditLogger(ctx));

    public RoleServiceTests()
    {
        using var ctx = _db.CreateContext();
        DatabaseSeeder.SeedAsync(ctx).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreateRoleAsync_CreatesWithPermissionsAndAudits()
    {
        var actor = Guid.NewGuid();
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateRoleAsync(new CreateRoleRequest
            {
                Name = "Fotógrafo", Color = "#00735e",
                Permissions = ["publicaciones.crear", "perfil.editar"],
            }, actor);

        await using var check = _db.CreateContext();
        var rol = await check.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .SingleAsync(r => r.Id == id);

        Assert.False(rol.IsSystem);   // los creados a mano nunca son de sistema
        Assert.Equal(2, rol.RolePermissions.Count);
        Assert.Equal(1, await check.AuditEntries.CountAsync(a => a.Action == "rol.creado" && a.ActorId == actor));
    }

    [Fact]
    public async Task CreateRoleAsync_DuplicateName_ThrowsConflict()
    {
        await using var ctx = _db.CreateContext();
        // "Miembro" ya existe como rol de sistema.
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateRoleAsync(new CreateRoleRequest { Name = "  miembro  " }, Guid.NewGuid()));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task CreateRoleAsync_UnknownPermission_ThrowsBadRequest()
    {
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateRoleAsync(new CreateRoleRequest
            {
                Name = "Raro", Permissions = ["publicaciones.crear", "inventado.total"],
            }, Guid.NewGuid()));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("inventado.total", ex.Message);
    }

    [Fact]
    public async Task CreateRoleAsync_WithWildcard_IsRejected()
    {
        // Concederlo crearía un segundo Super Administrador saltándose el modelo.
        await using var ctx = _db.CreateContext();
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).CreateRoleAsync(new CreateRoleRequest { Name = "Casi dios", Permissions = ["*"] }, Guid.NewGuid()));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task SetRolePermissionsAsync_ReplacesTheWholeMap()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateRoleAsync(new CreateRoleRequest
            {
                Name = "Curador", Permissions = ["publicaciones.crear"],
            }, Guid.NewGuid());

        await using (var ctx = _db.CreateContext())
            await NewService(ctx).SetRolePermissionsAsync(id, ["finanzas.ver", "auditoria.ver"], Guid.NewGuid());

        await using var check = _db.CreateContext();
        var claves = await check.RolePermissions.Where(rp => rp.RoleId == id)
            .Select(rp => rp.Permission.Key).OrderBy(k => k).ToListAsync();

        // Reemplaza, no acumula: el permiso anterior desaparece.
        Assert.Equal(["auditoria.ver", "finanzas.ver"], claves);
    }

    [Fact]
    public async Task SetRolePermissionsAsync_OnSystemRole_ThrowsConflict()
    {
        await using var ctx = _db.CreateContext();
        var junta = await ctx.Roles.SingleAsync(r => r.Name == "Junta Directiva");

        // La semilla repondría lo quitado al reiniciar: sería una reversión silenciosa.
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).SetRolePermissionsAsync(junta.Id, ["finanzas.ver"], Guid.NewGuid()));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateRoleAsync_OnSystemRole_ThrowsConflict()
    {
        await using var ctx = _db.CreateContext();
        var miembro = await ctx.Roles.SingleAsync(r => r.Name == "Miembro");

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).UpdateRoleAsync(miembro.Id, new UpdateRoleRequest { Name = "Otro" }, Guid.NewGuid()));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task DeleteRoleAsync_ReportsAffectedUsersAndRemovesAssignments()
    {
        Guid id;
        await using (var ctx = _db.CreateContext())
            id = await NewService(ctx).CreateRoleAsync(new CreateRoleRequest { Name = "Temporal" }, Guid.NewGuid());

        var u1 = await AddUserAsync("a@ex.com");
        var u2 = await AddUserAsync("b@ex.com");
        await using (var ctx = _db.CreateContext())
        {
            var svc = NewService(ctx);
            await svc.AssignRoleAsync(u1, id, Guid.NewGuid());
            await svc.AssignRoleAsync(u2, id, Guid.NewGuid());
        }

        int afectados;
        await using (var ctx = _db.CreateContext())
            afectados = await NewService(ctx).DeleteRoleAsync(id, Guid.NewGuid());

        await using var check = _db.CreateContext();
        // Se informa a cuántos afecta: es una pérdida de permisos y debe verse.
        Assert.Equal(2, afectados);
        Assert.False(await check.Roles.AnyAsync(r => r.Id == id));
        Assert.False(await check.UserRoles.AnyAsync(ur => ur.RoleId == id));
    }

    [Fact]
    public async Task DeleteRoleAsync_OnSystemRole_ThrowsConflict()
    {
        await using var ctx = _db.CreateContext();
        var super = await ctx.Roles.SingleAsync(r => r.Name == "Super Administrador");

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            NewService(ctx).DeleteRoleAsync(super.Id, Guid.NewGuid()));

        Assert.Equal(409, ex.StatusCode);
    }

    private async Task<Guid> AddUserAsync(string email)
    {
        await using var ctx = _db.CreateContext();
        var u = new User { Email = email, PasswordHash = "x", FullName = email };
        ctx.Users.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    public void Dispose() => _db.Dispose();
}
