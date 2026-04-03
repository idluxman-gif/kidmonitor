# Configuration Reference

KidMonitor reads its configuration from two files, merged in order:

1. **Bundled defaults** — `KidMonitor.Service\appsettings.json` (installed to `C:\Program Files\KidMonitor\appsettings.json`)
2. **User overrides** — `C:\ProgramData\KidMonitor\appsettings.json` (created/updated by the installer and by the dashboard `PUT /api/config` endpoint; takes precedence)

Changes to the override file are picked up automatically by the running service — no restart is required.

---

## Top-level sections

```json
{
  "Monitoring": { ... },
  "Notifications": { ... },
  "FoulLanguage": { ... },
  "LanguageDetection": { ... },
  "Database": { ... },
  "Dashboard": { ... }
}
```

---

## `Monitoring`

General monitoring behaviour.

| Field | Type | Default | Description |
|---|---|---|---|
| `PollIntervalSeconds` | integer | `10` | How often (in seconds) the service polls running processes for screen-time tracking. Lower values increase responsiveness but add minor CPU overhead. |
| `TrackedApps` | array | see below | List of applications to monitor. Each entry has a `ProcessName` (the executable name without `.exe`) and a `DisplayName` (label shown in reports and notifications). |

### `TrackedApps` entry

```json
{ "ProcessName": "chrome", "DisplayName": "Google Chrome" }
```

Default tracked apps after installation:

| ProcessName | DisplayName |
|---|---|
| `chrome` | Google Chrome |
| `msedge` | Microsoft Edge |
| `firefox` | Firefox |
| `WhatsApp` | WhatsApp Desktop |
| `YouTube` | YouTube |

Add or remove entries to control which apps are tracked. The `ProcessName` must match the Windows process name exactly (case-insensitive). You can find process names in Task Manager under the **Details** tab.

### `LanguageDetection` (nested inside `Monitoring`)

Controls the text and audio foul-language detection pipeline.

| Field | Type | Default | Description |
|---|---|---|---|
| `Enabled` | boolean | `true` | Master switch for foul-language detection. Set to `false` to disable completely. |
| `WordList` | string array | `[]` | Inline list of words to flag (plain lowercase words, one per entry). When this list is non-empty it is used instead of the file-based word list (`FoulLanguage:WordListPath`). |
| `AudioEnabled` | boolean | `false` | When `true`, active YouTube sessions are transcribed with Whisper and the transcript is scanned for flagged words. Requires `ModelPath` to point to a valid Whisper GGML model file. |
| `ModelPath` | string | `C:\ProgramData\KidMonitor\ggml-base.bin` | Absolute path to the Whisper GGML model binary (e.g. `ggml-base.bin`). Only used when `AudioEnabled` is `true`. |
| `GpuMode` | string | `"cpu"` | Whisper inference device. Use `"cpu"` (default, always works) or `"cuda"` (requires an NVIDIA GPU with CUDA). |
| `AudioWindowSeconds` | integer | `8` | Rolling audio window length (in seconds) fed to Whisper for each transcription pass. |

---

## `Notifications`

Controls when and how often notifications are sent.

| Field | Type | Default | Description |
|---|---|---|---|
| `AppStartThresholdMinutes` | integer | `5` | Minimum number of minutes an app must be continuously open before a "child started using X" toast notification is sent. Prevents alerts for brief accidental opens. |
| `DailySummaryTimeLocal` | string | `"20:00"` | Local time (`HH:mm` 24-hour format) at which the daily summary is generated and sent as a Windows notification. Change to suit your routine (e.g. `"21:30"`). |
| `FoulLanguageCooldownSeconds` | integer | `60` | Minimum seconds between foul-language alert toasts **per detection source** (each unique app + text/audio combination is throttled independently). Prevents notification spam when the same word appears repeatedly in a short time. |

---

## `FoulLanguage`

File-based word list settings. This section is used when `Monitoring:LanguageDetection:WordList` is empty.

| Field | Type | Default | Description |
|---|---|---|---|
| `Enabled` | boolean | `true` | Whether file-based word-list detection is active. |
| `WordListPath` | string | `C:\ProgramData\KidMonitor\wordlist.txt` | Path to a plain-text file containing flagged words, one word per line (case-insensitive). Edit this file with Notepad to add or remove words. |

---

## `Database`

| Field | Type | Default | Description |
|---|---|---|---|
| `Path` | string | `C:\ProgramData\KidMonitor\kidmonitor.db` | Absolute path to the SQLite database file. The service creates it automatically on first run. Change only if you want to move the data to a different drive. |

---

## `Dashboard`

| Field | Type | Default | Description |
|---|---|---|---|
| `Pin` | string | `"0000"` | 4-digit PIN required to log in to the web dashboard at `http://localhost:5110`. Set during installation; change here or via the dashboard UI. **Do not leave this as the default `0000`.** |
| `Port` | integer | `5110` | TCP port the dashboard listens on (loopback only — not accessible from other machines on the network). Change if port 5110 is already in use by another application. |

---

## Example override file

`C:\ProgramData\KidMonitor\appsettings.json`:

```json
{
  "Monitoring": {
    "PollIntervalSeconds": 10,
    "TrackedApps": [
      { "ProcessName": "chrome",    "DisplayName": "Google Chrome" },
      { "ProcessName": "msedge",    "DisplayName": "Microsoft Edge" },
      { "ProcessName": "WhatsApp",  "DisplayName": "WhatsApp Desktop" }
    ],
    "LanguageDetection": {
      "Enabled": true,
      "WordList": ["badword1", "badword2"],
      "AudioEnabled": false
    }
  },
  "Notifications": {
    "AppStartThresholdMinutes": 5,
    "DailySummaryTimeLocal": "21:00",
    "FoulLanguageCooldownSeconds": 120
  },
  "Dashboard": {
    "Pin": "7391",
    "Port": 5110
  }
}
```
