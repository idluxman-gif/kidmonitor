import { AxiosInstance } from 'axios';

export interface DeviceSummary {
  id: string;
  name: string;
  status: 'online' | 'offline';
}

export interface FamilyActivityEvent {
  childDeviceName: string;
  detail: string;
  eventType: string;
  icon: 'alert' | 'summary' | 'activity';
  id: string;
  occurredAt: string;
  summary: string;
}

export interface CurrentAppActivity {
  deviceName: string;
  name: string;
  startedAt: string;
}

export interface DashboardSnapshot {
  currentApp: CurrentAppActivity | null;
  devices: DeviceSummary[];
  foulLanguageEventCount: number;
  lastEvent: FamilyActivityEvent | null;
  recentEvents: FamilyActivityEvent[];
  todayEventCount: number;
}

export interface NotificationHistoryPage {
  items: FamilyActivityEvent[];
  nextPage: number | null;
}

export interface ReportAppUsage {
  minutes: number;
  name: string;
}

export interface DailyReportSnapshot {
  date: string;
  foulLanguageEventCount: number;
  topApps: ReportAppUsage[];
  totalScreenTimeMinutes: number;
}

export interface WeeklyReportDayTotal {
  label: string;
  minutes: number;
}

export interface WeeklyReportSnapshot {
  dailyTotals: WeeklyReportDayTotal[];
  foulLanguageEventCount: number;
  topApps: ReportAppUsage[];
  totalScreenTimeMinutes: number;
  weekLabel: string;
}

export interface NotificationPreferences {
  contentAlerts: boolean;
  dailySummaries: boolean;
  weeklyReports: boolean;
}

export interface NotificationPreferencesStore {
  load(): Promise<NotificationPreferences>;
  save(preferences: NotificationPreferences): Promise<void>;
}

export interface FamilyActivityGateway {
  claimPairing(pairingCode: string): Promise<DeviceSummary>;
  getDailyReport(date: string): Promise<DailyReportSnapshot>;
  getDashboard(): Promise<DashboardSnapshot>;
  getDevices(): Promise<DeviceSummary[]>;
  getNotifications(options?: { page?: number }): Promise<NotificationHistoryPage>;
  getWeeklyReport(): Promise<WeeklyReportSnapshot>;
  removeDevice(deviceId: string): Promise<void>;
}

export const defaultNotificationPreferences: NotificationPreferences = {
  contentAlerts: true,
  dailySummaries: true,
  weeklyReports: true,
};

export function createFamilyActivityGateway(client: AxiosInstance): FamilyActivityGateway {
  return {
    async claimPairing(pairingCode) {
      const response = await client.post('/pairing/claim', {
        pairingCode,
      });

      const data = asRecord(response.data);
      const id = toStringValue(data.deviceId ?? data.id) ?? pairingCode;
      const name = toStringValue(data.deviceName ?? data.name) ?? 'Paired device';

      return {
        id,
        name,
        status: 'offline',
      };
    },

    async getDashboard() {
      const response = await client.get('/dashboard');
      return normalizeDashboard(response.data);
    },

    async getNotifications(options) {
      const response = await client.get('/notifications', {
        params: {
          page: options?.page ?? 1,
          parentId: 'me',
        },
      });

      return normalizeNotifications(response.data);
    },

    async getDailyReport(date) {
      const response = await client.get('/reports/daily', {
        params: { date },
      });

      return normalizeDailyReport(response.data, date);
    },

    async getWeeklyReport() {
      const response = await client.get('/reports/weekly');
      return normalizeWeeklyReport(response.data);
    },

    async getDevices() {
      const response = await client.get('/devices');
      return normalizeDevices(response.data);
    },

    async removeDevice(deviceId) {
      await client.delete(`/devices/${deviceId}`);
    },
  };
}

function normalizeDashboard(payload: unknown): DashboardSnapshot {
  const data = asRecord(payload);
  const recentEvents = normalizeEventList(data.recentEvents ?? data.events ?? data.RecentLanguageEvents);
  const devices = normalizeDevices(data.devices);
  const lastEvent = normalizeEvent(data.lastEvent) ?? recentEvents[0] ?? null;
  const currentApp = normalizeCurrentApp(data.currentApp ?? data.activeApp ?? data.currentSession);
  const todayEventCount = toNumber(data.todayEventCount ?? data.eventCountToday ?? data.TotalEventCount) ?? recentEvents.length;
  const foulLanguageEventCount =
    toNumber(data.foulLanguageEventCount ?? data.FoulLanguageEventCount) ??
    recentEvents.filter((event) => event.eventType.includes('foul')).length;

  return {
    currentApp,
    devices,
    foulLanguageEventCount,
    lastEvent,
    recentEvents,
    todayEventCount,
  };
}

function normalizeNotifications(payload: unknown): NotificationHistoryPage {
  if (Array.isArray(payload)) {
    return {
      items: normalizeEventList(payload),
      nextPage: null,
    };
  }

  const data = asRecord(payload);
  return {
    items: normalizeEventList(data.items ?? data.notifications ?? data.events),
    nextPage: toNumber(data.nextPage ?? data.next_page),
  };
}

function normalizeDailyReport(payload: unknown, fallbackDate: string): DailyReportSnapshot {
  const data = asRecord(payload);
  return {
    date: toStringValue(data.date ?? data.Date) ?? fallbackDate,
    foulLanguageEventCount: toNumber(data.foulLanguageEventCount ?? data.FoulLanguageEventCount) ?? 0,
    topApps: normalizeReportAppUsage(data.topApps ?? data.AppBreakdown ?? data.appBreakdown),
    totalScreenTimeMinutes: resolveMinutes(
      data.totalScreenTimeMinutes,
      data.TotalScreenTimeSeconds,
    ),
  };
}

function normalizeWeeklyReport(payload: unknown): WeeklyReportSnapshot {
  const data = asRecord(payload);
  return {
    dailyTotals: normalizeDailyTotals(data.dailyTotals ?? data.days ?? data.DailyTotals),
    foulLanguageEventCount: toNumber(data.foulLanguageEventCount ?? data.FoulLanguageEventCount) ?? 0,
    topApps: normalizeReportAppUsage(data.topApps ?? data.appBreakdown),
    totalScreenTimeMinutes: resolveMinutes(
      data.totalScreenTimeMinutes,
      data.TotalScreenTimeSeconds,
    ),
    weekLabel: toStringValue(data.weekLabel ?? data.label ?? data.RangeLabel) ?? 'This week',
  };
}

function normalizeDevices(payload: unknown): DeviceSummary[] {
  const data = asRecord(payload);
  const items = Array.isArray(payload)
    ? payload
    : Array.isArray(data.items)
      ? (data.items as unknown[])
      : [];

  return items
    .map((item) => {
      const device = asRecord(item);
      const id = toStringValue(device.id ?? device.Id);
      const name = toStringValue(device.name ?? device.deviceName ?? device.DeviceName);

      if (!id || !name) {
        return null;
      }

      const statusValue = String(device.status ?? device.Status ?? '').toLowerCase();
      return {
        id,
        name,
        status: statusValue === 'online' ? 'online' : 'offline',
      } as DeviceSummary;
    })
    .filter((item): item is DeviceSummary => item !== null);
}

function normalizeCurrentApp(payload: unknown): CurrentAppActivity | null {
  const data = asRecord(payload);
  const name = toStringValue(data.name ?? data.appName ?? data.App ?? data.DisplayName);

  if (!name) {
    return null;
  }

  return {
    deviceName: toStringValue(data.deviceName ?? data.DeviceName) ?? 'Child device',
    name,
    startedAt: toStringValue(data.startedAt ?? data.StartedAt) ?? new Date().toISOString(),
  };
}

function normalizeEventList(payload: unknown): FamilyActivityEvent[] {
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload
    .map(normalizeEvent)
    .filter((item): item is FamilyActivityEvent => item !== null);
}

function normalizeEvent(payload: unknown): FamilyActivityEvent | null {
  const data = asRecord(payload);
  const summary =
    toStringValue(data.summary ?? data.title ?? data.MatchedTerm ?? data.AppName) ??
    toStringValue(data.eventType ?? data.kind);

  if (!summary) {
    return null;
  }

  const eventType = toStringValue(data.eventType ?? data.kind ?? data.Source) ?? 'activity';

  return {
    childDeviceName: toStringValue(data.childDeviceName ?? data.deviceName ?? data.DeviceName) ?? 'Child device',
    detail:
      toStringValue(data.detail ?? data.ContextSnippet ?? data.contextSnippet ?? data.Source) ??
      'Open the notification for more detail.',
    eventType,
    icon: normalizeEventIcon(eventType),
    id: toStringValue(data.id ?? data.Id) ?? `${eventType}-${summary}`,
    occurredAt:
      toStringValue(data.occurredAt ?? data.timestamp ?? data.DetectedAt ?? data.receivedAt) ??
      new Date().toISOString(),
    summary,
  };
}

function normalizeReportAppUsage(payload: unknown): ReportAppUsage[] {
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload
    .map((item) => {
      const usage = asRecord(item);
      const name = toStringValue(usage.name ?? usage.app ?? usage.App);

      if (!name) {
        return null;
      }

      return {
        minutes: resolveMinutes(usage.minutes, usage.TotalSeconds),
        name,
      } as ReportAppUsage;
    })
    .filter((item): item is ReportAppUsage => item !== null);
}

function normalizeDailyTotals(payload: unknown): WeeklyReportDayTotal[] {
  if (!Array.isArray(payload)) {
    return [];
  }

  return payload
    .map((item) => {
      const day = asRecord(item);
      const label = toStringValue(day.label ?? day.day ?? day.Date);

      if (!label) {
        return null;
      }

      return {
        label,
        minutes: resolveMinutes(day.minutes, day.totalSeconds ?? day.TotalScreenTimeSeconds),
      } as WeeklyReportDayTotal;
    })
    .filter((item): item is WeeklyReportDayTotal => item !== null);
}

function normalizeEventIcon(eventType: string): FamilyActivityEvent['icon'] {
  if (eventType.includes('summary')) {
    return 'summary';
  }

  if (eventType.includes('foul') || eventType.includes('alert')) {
    return 'alert';
  }

  return 'activity';
}

function resolveMinutes(minutesValue: unknown, secondsValue: unknown): number {
  const explicitMinutes = toNumber(minutesValue);
  if (explicitMinutes !== null) {
    return explicitMinutes;
  }

  const explicitSeconds = toNumber(secondsValue);
  if (explicitSeconds !== null) {
    return Math.round(explicitSeconds / 60);
  }

  return 0;
}

function toNumber(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string' && value.trim().length > 0) {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}

function toStringValue(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : null;
}

function asRecord(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === 'object' ? (value as Record<string, unknown>) : {};
}
