import * as SecureStore from 'expo-secure-store';

import {
  defaultNotificationPreferences,
  NotificationPreferences,
  NotificationPreferencesStore,
} from '@/features/familyActivity/createFamilyActivityGateway';

const storageKey = 'kidmonitor.notificationPreferences';

export class SecureStoreNotificationPreferencesStore implements NotificationPreferencesStore {
  public async load(): Promise<NotificationPreferences> {
    const rawValue = await SecureStore.getItemAsync(storageKey);
    if (!rawValue) {
      return defaultNotificationPreferences;
    }

    try {
      const parsed = JSON.parse(rawValue) as Partial<NotificationPreferences>;
      return {
        contentAlerts: parsed.contentAlerts ?? defaultNotificationPreferences.contentAlerts,
        dailySummaries: parsed.dailySummaries ?? defaultNotificationPreferences.dailySummaries,
        weeklyReports: parsed.weeklyReports ?? defaultNotificationPreferences.weeklyReports,
      };
    } catch {
      return defaultNotificationPreferences;
    }
  }

  public async save(preferences: NotificationPreferences): Promise<void> {
    await SecureStore.setItemAsync(storageKey, JSON.stringify(preferences));
  }
}
