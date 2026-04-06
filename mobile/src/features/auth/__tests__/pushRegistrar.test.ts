import { buildPushTokenRegistration } from '@/features/auth/pushRegistrar';

describe('buildPushTokenRegistration', () => {
  test('maps Android device tokens to the API fcm payload', () => {
    expect(buildPushTokenRegistration('android', 'android-token')).toEqual({
      platform: 'fcm',
      token: 'android-token',
    });
  });

  test('maps iOS device tokens to the API apns payload', () => {
    expect(buildPushTokenRegistration('ios', 'ios-token')).toEqual({
      platform: 'apns',
      token: 'ios-token',
    });
  });

  test('returns null for unsupported platforms or empty tokens', () => {
    expect(buildPushTokenRegistration('web', 'web-token')).toBeNull();
    expect(buildPushTokenRegistration('android', '')).toBeNull();
  });
});
