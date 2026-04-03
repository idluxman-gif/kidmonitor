# Installing KidMonitor

This guide walks you through installing KidMonitor on a Windows PC. No technical knowledge is required — just follow the steps in order.

## What you need before you start

- A Windows 10 or Windows 11 PC (64-bit)
- Administrator access to the PC (you will be asked to approve an administrator prompt during installation)
- The KidMonitor installer file: **KidMonitorSetup.msi**

## Step 1 — Download the .NET 8 Runtime

KidMonitor requires the .NET 8 runtime. If it is already installed on the PC you can skip this step.

1. Open a web browser and go to: `https://dotnet.microsoft.com/download/dotnet/8.0`
2. Under **Run apps — Runtime**, download both:
   - **.NET Desktop Runtime 8** (for the tray icon)
   - **ASP.NET Core Runtime 8** (for the background service)
3. Run each downloaded installer and follow the on-screen prompts.

## Step 2 — Run the KidMonitor installer

1. Double-click **KidMonitorSetup.msi**.
2. If Windows asks "Do you want to allow this app to make changes to your device?", click **Yes**.
3. The installer will open. Click **Next** (or **Install** if prompted).
4. When asked for a **Dashboard PIN**, enter a 4-digit number you will remember. This PIN is used to access the monitoring dashboard later.
   > The default is `0000`. **Change it to something only you know.**
5. Click **Install** and wait for the progress bar to complete.
6. Click **Finish**.

KidMonitor is now installed and the monitoring service starts automatically.

## Step 3 — Verify it is running

1. Look at the Windows taskbar in the bottom-right corner (the system tray, near the clock). You should see the KidMonitor shield icon.
2. Click the icon once to open its menu. You should see **Service: Running ✓**.

If it shows **Service: Unreachable ✗**, wait 30 seconds and check again. If the problem persists, see [Troubleshooting](#troubleshooting) below.

## Step 4 — Open the dashboard

1. Right-click (or left-click) the KidMonitor tray icon and select **Open Dashboard**, or open a browser and go to `http://localhost:5110`.
2. Enter your 4-digit PIN and click **Login**.
3. You will see today's screen-time summary and any language detection events.

## What KidMonitor monitors by default

After installation, KidMonitor watches the following apps automatically:

| App | What it tracks |
|---|---|
| Google Chrome | Screen time |
| Microsoft Edge | Screen time |
| Firefox | Screen time |
| WhatsApp Desktop | Screen time + visible chat text |
| YouTube | Screen time + video audio (if audio monitoring is enabled) |

You can add or remove apps from the dashboard (Settings tab) at any time.

## Daily summary notification

Every evening at **8:00 PM** you will receive a Windows notification with a summary of the day's screen time and any flagged language events. You can change this time in the dashboard settings.

## Uninstalling KidMonitor

1. Open **Settings → Apps** (or **Control Panel → Programs → Uninstall a program**).
2. Find **KidMonitor** in the list and click **Uninstall**.
3. Follow the prompts. The uninstaller stops and removes the monitoring service and tray app.

> Your data files in `C:\ProgramData\KidMonitor` (database, config, word list) are **not** deleted by the uninstaller so your history is preserved. You can delete that folder manually if you want to remove all data.

## Troubleshooting

**Tray icon shows "Service: Unreachable"**
- Open **Task Manager → Services** tab and look for **KidMonitorService**. If it shows "Stopped", right-click it and choose **Start**.
- If starting fails, right-click the Start button → **Event Viewer → Windows Logs → Application** and look for errors from source `KidMonitorService`.

**Dashboard asks for a PIN but I forgot it**
- Open `C:\ProgramData\KidMonitor\appsettings.json` in Notepad (requires administrator). Find the `"Dashboard"` section and update the `"Pin"` value to a new 4-digit number. Save and close. The service picks up the change automatically — no restart needed.

**I don't see the tray icon**
- Click the **^** (Show hidden icons) arrow in the system tray to see icons that are minimised. You can drag the KidMonitor icon to the visible area.
- If it is not there at all, navigate to `C:\Program Files\KidMonitor\` in File Explorer and double-click **KidMonitor.Tray.exe**.
