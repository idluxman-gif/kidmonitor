using System.Diagnostics;

namespace KidMonitor.Service;

/// <summary>
/// Helper for installing and uninstalling the Windows Service via sc.exe.
/// Run the host binary with --install or --uninstall to manage the service.
/// </summary>
public static class ServiceInstaller
{
    private const string ServiceName = "KidMonitorService";
    private const string ServiceDisplayName = "KidMonitor Parental Service";
    private const string ServiceDescription = "Monitors child PC usage and sends real-time parental alerts.";

    public static int Install(string exePath)
    {
        // sc create KidMonitorService binPath= "..." start= auto
        Run("sc", $"create \"{ServiceName}\" binPath= \"{exePath}\" start= auto DisplayName= \"{ServiceDisplayName}\"");
        Run("sc", $"description \"{ServiceName}\" \"{ServiceDescription}\"");
        // Configure recovery: restart on failure, 3 times, 60s delay
        Run("sc", $"failure \"{ServiceName}\" reset= 86400 actions= restart/60000/restart/60000/restart/60000");
        Console.WriteLine($"Service '{ServiceName}' installed. Run: sc start {ServiceName}");
        return 0;
    }

    public static int Uninstall()
    {
        Run("sc", $"stop \"{ServiceName}\"");
        Run("sc", $"delete \"{ServiceName}\"");
        Console.WriteLine($"Service '{ServiceName}' uninstalled.");
        return 0;
    }

    private static void Run(string fileName, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        })!;
        process.WaitForExit();
    }
}
