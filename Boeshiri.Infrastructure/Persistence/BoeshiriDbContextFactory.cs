using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Boeshiri.Infrastructure.Persistence;

/// <summary>
/// Factory de diseño para las herramientas de EF Core (migraciones). Permite a
/// `dotnet ef` construir el contexto sin arrancar la API. Toma la cadena de la
/// variable de entorno CONNECTIONSTRINGS__DEFAULT o usa la de Docker local.
/// </summary>
public class BoeshiriDbContextFactory : IDesignTimeDbContextFactory<BoeshiriDbContext>
{
    public BoeshiriDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULT")
            ?? "Host=localhost;Port=5432;Database=boeshiri;Username=boeshiri;Password=boeshiri_dev";

        var options = new DbContextOptionsBuilder<BoeshiriDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new BoeshiriDbContext(options);
    }
}
