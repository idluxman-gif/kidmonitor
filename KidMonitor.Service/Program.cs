using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using KidMonitor.Service;
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

var builder = Host.CreateApplicationBuilder(args);

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

// Database
var dbPath = builder.Configuration["Database:Path"] ?? @"C:\ProgramData\KidMonitor\kidmonitor.db";
builder.Services.AddDbContext<KidMonitorDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"),
    ServiceLifetime.Scoped);

// Services
builder.Services.AddSingleton<INotificationService, ToastNotificationService>();

// Background workers
builder.Services.AddHostedService<MonitorWorker>();
builder.Services.AddHostedService<ProcessTrackingWorker>();
builder.Services.AddHostedService<DailySummaryWorker>();

var host = builder.Build();

// Ensure data directory exists and auto-apply migrations on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();
    var dir = Path.GetDirectoryName(dbPath);
    if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);
    db.Database.Migrate();
}

host.Run();
return 0;
