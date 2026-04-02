using System.Net;
using System.Net.Http.Json;

namespace KidMonitor.Tests.Api;

/// <summary>
/// Verifies the PIN-based session authentication introduced in WHA-26.
///
/// Contract:
/// - POST /api/auth/login  { "pin": "&lt;value&gt;" }
///   → 200 OK + Set-Cookie: kidmonitor_session=...  (correct PIN)
///   → 401 Unauthorized                              (wrong PIN)
/// - Non-health endpoints without a valid session cookie → 401
/// - Non-health endpoints with a valid session cookie   → not 401
/// </summary>
public sealed class PinAuthTests : IClassFixture<ApiTestFactory>
{
    private readonly ApiTestFactory _factory;

    public PinAuthTests(ApiTestFactory factory) => _factory = factory;

    // ── Unauthenticated access ──────────────────────────────────────────────

    [Theory]
    [InlineData("/api/dashboard")]
    [InlineData("/api/sessions")]
    [InlineData("/api/summaries")]
    [InlineData("/api/events/language")]
    [InlineData("/api/config")]
    public async Task ProtectedEndpoint_Returns401_WithNoCookie(string path)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Login ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithCorrectPin_Returns200AndSetsCookie()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Pin = ApiTestFactory.TestPin });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.Contains("Set-Cookie"),
            "Expected a Set-Cookie header after successful login.");
    }

    [Fact]
    public async Task Login_WithWrongPin_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Pin = "wrong-pin" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyPin_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { Pin = "" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Authenticated access ────────────────────────────────────────────────

    [Fact]
    public async Task ProtectedEndpoint_ReturnsNot401_AfterSuccessfulLogin()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/dashboard");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Health_Returns200_WithoutAnyCookie()
    {
        // /api/health must remain open so the tray app can poll it without a session
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
