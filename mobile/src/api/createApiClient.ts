import axios, { AxiosAdapter, AxiosError, AxiosInstance, AxiosResponse, InternalAxiosRequestConfig } from 'axios';

import { AuthSession } from '@/features/auth/types';

interface RetriableRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
  skipAuthRefresh?: boolean;
}

export interface CreateApiClientOptions {
  baseURL: string;
  getSession(): Promise<AuthSession | null>;
  refreshSession(currentSession: AuthSession): Promise<AuthSession | null>;
  clearSession(): Promise<void>;
  adapter?: AxiosAdapter;
}

export function createApiClient(options: CreateApiClientOptions): AxiosInstance {
  const client = axios.create({
    adapter: options.adapter,
    baseURL: options.baseURL,
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
    },
  });

  client.interceptors.request.use(async (config) => {
    const requestConfig = config as RetriableRequestConfig;

    if (requestConfig.headers.Authorization) {
      return requestConfig;
    }

    const currentSession = await options.getSession();
    if (!currentSession) {
      return requestConfig;
    }

    requestConfig.headers.Authorization = `Bearer ${currentSession.accessToken}`;
    return requestConfig;
  });

  const handleAuthError = async (error: AxiosError) => {
    const requestConfig = error.config as RetriableRequestConfig | undefined;
    const statusCode = error.response?.status;

    if (!requestConfig || statusCode !== 401 || requestConfig._retry || requestConfig.skipAuthRefresh) {
      throw error;
    }

    requestConfig._retry = true;

    const currentSession = await options.getSession();
    if (!currentSession) {
      await options.clearSession();
      throw error;
    }

    const refreshedSession = await options.refreshSession(currentSession);
    if (!refreshedSession) {
      await options.clearSession();
      throw error;
    }

    requestConfig.headers.Authorization = `Bearer ${refreshedSession.accessToken}`;
    return client(requestConfig);
  };

  client.interceptors.response.use(
    async (response) => {
      if (isSuccessfulResponse(response)) {
        return response;
      }

      return handleAuthError(
        new AxiosError(
          `Request failed with status code ${response.status}`,
          undefined,
          response.config,
          undefined,
          response,
        ),
      );
    },
    handleAuthError,
  );

  return client;
}

function isSuccessfulResponse(response: AxiosResponse): boolean {
  const validateStatus =
    response.config.validateStatus ??
    ((status: number) => status >= 200 && status < 300);

  return validateStatus(response.status);
}
