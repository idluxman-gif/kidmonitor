# KidMonitor Parent Mobile App

Expo-managed React Native scaffold for the parent-facing mobile client.

## Quick start

```powershell
cd mobile
Copy-Item .env.example .env
pnpm install
pnpm start
```

Set `EXPO_PUBLIC_API_BASE_URL` to the cloud API you want to hit. The default example points at the Milestone 5 staging host and can be overridden for local emulator or preview targets.

## Included in this scaffold

- Auth flow with `Login` and `Register` screens
- Persisted session storage via `expo-secure-store`
- Axios API client with bearer-token injection and one-shot refresh on `401`
- App navigation for Dashboard, Notifications, Reports, and Settings
- Best-effort native push token registration that skips Expo Go gracefully
- Data-capable dashboard, notification history, reports, and settings screens wired to the expected cloud endpoints

## Notes

- Expo Go can run the shell and auth UI, but Expo's current documentation requires a development build for real native push-notification testing.
- The default staging host in `.env.example` is currently a placeholder. Use the real deployed API URL when it is available.
