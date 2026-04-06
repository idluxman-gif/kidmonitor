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

## Cloud API (Staging / Production)

`KidMonitor.Api` is an ASP.NET Core 8 web API that acts as the cloud relay between the Windows service and the parent's mobile app. It is deployed as a Docker container.

### Deploy to Railway (recommended)

1. Create a new Railway project and connect this GitHub repository.
2. Railway auto-detects `railway.toml` and builds via `KidMonitor.Api/Dockerfile`.
3. Add a **PostgreSQL** plugin to the project (Railway dashboard → **+ New** → **Database** → **Add PostgreSQL**). Railway injects `DATABASE_URL` automatically.
4. Set the following variables under **Variables** (service level):

   | Variable | Description |
   |---|---|
   | `JWT_SECRET` | Random 32+ char secret (`openssl rand -base64 48`) |
   | `FIREBASE_CREDENTIAL_JSON` | Firebase service-account JSON (single-line) |
   | `APNS_KEY_ID` | Apple APNs key ID |
   | `APNS_TEAM_ID` | Apple developer Team ID |
   | `APNS_BUNDLE_ID` | iOS app bundle ID |
   | `APNS_P8_KEY_CONTENT` | Contents of the `.p8` private key file |
   | `APNS_USE_SANDBOX` | `true` for TestFlight, `false` for production |

   See [`.env.example`](.env.example) for the full variable reference.

5. Railway applies EF Core migrations automatically on startup (`db.Database.Migrate()`).
6. Verify the deployment: `curl https://<your-app>.up.railway.app/health` → `{"status":"ok"}`.

### Staging URL

> `https://kidmonitor-api-staging.onrender.com`
>
> Health check: `curl https://kidmonitor-api-staging.onrender.com/health` → `{"status":"ok"}`
>
> Hosted on Render (Free tier, Oregon). Auto-deploys from `master` branch of [idluxman-gif/kidmonitor](https://github.com/idluxman-gif/kidmonitor).
> Free PostgreSQL expires **2026-05-06** — upgrade to a paid plan before then to retain data.
> Note: free-tier instances spin down after inactivity; first request after idle may take ~50 seconds.

### Deploy to Render (alternative)

1. Create a new **Web Service** and connect this repo.
2. Set **Docker** as the runtime, Dockerfile path: `KidMonitor.Api/Dockerfile`, root directory: `.`.
3. Add a **PostgreSQL** managed database; copy the Internal Connection String to `DATABASE_URL`.
4. Add all variables from the table above in the **Environment** tab; also add `ASPNETCORE_URLS=http://+:$PORT`.
5. Render applies migrations on startup and exposes the service on its generated `.onrender.com` URL.

## License

Private / proprietary. All rights reserved.
