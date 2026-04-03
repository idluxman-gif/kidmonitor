# Developer Setup Guide

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| Windows 10/11 x64 | — | Service and tray app are Windows-only |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.x | Includes the runtime and `dotnet` CLI |
| [WiX Toolset](https://wixtoolset.org/) | 5.x | Installed automatically as a NuGet SDK — no separate install needed |
| Git | any | For cloning/contributing |
| Visual Studio 2022 or VS Code | any | Optional; `dotnet` CLI works without an IDE |

## Clone and build

```powershell
git clone <repo-url>
cd KidMonitor

# Restore NuGet packages and build all projects
dotnet build KidMonitor.sln -c Debug
```

## Solution layout

```
KidMonitor.sln
├── KidMonitor.Core/           # Shared models, EF Core DbContext, configuration classes
├── KidMonitor.Service/        # Windows Service + embedded Kestrel dashboard API
│   ├── ContentCapture/        # Platform-specific content capture adapters
│   ├── Dashboard/             # Minimal API endpoints + PIN auth middleware
│   ├── LanguageDetection/     # Foul-language detector + Whisper transcription
│   ├── appsettings.json       # Bundled default configuration
│   └── Program.cs             # Host setup, DI registration, service install CLI
├── KidMonitor.Tray/           # WinForms system-tray application
├── KidMonitor.Installer/      # WiX 5 .msi installer
│   ├── Package.wxs
│   └── wordlist.txt           # Placeholder foul-word list shipped with installer
└── KidMonitor.Tests/          # xUnit test suite
```

## Running the service locally (development)

The service can run as a normal console app outside of the Windows Service infrastructure:

```powershell
cd KidMonitor.Service
dotnet run
```

It will load `appsettings.Development.json` (if present) and use the `ASPNETCORE_ENVIRONMENT=Development` profile, which skips Windows Service lifecycle calls.

The embedded dashboard is available at `http://localhost:5110` by default.

## Running the tray app locally

```powershell
cd KidMonitor.Tray
dotnet run
```

The tray app polls `http://localhost:5110/api/health` every 30 seconds to show service status. Start the service first so the tray app shows "Service: Running ✓".

## Running tests

```powershell
dotnet test KidMonitor.sln -c Debug
```

Tests use an in-memory SQLite database and do not require the service to be running. All workers and API endpoints are covered by the `KidMonitor.Tests` project.

### Test project layout

```
KidMonitor.Tests/
├── Api/                   # Dashboard endpoint and PIN auth integration tests
├── ContentCapture/        # Per-adapter unit tests (YouTube, WhatsApp, GameChat)
├── Data/                  # DbContext schema tests
├── LanguageDetection/     # Foul-language detector and language-detection worker tests
├── Service/               # MonitorWorker, ProcessTrackingWorker, DailySummaryWorker tests
├── Tray/                  # Tray health-poll unit tests
└── TestHelpers/           # Shared in-memory DB helper
```

## Building the installer

The installer project publishes both `KidMonitor.Service` and `KidMonitor.Tray` automatically before packaging:

```powershell
dotnet build KidMonitor.Installer\KidMonitor.Installer.wixproj -c Release
```

Output: `KidMonitor.Installer\bin\Release\en-US\KidMonitorSetup.msi`

> **Note:** The WiX SDK (`WixToolset.Sdk`) is downloaded as a NuGet package on first build. You do not need to install WiX separately.

## Configuration during development

The service merges configuration from two files (in order, later wins):

1. `KidMonitor.Service/appsettings.json` — bundled defaults committed to source control
2. `C:\ProgramData\KidMonitor\appsettings.json` — runtime override file (created by installer; safe to create manually for dev testing)

You can also use `appsettings.Development.json` for local dev overrides (this file is `.gitignore`d).

See [CONFIGURATION.md](CONFIGURATION.md) for a complete field reference.

## Database migrations

The service auto-applies EF Core migrations at startup. To add a new migration:

```powershell
cd KidMonitor.Core
dotnet ef migrations add <MigrationName> --startup-project ..\KidMonitor.Service
```

Migration files live in `KidMonitor.Core/Data/Migrations/`.

## Installing / uninstalling the service manually (without the .msi)

```powershell
# Install as a Windows Service (run as Administrator)
.\KidMonitor.Service.exe --install

# Uninstall
.\KidMonitor.Service.exe --uninstall
```

## Code style

- Target framework: `net8.0-windows` (Service, Tray), `net8.0` (Core), `net8.0-windows` (Tests)
- Nullable reference types enabled across all projects
- xUnit for tests; no mocking framework — prefer in-memory SQLite for database tests
- Follow existing patterns; see `dotnet-best-practices` skill for .NET conventions used in this repo
