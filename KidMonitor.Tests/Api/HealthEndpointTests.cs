using System.Net;

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
}
