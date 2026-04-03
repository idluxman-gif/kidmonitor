using System.Text.Json;
using KidMonitor.Service.Dashboard;

namespace KidMonitor.Tests.Service;

public sealed class DashboardConfigFileTests
{
    [Fact]
    public void WriteDashboardPin_CreatesConfigFile_WhenMissing()
    {
        using var tempDirectory = new TemporaryDirectory();

        DashboardConfigFile.WriteDashboardPin("4321", tempDirectory.Path);

        var json = File.ReadAllText(Path.Combine(tempDirectory.Path, "appsettings.json"));
        using var document = JsonDocument.Parse(json);

        Assert.Equal("4321", document.RootElement.GetProperty("Dashboard").GetProperty("Pin").GetString());
        Assert.Equal(5110, document.RootElement.GetProperty("Dashboard").GetProperty("Port").GetInt32());
    }

    [Fact]
    public void WriteDashboardPin_PreservesExistingSections_WhenUpdatingPin()
    {
        using var tempDirectory = new TemporaryDirectory();
        var configPath = Path.Combine(tempDirectory.Path, "appsettings.json");
        File.WriteAllText(configPath, """
            {
              "Monitoring": {
                "PollIntervalSeconds": 15
              },
              "Dashboard": {
                "Pin": "0000",
                "Port": 5110
              }
            }
            """);

        DashboardConfigFile.WriteDashboardPin("9876", tempDirectory.Path);

        var json = File.ReadAllText(configPath);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(15, document.RootElement.GetProperty("Monitoring").GetProperty("PollIntervalSeconds").GetInt32());
        Assert.Equal("9876", document.RootElement.GetProperty("Dashboard").GetProperty("Pin").GetString());
        Assert.Equal(5110, document.RootElement.GetProperty("Dashboard").GetProperty("Port").GetInt32());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"KidMonitor_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
