# KidMonitor

KidMonitor is a Windows background service that helps parents keep an eye on their child's PC usage. It tracks which apps are open, detects inappropriate language in text and video audio, and sends real-time Windows notifications — plus a daily summary every evening.

## Features

- **App screen-time tracking** — logs how long monitored apps (Chrome, Edge, YouTube, WhatsApp, etc.) are open each day.
- **Real-time foul-language alerts** — scans text visible in monitored windows and (optionally) YouTube audio for words from a configurable word list. Sends a Windows toast notification when a match is found.
- **Daily summary** — at a configurable time each evening a summary of the day's screen time and language events is generated and sent as a notification.
- **Local dashboard** — a PIN-protected web UI at `http://localhost:5110` shows today's screen time, language events, and past daily reports. Accessible from the parent's browser on the same machine.
- **System tray icon** — `KidMonitor.Tray` sits in the notification area, shows service health, and provides a one-click link to the dashboard.
- **Windows Installer** — a single `.msi` produced by `KidMonitor.Installer` installs the service, tray app, data directory, and default config in one step.

## Requirements

- Windows 10 (version 1803+) or Windows 11, 64-bit
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (Desktop Runtime for the tray app; ASP.NET Core Runtime for the service)
- Administrator privileges for installation

## Quick start (installer)

See [docs/INSTALL.md](docs/INSTALL.md) for step-by-step installation instructions aimed at non-technical users.

## Project layout

| Folder | What it is |
|---|---|
| `KidMonitor.Core` | Shared models, EF Core `DbContext`, and `MonitoringOptions` configuration classes |
| `KidMonitor.Service` | The Windows Service: monitoring workers, language detection, notification pipeline, and embedded dashboard API |
| `KidMonitor.Tray` | WinForms system-tray app that shows service status and opens the dashboard |
| `KidMonitor.Installer` | WiX 5 `.msi` installer |
| `KidMonitor.Tests` | xUnit test suite |

## Building from source

```powershell
# Restore and build everything
dotnet build KidMonitor.sln -c Release

# Run tests
dotnet test KidMonitor.sln -c Release

# Build the installer (requires WiX Toolset 5 — installed automatically as a NuGet SDK)
dotnet build KidMonitor.Installer\KidMonitor.Installer.wixproj -c Release
# Output: KidMonitor.Installer\bin\Release\en-US\KidMonitorSetup.msi
```

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for a full developer setup guide.

## Configuration

All settings live in `C:\ProgramData\KidMonitor\appsettings.json` after installation. The dashboard `PUT /api/config` endpoint can update monitoring options at runtime without restarting the service.

See [docs/CONFIGURATION.md](docs/CONFIGURATION.md) for a complete reference of every configuration field.

## License

Private / proprietary. All rights reserved.
