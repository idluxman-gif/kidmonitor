using KidMonitor.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Core.Data;

public class KidMonitorDbContext : DbContext
{
    public KidMonitorDbContext(DbContextOptions<KidMonitorDbContext> options) : base(options) { }

    public DbSet<AppSession> AppSessions => Set<AppSession>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSession>(e =>
        {
            e.HasIndex(s => s.StartedAt);
            e.HasIndex(s => s.ProcessName);
        });

        modelBuilder.Entity<NotificationLog>(e =>
        {
            e.HasIndex(n => n.SentAt);
            e.HasOne(n => n.AppSession)
             .WithMany()
             .HasForeignKey(n => n.AppSessionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DailySummary>(e =>
        {
            e.HasIndex(d => d.ReportDate).IsUnique();
        });
    }
}
