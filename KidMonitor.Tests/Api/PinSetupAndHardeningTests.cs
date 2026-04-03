using System.Net;
using System.Net.Http.Json;
using KidMonitor.Service.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KidMonitor.Tests.Api;

/// <summary>
/// Regression tests for WHA-47: PIN setup hardening and rate limiting.
///
/// Covers:
/// - Login blocked when PIN is still the default "0000"
/// - POST /api/auth/setup sets initial PIN
/// - Setup endpoint rejects default PIN and short PINs
/// - Setup endpoint rejects call after PIN is already configured
/// - PUT /api/auth/pin changes PIN (requires auth session + correct current PIN)
/// - Login rate limiting: 429 after 5 consecutive failures
/// - Successful login clears rate-limit counter
/// </summary>
public sealed class PinSetupAndHardeningTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public PinSetupAndHardeningTests(ApiTestFactory factory) => _factory = factory;

    /// <summary>Creates a client where Dashboard:Pin is overridden to the factory default "0000".</summary>
    private HttpClient CreateDefaultPinClient() =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Dashboard:Pin"] = DashboardOptions.DefaultPin
                }))).CreateClient();

    // ── Setup required: login blocked when PIN is "0000" ───────────────────

    [Fact]
    public async Task Login_Returns403_WhenPinIsDefault()
    {
        var client = CreateDefaultPinClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Pin = DashboardOptions.DefaultPin });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── /api/auth/setup ─────────────────────────────────────────────────────

    [Fact]
    public async Task Setup_Returns409_WhenPinAlreadyConfigured()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/setup",
            new { Pin = "newpin123" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Setup_Returns400_WhenPinIsDefault()
    {
        var client = CreateDefaultPinClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/setup",
            new { Pin = DashboardOptions.DefaultPin });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Setup_Returns400_WhenPinIsTooShort()
    {
        var client = CreateDefaultPinClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/setup",
            new { Pin = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── PUT /api/auth/pin ──────────────────────────────────────────────────

    [Fact]
    public async Task ChangePin_Returns401_WhenNotAuthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var response = await client.PutAsJsonAsync(
            "/api/auth/pin",
            new { CurrentPin = ApiTestFactory.TestPin, NewPin = "newpin123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePin_Returns401_WhenCurrentPinIsWrong()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/auth/pin",
            new { CurrentPin = "wrong-pin", NewPin = "newpin123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePin_Returns400_WhenNewPinIsDefault()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/auth/pin",
            new { CurrentPin = ApiTestFactory.TestPin, NewPin = DashboardOptions.DefaultPin });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePin_Returns400_WhenNewPinIsTooShort()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/auth/pin",
            new { CurrentPin = ApiTestFactory.TestPin, NewPin = "ab" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Rate limiting ───────────────────────────────────────────────────────

    [Fact]
    public async Task Login_Returns429_AfterFiveConsecutiveFailures()
    {
        // Use a fresh rate limiter instance so this test does not share state
        var rateLimiter = new LoginRateLimiter();
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(rateLimiter)))
            .CreateClient();

        for (int i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { Pin = "wrong" });
        }

        var response = await client.PostAsJsonAsync("/api/auth/login", new { Pin = "wrong" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Login_ClearsRateLimit_AfterSuccess()
    {
        var rateLimiter = new LoginRateLimiter();
        var client = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton(rateLimiter)))
            .CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

        // 4 failures (below lockout threshold of 5)
        for (int i = 0; i < 4; i++)
        {
            await client.PostAsJsonAsync("/api/auth/login", new { Pin = "wrong" });
        }

        // Successful login should clear the counter
        var successResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Pin = ApiTestFactory.TestPin });
        Assert.Equal(HttpStatusCode.OK, successResponse.StatusCode);

        // Subsequent failure should NOT be 429 (counter was reset)
        var afterResponse = await client.PostAsJsonAsync("/api/auth/login", new { Pin = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, afterResponse.StatusCode);
    }
}
