using KidMonitor.Core.Models;
using KidMonitor.Core.Security;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Core.Data;

public class KidMonitorDbContext : DbContext
{
    private readonly IEncryptionService _encryptionService;

    public KidMonitorDbContext(
        DbContextOptions<KidMonitorDbContext> options,
        IEncryptionService? encryptionService = null) : base(options)
    {
        _encryptionService = encryptionService ?? WindowsDpapiEncryptionService.Shared;
    }

    public DbSet<AppSession> AppSessions => Set<AppSession>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();
    public DbSet<ContentSession> ContentSessions => Set<ContentSession>();
    public DbSet<ContentSnapshot> ContentSnapshots => Set<ContentSnapshot>();
    public DbSet<LanguageDetectionEvent> LanguageDetectionEvents => Set<LanguageDetectionEvent>();

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
            e.Property(n => n.Body).HasConversion(ProtectedContentConverter.CreateRequiredString(_encryptionService));
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
            e.Property(s => s.ContentTitle).HasConversion(ProtectedContentConverter.CreateRequiredString(_encryptionService));
            e.Property(s => s.ContentIdentifier).HasConversion(ProtectedContentConverter.CreateOptionalString(_encryptionService));
            e.Property(s => s.Channel).HasConversion(ProtectedContentConverter.CreateOptionalString(_encryptionService));
            e.HasOne(s => s.AppSession)
             .WithMany()
             .HasForeignKey(s => s.AppSessionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ContentSnapshot>(e =>
        {
            e.HasIndex(s => s.CapturedAt);
            e.Property(s => s.CapturedText).HasConversion(ProtectedContentConverter.CreateRequiredString(_encryptionService));
            e.Property(s => s.SourceUrl).HasConversion(ProtectedContentConverter.CreateOptionalString(_encryptionService));
            e.Property(s => s.Channel).HasConversion(ProtectedContentConverter.CreateOptionalString(_encryptionService));
            e.HasOne(s => s.ContentSession)
             .WithMany(cs => cs.Snapshots)
             .HasForeignKey(s => s.ContentSessionId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LanguageDetectionEvent>(e =>
        {
            e.HasIndex(lde => lde.DetectedAt);
            e.Property(lde => lde.MatchedTerm).HasConversion(ProtectedContentConverter.CreateRequiredString(_encryptionService));
            e.Property(lde => lde.ContextSnippet).HasConversion(ProtectedContentConverter.CreateRequiredString(_encryptionService));
            e.HasOne(lde => lde.ContentSession)
             .WithMany()
             .HasForeignKey(lde => lde.ContentSessionId)
             .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
