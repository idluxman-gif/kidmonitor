import axios from 'axios';

import { AuthGateway } from '@/features/auth/authService';
import { AuthSession, LoginInput, PushTokenRegistration, RegisterInput } from '@/features/auth/types';

export function createAuthGateway(baseURL: string): AuthGateway {
  const client = axios.create({
    baseURL,
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
  });

  return {
    async login(input: LoginInput): Promise<AuthSession> {
      const response = await client.post<AuthSession>('/auth/login', input);
      return response.data;
    },
    async register(input: RegisterInput): Promise<AuthSession> {
      const response = await client.post<AuthSession>('/auth/register', input);
      return response.data;
    },
    async refresh(refreshToken: string): Promise<AuthSession> {
      const response = await client.post<AuthSession>('/auth/refresh', { refreshToken });
      return response.data;
    },
    async logout(accessToken: string): Promise<void> {
      await client.post(
        '/auth/logout',
        undefined,
        {
          headers: {
            Authorization: `Bearer ${accessToken}`,
          },
        },
      );
    },
    async registerPushToken(
      accessToken: string,
      registration: PushTokenRegistration,
    ): Promise<void> {
      await client.post(
        '/push-tokens',
        registration,
        {
          headers: {
            Authorization: `Bearer ${accessToken}`,
          },
        },
      );
    },
  };
}
