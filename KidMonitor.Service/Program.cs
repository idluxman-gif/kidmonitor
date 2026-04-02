using KidMonitor.Core.Data;
using KidMonitor.Service;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// Run as Windows Service when not in development
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "KidMonitorService";
});

// Database
var dbPath = builder.Configuration["Database:Path"] ?? "kidmonitor.db";
builder.Services.AddDbContext<KidMonitorDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Background workers (stubs — implemented in WHA-6, WHA-7, WHA-8, WHA-9)
builder.Services.AddHostedService<MonitorWorker>();

var host = builder.Build();

// Auto-apply migrations on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();
    db.Database.Migrate();
}

host.Run();
