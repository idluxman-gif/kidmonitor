using KidMonitor.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Core.Data;

public class KidMonitorDbContext : DbContext
{
    public KidMonitorDbContext(DbContextOptions<KidMonitorDbContext> options) : base(options) { }

    public DbSet<AppSession> AppSessions => Set<AppSession>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<ContentSession> ContentSessions => Set<ContentSession>();
    public DbSet<ContentSnapshot> ContentSnapshots => Set<ContentSnapshot>();

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

        modelBuilder.Entity<ContentSession>(e =>
        {
            e.HasIndex(s => s.StartedAt);
            e.HasOne(s => s.AppSession)
             .WithMany()
             .HasForeignKey(s => s.AppSessionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ContentSnapshot>(e =>
        {
            e.HasIndex(s => s.CapturedAt);
            e.HasOne(s => s.ContentSession)
             .WithMany(cs => cs.Snapshots)
             .HasForeignKey(s => s.ContentSessionId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
