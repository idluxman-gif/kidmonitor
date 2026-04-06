import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

import { PushRegistrar } from '@/features/auth/authService';
import { PushTokenRegistration } from '@/features/auth/types';

export function buildPushTokenRegistration(
  platform: string,
  token: string | null | undefined,
): PushTokenRegistration | null {
  const normalizedToken = token?.trim();
  if (!normalizedToken) {
    return null;
  }

  if (platform === 'android') {
    return {
      platform: 'fcm',
      token: normalizedToken,
    };
  }

  if (platform === 'ios') {
    return {
      platform: 'apns',
      token: normalizedToken,
    };
  }

  return null;
}

export class ExpoPushRegistrar implements PushRegistrar {
  public async getPushToken(): Promise<PushTokenRegistration | null> {
    if (!Device.isDevice || Constants.appOwnership === 'expo') {
      return null;
    }

    if (Platform.OS === 'android') {
      await Notifications.setNotificationChannelAsync('default', {
        name: 'default',
        importance: Notifications.AndroidImportance.DEFAULT,
      });
    }

    const existingPermissions = await Notifications.getPermissionsAsync();
    let status = existingPermissions.status;

    if (status !== 'granted') {
      status = (await Notifications.requestPermissionsAsync()).status;
    }

    if (status !== 'granted') {
      return null;
    }

    const nativeDeviceToken = await Notifications.getDevicePushTokenAsync();
    return buildPushTokenRegistration(Platform.OS, String(nativeDeviceToken.data ?? ''));
  }
}
