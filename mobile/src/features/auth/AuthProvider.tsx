import { AxiosInstance, isAxiosError } from 'axios';
import * as React from 'react';

import { createApiClient } from '@/api/createApiClient';
import { resolveApiBaseUrl } from '@/config/api';
import { AuthService } from '@/features/auth/authService';
import { createAuthGateway } from '@/features/auth/createAuthGateway';
import { ExpoPushRegistrar } from '@/features/auth/pushRegistrar';
import { SecureStoreValueStore } from '@/features/auth/secureStoreValueStore';
import { SessionRepository } from '@/features/auth/sessionRepository';
import { AuthSession, LoginInput, RegisterInput } from '@/features/auth/types';

export type AuthStatus = 'loading' | 'signedOut' | 'signedIn';

export interface AuthContextValue {
  apiBaseUrl: string;
  clearError(): void;
  client: AxiosInstance;
  error: string | null;
  session: AuthSession | null;
  signIn(input: LoginInput): Promise<void>;
  signOut(): Promise<void>;
  signUp(input: RegisterInput): Promise<void>;
  status: AuthStatus;
}

const AuthContext = React.createContext<AuthContextValue | null>(null);

interface AuthProviderProps {
  children: React.ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const apiBaseUrl = resolveApiBaseUrl();
  const [status, setStatus] = React.useState<AuthStatus>('loading');
  const [session, setSession] = React.useState<AuthSession | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  const sessionRef = React.useRef<AuthSession | null>(null);
  const repositoryRef = React.useRef(new SessionRepository(new SecureStoreValueStore()));
  const gatewayRef = React.useRef(createAuthGateway(apiBaseUrl));
  const pushRegistrarRef = React.useRef(new ExpoPushRegistrar());
  const authServiceRef = React.useRef(
    new AuthService({
      repository: repositoryRef.current,
      gateway: gatewayRef.current,
      pushRegistrar: pushRegistrarRef.current,
    }),
  );

  const syncSession = React.useCallback((nextSession: AuthSession | null) => {
    sessionRef.current = nextSession;
    React.startTransition(() => {
      setSession(nextSession);
      setStatus(nextSession ? 'signedIn' : 'signedOut');
    });
  }, []);

  const clearLocalSession = React.useCallback(async () => {
    await authServiceRef.current.clearLocalSession();
    syncSession(null);
  }, [syncSession]);

  const refreshCurrentSession = React.useCallback(async (currentSession: AuthSession) => {
    const refreshedSession = await authServiceRef.current.refresh(currentSession);
    syncSession(refreshedSession);
    return refreshedSession;
  }, [syncSession]);

  const clientRef = React.useRef<AxiosInstance>(
    createApiClient({
      baseURL: apiBaseUrl,
      getSession: async () => sessionRef.current,
      refreshSession: refreshCurrentSession,
      clearSession: clearLocalSession,
    }),
  );

  const bootstrap = React.useCallback(async () => {
    const restoredSession = await authServiceRef.current.restore();
    syncSession(restoredSession);
  }, [syncSession]);

  React.useEffect(() => {
    void bootstrap();
  }, [bootstrap]);

  async function signIn(input: LoginInput): Promise<void> {
    setError(null);

    try {
      const nextSession = await authServiceRef.current.login(input);
      syncSession(nextSession);
    } catch (authError) {
      setError(toFriendlyError(authError));
      throw authError;
    }
  }

  async function signUp(input: RegisterInput): Promise<void> {
    setError(null);

    try {
      const nextSession = await authServiceRef.current.register(input);
      syncSession(nextSession);
    } catch (authError) {
      setError(toFriendlyError(authError));
      throw authError;
    }
  }

  async function signOut(): Promise<void> {
    setError(null);
    await authServiceRef.current.logout(sessionRef.current);
    syncSession(null);
  }

  function clearError(): void {
    setError(null);
  }

  const value: AuthContextValue = {
    apiBaseUrl,
    clearError,
    client: clientRef.current,
    error,
    session,
    signIn,
    signOut,
    signUp,
    status,
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const value = React.useContext(AuthContext);
  if (!value) {
    throw new Error('useAuth must be used inside AuthProvider.');
  }

  return value;
}

function toFriendlyError(error: unknown): string {
  if (isAxiosError(error)) {
    const apiError = error.response?.data as { error?: unknown } | undefined;
    if (typeof apiError?.error === 'string' && apiError.error.length > 0) {
      return apiError.error;
    }

    if (typeof error.message === 'string' && error.message.length > 0) {
      return error.message;
    }
  }

  if (error instanceof Error && error.message.length > 0) {
    return error.message;
  }

  return 'Something went wrong while talking to the cloud API.';
}
