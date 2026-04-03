using System.Net;
using KidMonitor.Core.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KidMonitor.Tests.Api;

/// <summary>
/// Verifies that GET /api/health responds 200 OK without any authentication.
///
/// The health probe is intentionally public so the tray app's health poller can
/// call it without a PIN cookie (WHA-29).
/// </summary>
public sealed class HealthEndpointTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public HealthEndpointTests(ApiTestFactory factory) => _factory = factory;

    [Fact]
    public async Task GetHealth_ReturnsOk_WithNoAuthCookie()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetHealth_ResponseBody_ContainsStatusOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("ok", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHealth_InTesting_DoesNotRequireDatabaseMigrateOnStartup()
    {
        using var factory = new NoMigrationApiTestFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

internal sealed class NoMigrationApiTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dashboard:Pin"] = ApiTestFactory.TestPin,
                ["Database:Path"] = ":memory:"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<KidMonitorDbContext>>();
            services.RemoveAll<KidMonitorDbContext>();

            var missingDbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");

            services.AddDbContext<KidMonitorDbContext>(opts =>
                opts.UseSqlite($"Data Source={missingDbPath};Mode=ReadOnly"));
        });
    }
}
