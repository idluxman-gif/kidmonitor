using Microsoft.Extensions.Logging;

namespace KidMonitor.Tray;

/// <summary>
/// Polls the local dashboard health endpoint for the tray application.
/// </summary>
public sealed class HealthPoller(HttpClient httpClient, ILogger<HealthPoller> logger, TimeSpan? pollInterval = null)
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(30);

    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly ILogger<HealthPoller> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly TimeSpan _pollInterval = pollInterval ?? DefaultPollInterval;

    /// <summary>
    /// Performs one health check against <c>/api/health</c>.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the request.</param>
    /// <returns><see langword="true"/> when the service responds successfully; otherwise <see langword="false"/>.</returns>
    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/health", cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogDebug(ex, "Tray health check failed.");
            return false;
        }
    }

    /// <summary>
    /// Polls the health endpoint until cancellation is requested.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token for the polling loop.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await CheckAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
