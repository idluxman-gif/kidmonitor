import { createApiClient } from '@/api/createApiClient';
import { createFamilyActivityGateway } from '@/features/familyActivity/createFamilyActivityGateway';
import { AuthSession } from '@/features/auth/types';

const session: AuthSession = {
  accessToken: 'access-token',
  refreshToken: 'refresh-token',
  parentId: 'parent-1',
  email: 'parent@kidmonitor.test',
  displayName: 'Parent Test',
};

describe('createFamilyActivityGateway', () => {
  test('calls the mobile dashboard, notifications, reports, and device endpoints', async () => {
    const requests: Array<{ method?: string; url?: string; params?: unknown; data?: unknown }> = [];
    const client = createApiClient({
      baseURL: 'https://kidmonitor.test',
      getSession: async () => session,
      refreshSession: async () => session,
      clearSession: async () => undefined,
      adapter: async (config) => {
        requests.push({
          method: config.method,
          url: config.url,
          params: config.params,
          data: config.data,
        });

        return {
          config,
          data: { ok: true },
          headers: {},
          status: 200,
          statusText: 'OK',
        };
      },
    });

    const gateway = createFamilyActivityGateway(client);

    await gateway.getDashboard();
    await gateway.getNotifications({ page: 2 });
    await gateway.getDailyReport('2026-04-04');
    await gateway.getWeeklyReport();
    await gateway.getDevices();
    await gateway.removeDevice('device-2');
    await gateway.claimPairing('123456');

    expect(requests).toEqual([
      {
        method: 'get',
        url: '/dashboard',
        params: undefined,
        data: undefined,
      },
      {
        method: 'get',
        url: '/notifications',
        params: {
          page: 2,
          parentId: 'me',
        },
        data: undefined,
      },
      {
        method: 'get',
        url: '/reports/daily',
        params: {
          date: '2026-04-04',
        },
        data: undefined,
      },
      {
        method: 'get',
        url: '/reports/weekly',
        params: undefined,
        data: undefined,
      },
      {
        method: 'get',
        url: '/devices',
        params: undefined,
        data: undefined,
      },
      {
        method: 'delete',
        url: '/devices/device-2',
        params: undefined,
        data: undefined,
      },
      {
        method: 'post',
        url: '/pairing/claim',
        params: undefined,
        data: JSON.stringify({
          pairingCode: '123456',
        }),
      },
    ]);
  });
});
