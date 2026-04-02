using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KidMonitor.Core.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace KidMonitor.Tests.Api;

/// <summary>
/// Integration tests for GET /api/config and PUT /api/config (WHA-27).
///
/// GET /api/config  – returns the current MonitoringOptions as JSON
/// PUT /api/config  – accepts updated MonitoringOptions, persists to appsettings.json,
///                    and signals the service to reload
///
/// Round-trip contract: a PUT followed by GET must return the same values.
///
/// The config write test uses a per-test temp file for appsettings.json to avoid
/// polluting shared state between runs.
/// </summary>
public sealed class ConfigEndpointTests : IAsyncLifetime
{
    private string? _tempSettingsDir;
    private string? _tempSettingsPath;
    private ConfigRoundTripFactory? _factory;
    private SqliteConnection? _connection;

    public async Task InitializeAsync()
    {
        _tempSettingsDir = Path.Combine(Path.GetTempPath(), $"KidMonitorTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempSettingsDir);
        _tempSettingsPath = Path.Combine(_tempSettingsDir, "appsettings.json");

        // Write a minimal initial settings file
        await File.WriteAllTextAsync(_tempSettingsPath, """
            {
              "Monitoring": {
                "PollIntervalSeconds": 10,
                "TrackedApps": [],
                "LanguageDetection": { "Enabled": true, "WordList": [] }
              },
              "Dashboard": { "Pin": "test-1234" }
            }
            """);

        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<KidMonitorDbContext>()
            .UseSqlite(_connection)
            .Options;
        using var ctx = new KidMonitorDbContext(options);
        await ctx.Database.EnsureCreatedAsync();

        _factory = new ConfigRoundTripFactory(_connection, _tempSettingsDir!);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            _factory.Dispose();
            await Task.CompletedTask;
        }
        if (_connection is not null) await _connection.DisposeAsync();
        if (_tempSettingsDir is not null && Directory.Exists(_tempSettingsDir))
            Directory.Delete(_tempSettingsDir, recursive: true);
    }

    // ── GET /api/config ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetConfig_ReturnsOk_WithAuthCookie()
    {
        var client = await _factory!.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/config");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConfig_ReturnsJsonWithMonitoringFields()
    {
        var client = await _factory!.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/config");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // Must expose at minimum the fields consumed by the dashboard config tab
        Assert.True(root.TryGetProperty("pollIntervalSeconds", out _),
            "Response missing 'pollIntervalSeconds'");
        Assert.True(root.TryGetProperty("trackedApps", out _),
            "Response missing 'trackedApps'");
    }

    // ── PUT /api/config ─────────────────────────────────────────────────────

    [Fact]
    public async Task PutConfig_ReturnsOk_WithValidPayload()
    {
        var client = await _factory!.CreateAuthenticatedClientAsync();
        var payload = new
        {
            PollIntervalSeconds = 15,
            TrackedApps = new[] { new { ProcessName = "chrome.exe", DisplayName = "Chrome" } }
        };

        var response = await client.PutAsJsonAsync("/api/config", payload);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PutConfig_WritesUpdatedValuesToSettingsFile()
    {
        var client = await _factory!.CreateAuthenticatedClientAsync();
        var payload = new
        {
            PollIntervalSeconds = 42,
            TrackedApps = Array.Empty<object>()
        };

        await client.PutAsJsonAsync("/api/config", payload);

        var written = await File.ReadAllTextAsync(_tempSettingsPath!);
        Assert.Contains("42", written);
    }

    // ── Round-trip ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfigRoundTrip_PutThenGet_ReturnsSameValues()
    {
        var client = await _factory!.CreateAuthenticatedClientAsync();

        // PUT new config
        var putPayload = new
        {
            PollIntervalSeconds = 30,
            TrackedApps = new[]
            {
                new { ProcessName = "discord.exe", DisplayName = "Discord" },
                new { ProcessName = "steam.exe",   DisplayName = "Steam"   }
            },
            LanguageDetection = new
            {
                Enabled = false,
                WordList = new[] { "alpha", "beta" }
            }
        };

        var putResponse = await client.PutAsJsonAsync("/api/config", putPayload);
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        // GET it back
        var getResponse = await client.GetAsync("/api/config");
        var body = await getResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Equal(30, root.GetProperty("pollIntervalSeconds").GetInt32());

        var apps = root.GetProperty("trackedApps").EnumerateArray().ToList();
        Assert.Equal(2, apps.Count);
        Assert.Contains(apps, a =>
            a.TryGetProperty("processName", out var pn) && pn.GetString() == "discord.exe");

        // LanguageDetection.Enabled should be false
        if (root.TryGetProperty("languageDetection", out var ld))
        {
            Assert.False(ld.GetProperty("enabled").GetBoolean());
        }
    }

    [Fact]
    public async Task PutConfig_Returns401_WithoutAuthCookie()
    {
        var client = _factory!.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
        var payload = new { PollIntervalSeconds = 5 };

        var response = await client.PutAsJsonAsync("/api/config", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

/// <summary>
/// Variant of <see cref="ApiTestFactory"/> that injects a custom ProgramData directory path
/// so the config write tests can verify file output without touching the real ProgramData.
/// </summary>
internal sealed class ConfigRoundTripFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private readonly string _settingsDir;

    public ConfigRoundTripFactory(SqliteConnection connection, string settingsDir)
    {
        _connection = connection;
        _settingsDir = settingsDir;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Dashboard:Pin"] = ApiTestFactory.TestPin,
                ["Database:Path"] = ":memory:",
                // Tells the config-write handler where to persist appsettings.json
                ["Dashboard:ProgramDataPath"] = _settingsDir
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<KidMonitorDbContext>>();
            services.RemoveAll<KidMonitorDbContext>();
            services.AddDbContext<KidMonitorDbContext>(opts =>
                opts.UseSqlite(_connection));
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Pin = ApiTestFactory.TestPin });

        response.EnsureSuccessStatusCode();
        return client;
    }
}
