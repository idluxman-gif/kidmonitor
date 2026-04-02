namespace KidMonitor.Service.Dashboard;

/// <summary>
/// Configuration for the local parent dashboard (bound from Dashboard config section).
/// </summary>
public class DashboardOptions
{
    /// <summary>
    /// PIN required to access the dashboard. Set during install.
    /// Default is "0000" — should be changed at install time.
    /// </summary>
    public string Pin { get; set; } = "0000";

    /// <summary>Port the dashboard API listens on (loopback only).</summary>
    public int Port { get; set; } = 5110;
}
