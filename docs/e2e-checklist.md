# KidMonitor Milestone 5 — E2E Test Checklist

> **Related issue:** WHA-58
> **Dependencies:** WHA-51 · WHA-52 · WHA-53 · WHA-54 · WHA-55 · WHA-56 · WHA-57
> **Last updated:** 2026-04-04

This checklist must be completed against the **staging** environment with a real Android device before Milestone 5 is declared done.

---

## Prerequisites

- [ ] Cloud API is deployed to staging and `/health` returns `200 OK`
- [ ] PostgreSQL staging DB is migrated (`events`, `parents`, `devices`, `push_tokens` tables exist)
- [ ] KidMonitor PC service is installed and running on a Windows test machine (v1.x)
- [ ] Android test device has the KidMonitor mobile app installed (Expo Go or signed APK)
- [ ] Firebase project is configured; test device FCM token is registered via `POST /push-tokens`
- [ ] At least one parent account exists on staging; device paired to it

---

## Scenario 1 — Happy Path: Foul Language → Push Notification

**Goal:** PC detects foul language → cloud stores event → FCM push received on Android within 10 s

| # | Step | Expected | Pass/Fail |
|---|------|----------|-----------|
| 1 | Trigger a foul-language detection on the PC (type a flagged word in WhatsApp Desktop or use the test harness) | KidMonitor service log shows `[ContentCapture] Foul language detected` | |
| 2 | Check cloud API DB: `SELECT * FROM events ORDER BY received_at DESC LIMIT 1;` | Row with `kind='content_alert'` and correct `device_id` appears within 5 s | |
| 3 | Observe Android test device | Push notification titled "Content Alert" arrives within 10 s of detection | |
| 4 | Tap the notification | Mobile app opens Notification History screen; event detail is visible | |

---

## Scenario 2 — Offline Resilience: PC Buffers Events When Cloud Is Down

**Goal:** Cloud API down → PC buffers events → API comes back → events flushed and push delivered

| # | Step | Expected | Pass/Fail |
|---|------|----------|-----------|
| 1 | Stop the cloud API staging instance (or block outbound traffic on the PC) | `CloudSyncService` log shows `Cloud API unreachable; buffering events` | |
| 2 | Trigger 3 foul-language detection events on the PC | No push notifications arrive; PC log shows events queued in local SQLite buffer | |
| 3 | Verify local buffer: check `kidmonitor_offline_queue.db` (or equivalent SQLite file) | 3 rows present | |
| 4 | Restore the cloud API / unblock traffic | PC log shows `Flushing N buffered events` | |
| 5 | Check cloud API DB | All 3 buffered events appear in `events` table within 30 s | |
| 6 | Observe Android test device | 3 push notifications (or 1 batched notification) arrive | |

---

## Scenario 3 — Pairing Flow: QR Code Device Registration

**Goal:** New device pairs via QR → device token issued → PC starts posting events immediately

| # | Step | Expected | Pass/Fail |
|---|------|----------|-----------|
| 1 | On a fresh PC install (no paired device), right-click tray icon → "Pair with parent app" | Pairing dialog opens with 6-digit code and QR code | |
| 2 | Open mobile app → Settings → "Add Device" → scan QR code | Mobile app sends pairing request to cloud API | |
| 3 | Cloud API returns device token to PC | PC dialog shows "Paired successfully!"; dialog closes | |
| 4 | Trigger a detection event on the PC | Event appears in cloud DB within 5 s; push notification arrives on mobile | |
| 5 | Mobile app → Settings → Devices | Newly paired PC appears in the device list | |

---

## Scenario 4 — Auth: Expired JWT Auto-Refresh

**Goal:** Expired JWT on mobile app → auto-refresh → no user-visible interruption

| # | Step | Expected | Pass/Fail |
|---|------|----------|-----------|
| 1 | Manually expire the parent JWT (wait for token TTL or patch the token expiry in DB) | — | |
| 2 | Navigate within the mobile app (e.g., Dashboard → Reports) | App silently calls `POST /auth/refresh` | |
| 3 | Check mobile app network logs (React Native Debugger / Flipper) | `401` response triggers interceptor; new access token obtained | |
| 4 | User sees the Reports screen normally | No login prompt; no error screen | |
| 5 | Repeat step 2 with an invalid refresh token (simulate expiry) | App redirects to Login screen with "Session expired" message | |

---

## Scenario 5 — Rate Limiting: PC Backs Off on 429

**Goal:** 61+ events/min from PC → cloud returns 429 → PC backs off gracefully

| # | Step | Expected | Pass/Fail |
|---|------|----------|-----------|
| 1 | Configure test harness to fire 65 synthetic events in 60 s from the PC service | — | |
| 2 | Monitor cloud API logs | After 60th event in 60 s window, API returns `429 Too Many Requests` | |
| 3 | Check PC service logs | `CloudSyncService` logs `Rate limited (429); backing off for Xs` | |
| 4 | Wait for back-off window to expire | PC resumes posting events; subsequent events accepted normally (202) | |
| 5 | Verify no events were lost | All 65 events eventually appear in cloud DB (some delayed by back-off) | |

---

## Scenario 6 — Multi-Device: Two PCs, One Parent

**Goal:** 2 PCs paired to 1 parent → both appear in dashboard; pushes from both received

| # | Step | Expected | Pass/Fail |
|---|------|----------|-----------|
| 1 | Pair a second PC (different machine or new device key) to the same parent account | Second device appears in cloud DB `devices` table | |
| 2 | Mobile app → Settings → Devices | Both devices listed with distinct names | |
| 3 | Trigger a detection event on PC #1 | Push notification identifies "PC #1" as the source | |
| 4 | Trigger a detection event on PC #2 | Push notification identifies "PC #2" as the source | |
| 5 | Mobile app → Dashboard | Activity feed shows events from both PCs interleaved by timestamp | |
| 6 | Remove PC #2 via mobile app (Settings → Devices → Remove) | PC #2 device token is revoked; subsequent events from PC #2 return 401 | |

---

## Sign-off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| QA Engineer | | | |
| Engineering Lead | | | |
| Product Owner | | | |

---

## Notes

- Scenarios 1–5 can be partially verified using the Newman CI regression run (see `docs/postman/`)
- Scenario 6 (multi-device) and scenario 3 (pairing) require manual execution
- All scenarios require push notifications to be tested manually on a real Android device (FCM does not work on emulators without play services)
