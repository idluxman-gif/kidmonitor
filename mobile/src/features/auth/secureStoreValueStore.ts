import * as SecureStore from 'expo-secure-store';

import { ValueStore } from '@/features/auth/sessionRepository';

export class SecureStoreValueStore implements ValueStore {
  public async getItem(key: string): Promise<string | null> {
    return SecureStore.getItemAsync(key);
  }

  public async setItem(key: string, value: string): Promise<void> {
    await SecureStore.setItemAsync(key, value);
  }

  public async deleteItem(key: string): Promise<void> {
    await SecureStore.deleteItemAsync(key);
  }
}
