namespace KidMonitor.Service;

/// <summary>
/// Root hosted service that orchestrates all monitoring sub-tasks.
/// Concrete implementations are added in WHA-6 (lifecycle), WHA-7 (process tracking),
/// WHA-8 (notifications), and WHA-9 (daily summary).
/// </summary>
public class MonitorWorker : BackgroundService
{
    private readonly ILogger<MonitorWorker> _logger;

    public MonitorWorker(ILogger<MonitorWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("KidMonitor service started at {Time}", DateTimeOffset.Now);

        var heartbeatCount = 0;
        var lastLogTime = DateTime.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            heartbeatCount++;
            if ((DateTime.UtcNow - lastLogTime).TotalSeconds >= 60)
            {
                _logger.LogInformation(
                    "KidMonitor watchdog heartbeat #{Count} at {Time} UTC",
                    heartbeatCount, DateTime.UtcNow);
                lastLogTime = DateTime.UtcNow;
            }
        }

        _logger.LogInformation("KidMonitor service stopping at {Time}", DateTimeOffset.Now);
    }
}
