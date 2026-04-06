// Axios falls back to Expo's fetch polyfill under Jest, which crashes before our
// injected test adapters run. Disable it in tests so interceptor behavior stays isolated.
delete (globalThis as any).fetch;
delete (globalThis as any).Headers;
delete (globalThis as any).Request;
delete (globalThis as any).Response;

jest.mock('expo-notifications', () => ({
  AndroidImportance: {
    DEFAULT: 'default',
  },
  getDevicePushTokenAsync: jest.fn(),
  getPermissionsAsync: jest.fn(),
  requestPermissionsAsync: jest.fn(),
  setNotificationChannelAsync: jest.fn(),
}));

export {};
