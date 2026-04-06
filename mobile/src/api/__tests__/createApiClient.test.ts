import { createApiClient } from '@/api/createApiClient';
import { AuthSession } from '@/features/auth/types';

const storedSession: AuthSession = {
  accessToken: 'old-access',
  refreshToken: 'old-refresh',
  parentId: '62714d38-97d7-4ebd-8aa8-85086a697c84',
  email: 'parent@kidmonitor.test',
  displayName: 'Parent Test',
};

const refreshedSession: AuthSession = {
  ...storedSession,
  accessToken: 'new-access',
  refreshToken: 'new-refresh',
};

describe('createApiClient', () => {
  test('adds the current bearer token to outgoing requests', async () => {
    let seenAuthorization = '';
    const client = createApiClient({
      baseURL: 'https://kidmonitor.test',
      getSession: async () => storedSession,
      refreshSession: async () => refreshedSession,
      clearSession: async () => undefined,
      adapter: async (config) => {
        seenAuthorization = String(config.headers?.Authorization ?? '');
        return {
          config,
          data: { ok: true },
          headers: {},
          status: 200,
          statusText: 'OK',
        };
      },
    });

    await expect(client.get('/reports')).resolves.toMatchObject({
      data: { ok: true },
    });
    expect(seenAuthorization).toBe('Bearer old-access');
  });

  test('refreshes once after a 401 and retries with the new token', async () => {
    let currentSession: AuthSession | null = storedSession;
    let attempts = 0;
    const client = createApiClient({
      baseURL: 'https://kidmonitor.test',
      getSession: async () => currentSession,
      refreshSession: async () => {
        currentSession = refreshedSession;
        return refreshedSession;
      },
      clearSession: async () => {
        currentSession = null;
      },
      adapter: async (config) => {
        attempts += 1;
        if (attempts === 1) {
          return {
            config,
            data: { error: 'expired' },
            headers: {},
            status: 401,
            statusText: 'Unauthorized',
          };
        }

        expect(String(config.headers?.Authorization)).toBe('Bearer new-access');
        return {
          config,
          data: { ok: true },
          headers: {},
          status: 200,
          statusText: 'OK',
        };
      },
    });

    await expect(client.get('/dashboard')).resolves.toMatchObject({
      data: { ok: true },
    });
    expect(attempts).toBe(2);
  });

  test('clears the local session when refresh cannot recover from a 401', async () => {
    let cleared = false;
    const client = createApiClient({
      baseURL: 'https://kidmonitor.test',
      getSession: async () => storedSession,
      refreshSession: async () => null,
      clearSession: async () => {
        cleared = true;
      },
      adapter: async (config) => ({
        config,
        data: { error: 'expired' },
        headers: {},
        status: 401,
        statusText: 'Unauthorized',
      }),
    });

    await expect(client.get('/notifications')).rejects.toMatchObject({
      response: {
        status: 401,
      },
    });
    expect(cleared).toBe(true);
  });
});
