using System.Net.Http.Headers;
using Boeshiri.Domain.Entities;
using Boeshiri.Domain.Enums;
using Boeshiri.Infrastructure.Auth;
using Boeshiri.Infrastructure.Persistence;
using Boeshiri.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Boeshiri.Tests.Support;

/// <summary>
/// La API completa en memoria (pipeline, autorización, validación) sobre SQLite.
/// Existe para probar lo que los tests de servicio no ven: qué atributo protege
/// cada endpoint y quién llega a qué. Un fallo así (postulantes viendo contenido
/// de miembros) pasó todos los tests de servicio sin que nada lo detectara.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting y no ConfigureAppConfiguration: Program lee la configuración
        // antes de Build (cadena de conexión, JWT), y con el hosting mínimo solo los
        // ajustes de host llegan a tiempo a esa lectura.
        foreach (var (clave, valor) in new Dictionary<string, string>
        {
            ["ConnectionStrings:Default"] = "Host=no-se-usa",
            ["Database:MigrateOnStartup"] = "false",
            ["Jwt:Key"] = "clave-de-pruebas-de-al-menos-treinta-y-dos-bytes!!",
            ["Jwt:Issuer"] = "boeshiri-api",
            ["Jwt:Audience"] = "boeshiri-web",
            ["App:PublicBaseUrl"] = "https://sitio.test",
            ["App:ContactEmail"] = "buzon@sitio.test",
        })
            builder.UseSetting(clave, valor);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<BoeshiriDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<BoeshiriDbContext>));
            services.AddDbContext<BoeshiriDbContext>(o => o.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BoeshiriDbContext>();
        db.Database.EnsureCreated();
        DatabaseSeeder.SeedAsync(db).GetAwaiter().GetResult();
        return host;
    }

    /// <summary>Crea un usuario con los roles indicados y devuelve su id.</summary>
    public async Task<Guid> CrearUsuarioAsync(string email, MemberStatus status, params string[] roles)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BoeshiriDbContext>();
        var user = new User
        {
            Email = email,
            PasswordHash = "x",
            FullName = email.Split('@')[0],
            Status = status,
            EmailVerified = true,
        };
        foreach (var nombre in roles)
        {
            var rol = await db.Roles.SingleAsync(r => r.Name == nombre);
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rol.Id });
        }
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Cliente HTTP autenticado como ese usuario (JWT recién emitido).</summary>
    public async Task<HttpClient> ClienteDeAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BoeshiriDbContext>();
        var user = await db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .SingleAsync(u => u.Id == userId);
        var (token, _) = scope.ServiceProvider.GetRequiredService<JwtTokenGenerator>().Generate(user);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Acceso directo a la base para preparar datos o comprobar efectos.</summary>
    public async Task ConDbAsync(Func<BoeshiriDbContext, Task> accion)
    {
        using var scope = Services.CreateScope();
        await accion(scope.ServiceProvider.GetRequiredService<BoeshiriDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
