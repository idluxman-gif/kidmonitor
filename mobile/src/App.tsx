import 'react-native-reanimated';
import * as React from 'react';
import { StatusBar } from 'expo-status-bar';

import { AuthProvider, useAuth } from '@/features/auth/AuthProvider';
import { createFamilyActivityGateway } from '@/features/familyActivity/createFamilyActivityGateway';
import { SecureStoreNotificationPreferencesStore } from '@/features/familyActivity/notificationPreferencesStore';
import { RootNavigator } from '@/navigation/RootNavigator';

export function App() {
  return (
    <AuthProvider>
      <StatusBar style="dark" />
      <AppShell />
    </AuthProvider>
  );
}

function AppShell() {
  const auth = useAuth();
  const familyActivityGatewayRef = React.useRef(createFamilyActivityGateway(auth.client));
  const notificationPreferencesStoreRef = React.useRef(new SecureStoreNotificationPreferencesStore());

  return (
    <RootNavigator
      apiBaseUrl={auth.apiBaseUrl}
      error={auth.error}
      familyActivityGateway={familyActivityGatewayRef.current}
      notificationPreferencesStore={notificationPreferencesStoreRef.current}
      onClearError={auth.clearError}
      onSignIn={auth.signIn}
      onSignOut={auth.signOut}
      onSignUp={auth.signUp}
      session={auth.session}
      status={auth.status}
    />
  );
}
