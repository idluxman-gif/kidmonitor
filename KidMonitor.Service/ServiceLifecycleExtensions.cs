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
        // SEC-03: Use NT SERVICE virtual account (least privilege) instead of default LocalSystem.
        // The virtual account is auto-created by Windows SCM and has no password to manage.
        // Grant it only the permissions it needs (data directory RW, see Program.cs ACL setup).
        Run("sc", $"create \"{ServiceName}\" binPath= \"{exePath}\" start= auto DisplayName= \"{ServiceDisplayName}\" obj= \"NT SERVICE\\{ServiceName}\"");
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
        if (process.ExitCode != 0)
        {
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            Console.Error.WriteLine($"sc.exe exited with code {process.ExitCode}.");
            if (!string.IsNullOrWhiteSpace(stdout)) Console.Error.WriteLine(stdout);
            if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr);
            throw new InvalidOperationException(
                $"sc.exe failed (exit code {process.ExitCode}): {stdout} {stderr}".Trim());
        }
    }
}
