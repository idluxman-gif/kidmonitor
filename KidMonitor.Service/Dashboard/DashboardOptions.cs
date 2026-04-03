namespace KidMonitor.Service.Dashboard;

/// <summary>
/// Configuration for the local parent dashboard (bound from Dashboard config section).
/// </summary>
public class DashboardOptions
{
    /// <summary>The factory-default PIN value that signals "not yet configured".</summary>
    public const string DefaultPin = "0000";

    /// <summary>
    /// PIN required to access the dashboard. Set during install.
    /// When still equal to <see cref="DefaultPin"/>, the setup endpoint must be called first.
    /// </summary>
    public string Pin { get; set; } = DefaultPin;

    /// <summary>Port the dashboard API listens on (loopback only).</summary>
    public int Port { get; set; } = 5110;
}
