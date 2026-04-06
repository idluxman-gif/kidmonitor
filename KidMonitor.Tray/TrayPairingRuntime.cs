using System.Text.Json;
using KidMonitor.Core.Configuration;
using KidMonitor.Core.Security;
using KidMonitor.Service.Cloud;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KidMonitor.Tray;

internal sealed class TrayPairingRuntime(HttpClient httpClient, TrayPairingCoordinator coordinator) : IDisposable
{
    public TrayPairingCoordinator Coordinator { get; } = coordinator;

    public void Dispose()
    {
        httpClient.Dispose();
    }
}

internal static class TrayPairingRuntimeFactory
{
    private const string ProgramDataConfigPath = @"C:\ProgramData\KidMonitor\appsettings.json";
    private const string DefaultCredentialsFilePath = @"C:\ProgramData\KidMonitor\cloud-device.json";

    public static TrayPairingRuntime? TryCreate()
    {
        var settings = TrayPairingSettingsLoader.TryLoad();
        if (settings is null)
        {
            return null;
        }

        var httpClient = new HttpClient
        {
            BaseAddress = settings.BaseUri,
            Timeout = TimeSpan.FromSeconds(15),
        };
        var credentialStore = new DpapiCloudDeviceCredentialStore(
            Options.Create(
                new CloudApiOptions
                {
                    BaseUrl = settings.BaseUri.ToString(),
                    CredentialsFilePath = settings.CredentialsFilePath,
                }),
            WindowsDpapiEncryptionService.Shared,
            NullLogger<DpapiCloudDeviceCredentialStore>.Instance);
        var pairingClient = new CloudPairingClient(
            new SingleHttpClientFactory(httpClient),
            credentialStore);
        var coordinator = new TrayPairingCoordinator(pairingClient, TimeProvider.System, TimeSpan.FromSeconds(5));

        return new TrayPairingRuntime(httpClient, coordinator);
    }

    private sealed class SingleHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed record TrayPairingSettings(Uri BaseUri, string CredentialsFilePath);

    private static class TrayPairingSettingsLoader
    {
        public static TrayPairingSettings? TryLoad()
        {
            foreach (var configPath in EnumerateCandidateConfigPaths())
            {
                if (!File.Exists(configPath))
                {
                    continue;
                }

                using var stream = File.OpenRead(configPath);
                using var document = JsonDocument.Parse(stream);

                if (!document.RootElement.TryGetProperty("CloudApi", out var cloudApiSection))
                {
                    continue;
                }

                var baseUrl = TryGetString(cloudApiSection, "BaseUrl");
                if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
                {
                    continue;
                }

                var credentialsFilePath = TryGetString(cloudApiSection, "CredentialsFilePath");
                if (string.IsNullOrWhiteSpace(credentialsFilePath))
                {
                    credentialsFilePath = DefaultCredentialsFilePath;
                }

                return new TrayPairingSettings(baseUri, credentialsFilePath.Trim());
            }

            return null;
        }

        private static IEnumerable<string> EnumerateCandidateConfigPaths()
        {
            yield return ProgramDataConfigPath;
            yield return Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property)
                && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
    }
}
