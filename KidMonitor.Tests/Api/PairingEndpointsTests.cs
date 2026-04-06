using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using KidMonitor.Api.Data;
using KidMonitor.Api.Endpoints;
using KidMonitor.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KidMonitor.Tests.Api;

public sealed class PairingEndpointsTests
{
    [Fact]
    public async Task PairingFlow_GenerateClaimConfirm_ProducesDeviceTokenAndListsTheDevice()
    {
        await using var host = await PairingApiHost.StartAsync();
        var parentId = Guid.NewGuid();
        await host.EnsureParentAsync(parentId);

        var generateResponse = await host.Client.PostAsJsonAsync("/pairing/generate", new
        {
            deviceKey = "pc-123",
            deviceName = "Kid Desktop",
        });

        Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
        var generateBody = await generateResponse.Content.ReadFromJsonAsync<GeneratePairingResponse>();
        Assert.NotNull(generateBody);

        using var authenticatedRequest = new HttpRequestMessage(HttpMethod.Post, "/pairing/claim")
        {
            Content = JsonContent.Create(new { pairingCode = generateBody!.PairingCode }),
        };
        authenticatedRequest.Headers.Add(TestAuthHandler.ParentIdHeader, parentId.ToString());

        var claimResponse = await host.Client.SendAsync(authenticatedRequest);
        Assert.Equal(HttpStatusCode.OK, claimResponse.StatusCode);

        var confirmResponse = await host.Client.PostAsJsonAsync("/pairing/confirm", new
        {
            deviceKey = "pc-123",
            pairingCode = generateBody.PairingCode,
        });

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmBody = await confirmResponse.Content.ReadFromJsonAsync<ConfirmPairingResponse>();
        Assert.NotNull(confirmBody);
        Assert.False(string.IsNullOrWhiteSpace(confirmBody!.DeviceToken));

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/devices");
        listRequest.Headers.Add(TestAuthHandler.ParentIdHeader, authenticatedRequest.Headers.GetValues(TestAuthHandler.ParentIdHeader).Single());

        var listResponse = await host.Client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var devices = await listResponse.Content.ReadFromJsonAsync<DeviceResponse[]>();
        var device = Assert.Single(devices!);
        Assert.Equal("pc-123", device.DeviceKey);
        Assert.Equal("Kid Desktop", device.DeviceName);
        Assert.Null(device.DeviceToken);
    }

    [Fact]
    public async Task DeleteDevice_RemovesOnlyTheAuthenticatedParentsDevice()
    {
        await using var host = await PairingApiHost.StartAsync();

        var parentId = Guid.NewGuid();
        await host.EnsureParentAsync(parentId);
        var deviceId = await host.CreatePairedDeviceAsync(parentId, "pc-123", "Kid Desktop");

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/devices/{deviceId}");
        deleteRequest.Headers.Add(TestAuthHandler.ParentIdHeader, parentId.ToString());

        var deleteResponse = await host.Client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/devices");
        listRequest.Headers.Add(TestAuthHandler.ParentIdHeader, parentId.ToString());

        var listResponse = await host.Client.SendAsync(listRequest);
        var devices = await listResponse.Content.ReadFromJsonAsync<DeviceResponse[]>();

        Assert.NotNull(devices);
        Assert.Empty(devices);
    }

    private sealed class PairingApiHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly WebApplication _app;

        private PairingApiHost(SqliteConnection connection, WebApplication app, HttpClient client)
        {
            _connection = connection;
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<PairingApiHost> StartAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            builder.Services.AddAuthorization();
            builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddScoped<DevicePairingService>();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapDeviceEndpoints();
            app.MapPairingEndpoints();

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            await app.StartAsync();
            return new PairingApiHost(connection, app, app.GetTestClient());
        }

        public async Task<Guid> CreatePairedDeviceAsync(Guid parentId, string deviceKey, string deviceName)
        {
            var pairingService = _app.Services.GetRequiredService<DevicePairingService>();
            var session = await pairingService.GenerateAsync(deviceKey, deviceName, CancellationToken.None);
            var claim = await pairingService.ClaimAsync(parentId, session.PairingCode, CancellationToken.None);
            return claim.DeviceId;
        }

        public async Task EnsureParentAsync(Guid parentId)
        {
            await using var scope = _app.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await db.Parents.AnyAsync(parent => parent.Id == parentId))
            {
                return;
            }

            db.Parents.Add(new KidMonitor.Api.Models.Parent
            {
                Id = parentId,
                Email = $"{parentId}@example.com",
                DisplayName = "Test Parent",
                PasswordHash = "hashed",
            });
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _connection.DisposeAsync();
            await _app.DisposeAsync();
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string ParentIdHeader = "X-Test-ParentId";
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(ParentIdHeader, out var parentIdValues)
                || !Guid.TryParse(parentIdValues.SingleOrDefault(), out var parentId))
            {
                return Task.FromResult(AuthenticateResult.Fail("Missing parent id header."));
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, parentId.ToString()),
                ],
                SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed record GeneratePairingResponse(string PairingCode, string QrPayload, DateTimeOffset ExpiresAt);

    private sealed record ConfirmPairingResponse(
        string Status,
        Guid? DeviceId,
        string? DeviceToken,
        string? DeviceName);
}
