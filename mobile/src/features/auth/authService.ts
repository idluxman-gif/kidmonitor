import { AuthSession, LoginInput, PushTokenRegistration, RegisterInput } from '@/features/auth/types';

export interface SessionRepositoryPort {
  load(): Promise<AuthSession | null>;
  save(session: AuthSession): Promise<void>;
  clear(): Promise<void>;
}

export interface AuthGateway {
  login(input: LoginInput): Promise<AuthSession>;
  register(input: RegisterInput): Promise<AuthSession>;
  refresh(refreshToken: string): Promise<AuthSession>;
  logout(accessToken: string): Promise<void>;
  registerPushToken(
    accessToken: string,
    registration: PushTokenRegistration,
  ): Promise<void>;
}

export interface PushRegistrar {
  getPushToken(): Promise<PushTokenRegistration | null>;
}

export interface AuthServiceDependencies {
  repository: SessionRepositoryPort;
  gateway: AuthGateway;
  pushRegistrar: PushRegistrar;
}

export class AuthService {
  public constructor(private readonly dependencies: AuthServiceDependencies) {}

  public async restore(): Promise<AuthSession | null> {
    const currentSession = await this.dependencies.repository.load();
    if (!currentSession) {
      return null;
    }

    return this.refresh(currentSession);
  }

  public async login(input: LoginInput): Promise<AuthSession> {
    const session = await this.dependencies.gateway.login(input);
    return this.persistAndRegisterPushToken(session);
  }

  public async register(input: RegisterInput): Promise<AuthSession> {
    const session = await this.dependencies.gateway.register(input);
    return this.persistAndRegisterPushToken(session);
  }

  public async refresh(currentSession: AuthSession): Promise<AuthSession | null> {
    try {
      const refreshedSession = await this.dependencies.gateway.refresh(currentSession.refreshToken);
      await this.dependencies.repository.save(refreshedSession);
      return refreshedSession;
    } catch {
      await this.dependencies.repository.clear();
      return null;
    }
  }

  public async logout(currentSession: AuthSession | null): Promise<void> {
    try {
      if (currentSession) {
        await this.dependencies.gateway.logout(currentSession.accessToken);
      }
    } finally {
      await this.dependencies.repository.clear();
    }
  }

  public async clearLocalSession(): Promise<void> {
    await this.dependencies.repository.clear();
  }

  private async persistAndRegisterPushToken(session: AuthSession): Promise<AuthSession> {
    await this.dependencies.repository.save(session);

    try {
      const pushToken = await this.dependencies.pushRegistrar.getPushToken();
      if (pushToken) {
        await this.dependencies.gateway.registerPushToken(session.accessToken, pushToken);
      }
    } catch {
      // Push registration is best-effort so auth stays usable in Expo Go or offline.
    }

    return session;
  }
}
