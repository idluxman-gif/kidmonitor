import { SessionRepository } from '@/features/auth/sessionRepository';
import { AuthSession } from '@/features/auth/types';

class MemoryValueStore {
  public values = new Map<string, string>();

  async getItem(key: string): Promise<string | null> {
    return this.values.get(key) ?? null;
  }

  async setItem(key: string, value: string): Promise<void> {
    this.values.set(key, value);
  }

  async deleteItem(key: string): Promise<void> {
    this.values.delete(key);
  }
}

const exampleSession: AuthSession = {
  accessToken: 'access-token',
  refreshToken: 'refresh-token',
  parentId: 'b50ec1b6-4c3c-4c9f-a37f-2ad9676cd145',
  email: 'parent@kidmonitor.test',
  displayName: 'Parent Test',
};

describe('SessionRepository', () => {
  test('returns null when no stored session exists', async () => {
    const repo = new SessionRepository(new MemoryValueStore());

    await expect(repo.load()).resolves.toBeNull();
  });

  test('round-trips a stored session', async () => {
    const store = new MemoryValueStore();
    const repo = new SessionRepository(store);

    await repo.save(exampleSession);

    await expect(repo.load()).resolves.toEqual(exampleSession);
  });

  test('clears malformed session payloads instead of throwing', async () => {
    const store = new MemoryValueStore();
    const repo = new SessionRepository(store);

    await store.setItem('kidmonitor.auth.session', '{not-json');

    await expect(repo.load()).resolves.toBeNull();
    await expect(store.getItem('kidmonitor.auth.session')).resolves.toBeNull();
  });
});
