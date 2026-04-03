using System.Text.Json;
using System.Text.Json.Nodes;
using KidMonitor.Core.Configuration;

namespace KidMonitor.Service.Dashboard;

/// <summary>
/// Reads and writes the persisted dashboard configuration file.
/// </summary>
public static class DashboardConfigFile
{
    private const string DefaultProgramDataPath = @"C:\ProgramData\KidMonitor";

    /// <summary>
    /// Returns the effective ProgramData directory for the dashboard config.
    /// </summary>
    public static string GetProgramDataPath(IConfiguration configuration)
        => configuration["Dashboard:ProgramDataPath"] ?? DefaultProgramDataPath;

    /// <summary>
    /// Returns the appsettings.json path for a ProgramData directory.
    /// </summary>
    public static string GetConfigPath(string? programDataPath = null)
        => Path.Combine(programDataPath ?? DefaultProgramDataPath, "appsettings.json");

    /// <summary>
    /// Reads the monitoring options, preferring the persisted config file when present.
    /// </summary>
    public static MonitoringOptions ReadMonitoringOptions(IConfiguration configuration)
    {
        var configPath = GetConfigPath(GetProgramDataPath(configuration));
        if (File.Exists(configPath))
        {
            var persistedConfiguration = new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: false)
                .Build();

            var persistedMonitoring = persistedConfiguration.GetSection("Monitoring").Get<MonitoringOptions>();
            if (persistedMonitoring is not null)
            {
                return persistedMonitoring;
            }
        }

        return configuration.GetSection("Monitoring").Get<MonitoringOptions>() ?? new MonitoringOptions();
    }

    /// <summary>
    /// Writes the dashboard PIN to the persisted config file, preserving unrelated sections.
    /// </summary>
    public static void WriteDashboardPin(string pin, string? programDataPath = null)
    {
        var normalizedPin = string.IsNullOrWhiteSpace(pin) ? "0000" : pin.Trim();
        var configPath = GetConfigPath(programDataPath);
        var directory = Path.GetDirectoryName(configPath) ?? throw new InvalidOperationException("Config directory is unavailable.");

        Directory.CreateDirectory(directory);

        var root = File.Exists(configPath)
            ? JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject ?? new JsonObject()
            : new JsonObject();

        var dashboard = root["Dashboard"] as JsonObject ?? new JsonObject();
        dashboard["Pin"] = normalizedPin;
        dashboard["Port"] ??= 5110;
        root["Dashboard"] = dashboard;

        File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
