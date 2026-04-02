using System.Net.Http.Json;
using KidMonitor.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KidMonitor.Tests.Api;

/// <summary>
/// Shared <see cref="WebApplicationFactory{TEntryPoint}"/> for all Milestone 3 API tests.
///
/// Overrides:
/// - SQLite database → isolated in-memory connection per factory instance
/// - Dashboard:Pin → "test-1234" (stable, known to all tests)
/// - Database:Path → ":memory:" (prevents file-system usage; DbContext override takes precedence)
/// - Environment → "Testing" (suppresses Windows Service host mode and NTFS ACL startup code)
///
/// PREREQUISITES (WHA-26):
///   KidMonitor.Service must switch to WebApplication.CreateBuilder() so that the web host
///   component is present and WebApplicationFactory can create an in-process TestServer.
///   Until WHA-26 is complete, tests that invoke HTTP endpoints will receive 404s or fail
///   to start — that is expected TDD behaviour (tests define the contract first).
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    public const string TestPin = "test-1234";

    private readonly SqliteConnection _connection;

    public ApiTestFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        // Seed the schema so tests can insert data before making HTTP requests
        var options = new DbContextOptionsBuilder<KidMonitorDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var ctx = new KidMonitorDbContext(options);
        ctx.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _connection.Dispose();
        base.Dispose(disposing);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // PIN checked by the auth middleware added in WHA-26
                ["Dashboard:Pin"] = TestPin,
                // Prevent the startup block from creating C:\ProgramData\KidMonitor\ in CI
                ["Database:Path"] = ":memory:"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the production DbContext (file-backed SQLite) with the shared
            // in-memory connection so tests run in isolation and leave no artefacts.
            services.RemoveAll<DbContextOptions<KidMonitorDbContext>>();
            services.RemoveAll<KidMonitorDbContext>();

            services.AddDbContext<KidMonitorDbContext>(opts =>
                opts.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Returns an <see cref="HttpClient"/> pre-configured with a valid PIN session cookie
    /// so tests that are not focused on auth can skip the login step.
    /// Requires POST /api/auth/login endpoint (WHA-26).
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Pin = TestPin });

        response.EnsureSuccessStatusCode();
        return client;
    }

    /// <summary>Opens a fresh <see cref="KidMonitorDbContext"/> on the shared in-memory connection.</summary>
    public KidMonitorDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<KidMonitorDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new KidMonitorDbContext(options);
    }
}
