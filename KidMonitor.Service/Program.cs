using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using KidMonitor.Core.Security;
using KidMonitor.Service;
using KidMonitor.Service.Cloud;
using KidMonitor.Service.ContentCapture;
using KidMonitor.Service.Dashboard;
using KidMonitor.Service.LanguageDetection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;

// Handle --install / --uninstall CLI args before building host
if (args.Contains("--install"))
{
    var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine process path.");
    return ServiceInstaller.Install(exePath);
}
if (args.Contains("--uninstall"))
{
    return ServiceInstaller.Uninstall();
}
if (TryGetArgumentValue(args, "--set-dashboard-pin", out var dashboardPin))
{
    try
    {
        DashboardConfigFile.WriteDashboardPin(dashboardPin);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to persist dashboard PIN: {ex.Message}");
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);

// Load overrides from ProgramData (installed config takes precedence over bundled defaults)
const string programDataConfig = @"C:\ProgramData\KidMonitor\appsettings.json";
if (File.Exists(programDataConfig))
{
    builder.Configuration.AddJsonFile(programDataConfig, optional: true, reloadOnChange: true);
}

// Run as Windows Service when not in development
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "KidMonitorService";
});

// Windows Event Log for service lifecycle events
builder.Logging.AddEventLog(settings =>
{
    settings.SourceName = "KidMonitorService";
});

// Strongly-typed configuration
builder.Services.Configure<MonitoringOptions>(builder.Configuration.GetSection("Monitoring"));
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection("Notifications"));
builder.Services.Configure<FoulLanguageOptions>(builder.Configuration.GetSection("FoulLanguage"));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection("Database"));
builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection("Dashboard"));
builder.Services.Configure<CloudApiOptions>(builder.Configuration.GetSection("CloudApi"));

// Database
var dbPath = builder.Configuration["Database:Path"] ?? @"C:\ProgramData\KidMonitor\kidmonitor.db";
builder.Services.AddSingleton<IEncryptionService, WindowsDpapiEncryptionService>();
builder.Services.AddDbContext<KidMonitorDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"),
    ServiceLifetime.Scoped);

// Services
builder.Services.AddSingleton<INotificationService, ToastNotificationService>();
builder.Services.AddSingleton<MonitoringEventChannel>();
builder.Services.AddSingleton<ICloudDeviceCredentialStore, DpapiCloudDeviceCredentialStore>();
builder.Services.AddSingleton<OfflineCloudEventStore>();
builder.Services.AddSingleton<ICloudEventPublisher, CloudEventPublisher>();
builder.Services.AddHttpClient(CloudEventPublisher.HttpClientName, (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<CloudApiOptions>>().Value;
        if (Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            client.BaseAddress = baseUri;
        }
        client.Timeout = TimeSpan.FromSeconds(15);
    })
    .AddPolicyHandler(_ => Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .OrResult(response => CloudEventPublisher.IsTransientStatusCode(response.StatusCode))
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(2 * Math.Pow(2, retryAttempt - 1))));

// Content capture adapters (registered as IContentCaptureAdapter, resolved as IEnumerable)
builder.Services.AddSingleton<IContentCaptureAdapter, YouTubeContentAdapter>();
builder.Services.AddSingleton<IContentCaptureAdapter, WhatsAppContentAdapter>();
builder.Services.AddSingleton<IContentCaptureAdapter, GameChatContentAdapter>();

// Language detection pipeline
builder.Services.AddSingleton<ContentSnapshotChannel>();
builder.Services.AddSingleton<IFoulLanguageDetector, ConfigurableFoulLanguageDetector>();
builder.Services.AddSingleton<WhisperTranscriptionService>();
builder.Services.AddSingleton<LoginRateLimiter>();

// Background workers
builder.Services.AddHostedService<MonitorWorker>();
builder.Services.AddHostedService<ProcessTrackingWorker>();
builder.Services.AddHostedService<DailySummaryWorker>();
builder.Services.AddHostedService<ContentCaptureWorker>();
builder.Services.AddHostedService<LanguageDetectionWorker>();
builder.Services.AddHostedService<CloudSyncService>();

// Session (cookie-based PIN auth for dashboard)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

// Configure Kestrel to listen on loopback only
var dashPort = builder.Configuration.GetValue<int>("Dashboard:Port", 5110);
builder.WebHost.UseKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Loopback, dashPort);
});

var app = builder.Build();

// Ensure data directory exists with restrictive NTFS ACLs, then auto-apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();
    var dir = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dir))
    {
        Directory.CreateDirectory(dir);

        // SEC-01/SEC-02: Lock down the data directory so only the service virtual account
        // and local Administrators can read/write it.
        if (OperatingSystem.IsWindows() && !app.Environment.IsEnvironment("Testing"))
        {
            var dirInfo = new DirectoryInfo(dir);
            var security = dirInfo.GetAccessControl();

            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

            security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                @"NT SERVICE\KidMonitorService",
                System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                System.Security.AccessControl.PropagationFlags.None,
                System.Security.AccessControl.AccessControlType.Allow));

            security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                @"BUILTIN\Administrators",
                System.Security.AccessControl.FileSystemRights.FullControl,
                System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                System.Security.AccessControl.PropagationFlags.None,
                System.Security.AccessControl.AccessControlType.Allow));

            security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                @"BUILTIN\Users",
                System.Security.AccessControl.FileSystemRights.ReadAndExecute | System.Security.AccessControl.FileSystemRights.Write,
                System.Security.AccessControl.InheritanceFlags.ContainerInherit | System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                System.Security.AccessControl.PropagationFlags.None,
                System.Security.AccessControl.AccessControlType.Deny));

            dirInfo.SetAccessControl(security);
        }
    }
    if (!app.Environment.IsEnvironment("Testing"))
    {
        db.Database.Migrate();
    }
}

// Middleware pipeline
app.UseStaticFiles();
app.UseSession();
app.UseMiddleware<PinAuthMiddleware>();

// Map all dashboard API endpoints
app.MapDashboardEndpoints();

app.Run();
return 0;

static bool TryGetArgumentValue(IReadOnlyList<string> args, string argumentName, out string value)
{
    for (var i = 0; i < args.Count - 1; i++)
    {
        if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
        {
            value = args[i + 1];
            return true;
        }
    }

    value = string.Empty;
    return false;
}
