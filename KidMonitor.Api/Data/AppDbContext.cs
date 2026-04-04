using KidMonitor.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<PushToken> PushTokens => Set<PushToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Parent>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Email).IsUnique();
        });

        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.DeviceKey).IsUnique();
            e.HasOne(d => d.Parent)
             .WithMany(p => p.Devices)
             .HasForeignKey(d => d.ParentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Event>(e =>
        {
            e.HasKey(ev => ev.Id);
            e.HasOne(ev => ev.Device)
             .WithMany(d => d.Events)
             .HasForeignKey(ev => ev.DeviceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PushToken>(e =>
        {
            e.HasKey(pt => pt.Id);
            e.HasIndex(pt => new { pt.ParentId, pt.Platform }).IsUnique();
            e.HasOne(pt => pt.Parent)
             .WithMany(p => p.PushTokens)
             .HasForeignKey(pt => pt.ParentId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
