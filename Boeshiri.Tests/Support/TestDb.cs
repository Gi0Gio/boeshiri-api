using Boeshiri.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Boeshiri.Tests.Support;

/// <summary>
/// Base de datos SQLite en memoria para tests. La conexión se mantiene abierta
/// durante la vida del test; varios <see cref="BoeshiriDbContext"/> comparten los
/// mismos datos. Realista para constraints (índice único, FKs) sin tocar Postgres.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public BoeshiriDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<BoeshiriDbContext>()
            .UseSqlite(_connection)
            .Options);

    public void Dispose() => _connection.Dispose();
}
