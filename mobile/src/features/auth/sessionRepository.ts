import { AuthSession } from '@/features/auth/types';

export const SESSION_STORAGE_KEY = 'kidmonitor.auth.session';

export interface ValueStore {
  getItem(key: string): Promise<string | null>;
  setItem(key: string, value: string): Promise<void>;
  deleteItem(key: string): Promise<void>;
}

function isAuthSession(value: unknown): value is AuthSession {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<AuthSession>;

  return [
    candidate.accessToken,
    candidate.refreshToken,
    candidate.parentId,
    candidate.email,
    candidate.displayName,
  ].every((field) => typeof field === 'string' && field.length > 0);
}

export class SessionRepository {
  public constructor(
    private readonly store: ValueStore,
    private readonly storageKey = SESSION_STORAGE_KEY,
  ) {}

  public async load(): Promise<AuthSession | null> {
    const rawSession = await this.store.getItem(this.storageKey);
    if (!rawSession) {
      return null;
    }

    try {
      const parsed = JSON.parse(rawSession);
      if (!isAuthSession(parsed)) {
        await this.clear();
        return null;
      }

      return parsed;
    } catch {
      await this.clear();
      return null;
    }
  }

  public async save(session: AuthSession): Promise<void> {
    await this.store.setItem(this.storageKey, JSON.stringify(session));
  }

  public async clear(): Promise<void> {
    await this.store.deleteItem(this.storageKey);
  }
}
