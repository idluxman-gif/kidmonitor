using KidMonitor.Core.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace KidMonitor.Tests.TestHelpers;

/// <summary>
/// Creates a KidMonitorDbContext backed by an in-process SQLite :memory: database.
/// Each call to <see cref="CreateDb"/> returns an independent context with a fresh schema.
/// The caller is responsible for disposing both the context and the connection.
/// </summary>
public static class InMemoryDbHelper
{
    /// <summary>
    /// Creates a new in-memory SQLite <see cref="KidMonitorDbContext"/> with the full schema applied.
    /// Keep <paramref name="connection"/> alive for the lifetime of the context; dispose when done.
    /// </summary>
    public static KidMonitorDbContext CreateDb(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<KidMonitorDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new KidMonitorDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>
    /// Creates a mocked <see cref="IServiceScopeFactory"/> that always resolves
    /// <see cref="KidMonitorDbContext"/> to <paramref name="db"/>.
    /// </summary>
    public static IServiceScopeFactory CreateScopeFactory(KidMonitorDbContext db)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(KidMonitorDbContext)))
            .Returns(db);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(sf => sf.CreateScope()).Returns(scope.Object);

        return scopeFactory.Object;
    }
}
