import { AuthService } from '@/features/auth/authService';
import { AuthSession, LoginInput, PushTokenRegistration, RegisterInput } from '@/features/auth/types';

class MemorySessionRepository {
  public current: AuthSession | null = null;

  async load(): Promise<AuthSession | null> {
    return this.current;
  }

  async save(session: AuthSession): Promise<void> {
    this.current = session;
  }

  async clear(): Promise<void> {
    this.current = null;
  }
}

const storedSession: AuthSession = {
  accessToken: 'stored-access',
  refreshToken: 'stored-refresh',
  parentId: '9d3a6d59-c96f-4a39-a7e3-ff8c4f70fa1b',
  email: 'stored@kidmonitor.test',
  displayName: 'Stored Parent',
};

const refreshedSession: AuthSession = {
  ...storedSession,
  accessToken: 'fresh-access',
  refreshToken: 'fresh-refresh',
};

const loginInput: LoginInput = {
  email: 'stored@kidmonitor.test',
  password: 'Secret123!',
};

const registerInput: RegisterInput = {
  displayName: 'Stored Parent',
  email: 'stored@kidmonitor.test',
  password: 'Secret123!',
};

const pushToken: PushTokenRegistration = {
  platform: 'fcm',
  token: 'device-token-123',
};

describe('AuthService', () => {
  test('refreshes a stored session during restore and persists new credentials', async () => {
    const repository = new MemorySessionRepository();
    repository.current = storedSession;
    const gateway = {
      login: jest.fn(),
      register: jest.fn(),
      refresh: jest.fn().mockResolvedValue(refreshedSession),
      logout: jest.fn(),
      registerPushToken: jest.fn(),
    };
    const pushRegistrar = {
      getPushToken: jest.fn().mockResolvedValue(pushToken),
    };
    const service = new AuthService({ repository, gateway, pushRegistrar });

    await expect(service.restore()).resolves.toEqual(refreshedSession);
    expect(gateway.refresh).toHaveBeenCalledWith(storedSession.refreshToken);
    expect(repository.current).toEqual(refreshedSession);
  });

  test('clears the local session if refresh fails during restore', async () => {
    const repository = new MemorySessionRepository();
    repository.current = storedSession;
    const gateway = {
      login: jest.fn(),
      register: jest.fn(),
      refresh: jest.fn().mockRejectedValue(new Error('expired')),
      logout: jest.fn(),
      registerPushToken: jest.fn(),
    };
    const pushRegistrar = {
      getPushToken: jest.fn(),
    };
    const service = new AuthService({ repository, gateway, pushRegistrar });

    await expect(service.restore()).resolves.toBeNull();
    expect(repository.current).toBeNull();
  });

  test('stores a login result and registers a push token without blocking auth on push failure', async () => {
    const repository = new MemorySessionRepository();
    const gateway = {
      login: jest.fn().mockResolvedValue(refreshedSession),
      register: jest.fn(),
      refresh: jest.fn(),
      logout: jest.fn(),
      registerPushToken: jest.fn().mockRejectedValue(new Error('expo-go')),
    };
    const pushRegistrar = {
      getPushToken: jest.fn().mockResolvedValue(pushToken),
    };
    const service = new AuthService({ repository, gateway, pushRegistrar });

    await expect(service.login(loginInput)).resolves.toEqual(refreshedSession);
    expect(gateway.login).toHaveBeenCalledWith(loginInput);
    expect(gateway.registerPushToken).toHaveBeenCalledWith(refreshedSession.accessToken, pushToken);
    expect(repository.current).toEqual(refreshedSession);
  });

  test('stores a registration result and exposes it as the current session', async () => {
    const repository = new MemorySessionRepository();
    const gateway = {
      login: jest.fn(),
      register: jest.fn().mockResolvedValue(refreshedSession),
      refresh: jest.fn(),
      logout: jest.fn(),
      registerPushToken: jest.fn().mockResolvedValue(undefined),
    };
    const pushRegistrar = {
      getPushToken: jest.fn().mockResolvedValue(pushToken),
    };
    const service = new AuthService({ repository, gateway, pushRegistrar });

    await expect(service.register(registerInput)).resolves.toEqual(refreshedSession);
    expect(gateway.register).toHaveBeenCalledWith(registerInput);
    expect(repository.current).toEqual(refreshedSession);
  });
});
