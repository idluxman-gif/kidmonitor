using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using KidMonitor.Service;
using KidMonitor.Service.ContentCapture;
using KidMonitor.Service.Dashboard;
using KidMonitor.Service.LanguageDetection;
using Microsoft.EntityFrameworkCore;

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

var builder = WebApplication.CreateBuilder(args);

// Load overrides from ProgramData (installed config takes precedence over bundled defaults).
// Register this at host-build time so test hosts can inject Dashboard:ProgramDataPath first.
builder.Host.ConfigureAppConfiguration((context, config) =>
{
    var programDataDirectory = context.Configuration["Dashboard:ProgramDataPath"] ?? @"C:\ProgramData\KidMonitor";
    var programDataConfig = Path.Combine(programDataDirectory, "appsettings.json");
    if (File.Exists(programDataConfig))
    {
        config.AddJsonFile(programDataConfig, optional: true, reloadOnChange: true);
    }
});

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

// Database
var dbPath = builder.Configuration["Database:Path"] ?? @"C:\ProgramData\KidMonitor\kidmonitor.db";
builder.Services.AddDbContext<KidMonitorDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"),
    ServiceLifetime.Scoped);

// Services
builder.Services.AddSingleton<INotificationService, ToastNotificationService>();

// Content capture adapters (registered as IContentCaptureAdapter, resolved as IEnumerable)
builder.Services.AddSingleton<IContentCaptureAdapter, YouTubeContentAdapter>();
builder.Services.AddSingleton<IContentCaptureAdapter, WhatsAppContentAdapter>();
builder.Services.AddSingleton<IContentCaptureAdapter, GameChatContentAdapter>();

// Language detection pipeline
builder.Services.AddSingleton<ContentSnapshotChannel>();
builder.Services.AddSingleton<IFoulLanguageDetector, ConfigurableFoulLanguageDetector>();
builder.Services.AddSingleton<WhisperTranscriptionService>();

// Background workers
builder.Services.AddHostedService<MonitorWorker>();
builder.Services.AddHostedService<ProcessTrackingWorker>();
builder.Services.AddHostedService<DailySummaryWorker>();
builder.Services.AddHostedService<ContentCaptureWorker>();
builder.Services.AddHostedService<LanguageDetectionWorker>();

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
    db.Database.Migrate();
}

// Middleware pipeline
app.UseStaticFiles();
app.UseSession();
app.UseMiddleware<PinAuthMiddleware>();

// Map all dashboard API endpoints
app.MapDashboardEndpoints();

app.Run();
return 0;
