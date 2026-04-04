using KidMonitor.Core.Configuration;
using KidMonitor.Core.Data;
using KidMonitor.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KidMonitor.Service.Cloud;

/// <summary>
/// Persists pending cloud events in the local SQLite database for replay.
/// </summary>
public sealed class OfflineCloudEventStore(
    IServiceScopeFactory scopeFactory,
    IOptions<CloudApiOptions> options,
    ILogger<OfflineCloudEventStore> logger)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly CloudApiOptions _options = options.Value;
    private readonly ILogger<OfflineCloudEventStore> _logger = logger;

    /// <summary>
    /// Buffers an event for later delivery, trimming the oldest rows when the queue is full.
    /// </summary>
    public async Task BufferAsync(MonitoringEvent monitoringEvent, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();

        var capacity = Math.Max(1, _options.OfflineQueueCapacity);
        var currentCount = await db.PendingCloudEvents.CountAsync(cancellationToken).ConfigureAwait(false);
        var overflow = currentCount - capacity + 1;
        if (overflow > 0)
        {
            var staleRows = await db.PendingCloudEvents
                .OrderBy(pending => pending.EnqueuedAt)
                .ThenBy(pending => pending.Id)
                .Take(overflow)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            db.PendingCloudEvents.RemoveRange(staleRows);
            _logger.LogWarning("Offline cloud queue reached capacity; dropped {Count} oldest buffered event(s).", staleRows.Count);
        }

        db.PendingCloudEvents.Add(new PendingCloudEvent
        {
            EventType = monitoringEvent.EventType,
            MetadataJson = CloudEventPublisher.SerializeMetadata(monitoringEvent.Metadata),
            Timestamp = monitoringEvent.Timestamp,
            EnqueuedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns buffered events in FIFO order.
    /// </summary>
    public async Task<List<PendingCloudEvent>> GetPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();

        return await db.PendingCloudEvents
            .OrderBy(pending => pending.EnqueuedAt)
            .ThenBy(pending => pending.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Removes a buffered event after it has been accepted or intentionally discarded.
    /// </summary>
    public async Task RemoveAsync(int pendingEventId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KidMonitorDbContext>();

        var pending = await db.PendingCloudEvents
            .FirstOrDefaultAsync(row => row.Id == pendingEventId, cancellationToken)
            .ConfigureAwait(false);

        if (pending is null)
        {
            return;
        }

        db.PendingCloudEvents.Remove(pending);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
