import * as React from 'react';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';

import {
  FamilyActivityGateway,
  NotificationPreferencesStore,
} from '@/features/familyActivity/createFamilyActivityGateway';
import { AuthStatus } from '@/features/auth/AuthProvider';
import { AuthSession, LoginInput, RegisterInput } from '@/features/auth/types';
import { AppStackParamList, AuthStackParamList } from '@/navigation/types';
import { DashboardScreen } from '@/screens/DashboardScreen';
import { LoadingScreen } from '@/screens/LoadingScreen';
import { LoginScreen } from '@/screens/LoginScreen';
import { NotificationsScreen } from '@/screens/NotificationsScreen';
import { RegisterScreen } from '@/screens/RegisterScreen';
import { ReportsScreen } from '@/screens/ReportsScreen';
import { SettingsScreen } from '@/screens/SettingsScreen';
import { navigationTheme, palette } from '@/ui/theme';

interface RootNavigatorProps {
  apiBaseUrl?: string;
  error?: string | null;
  familyActivityGateway?: FamilyActivityGateway;
  notificationPreferencesStore?: NotificationPreferencesStore;
  onClearError?: () => void;
  onSignIn?: (input: LoginInput) => Promise<void>;
  onSignOut?: () => Promise<void>;
  onSignUp?: (input: RegisterInput) => Promise<void>;
  session: AuthSession | null;
  status: AuthStatus;
}

const AuthStack = createNativeStackNavigator<AuthStackParamList>();
const AppStack = createNativeStackNavigator<AppStackParamList>();

const asyncNoop = async () => undefined;
const noop = () => undefined;

export function RootNavigator({
  apiBaseUrl = 'https://staging.kidmonitor.example.com',
  error = null,
  familyActivityGateway,
  notificationPreferencesStore,
  onClearError = noop,
  onSignIn = asyncNoop,
  onSignOut = asyncNoop,
  onSignUp = asyncNoop,
  session,
  status,
}: RootNavigatorProps) {
  if (status === 'loading') {
    return <LoadingScreen />;
  }

  return (
    <NavigationContainer theme={navigationTheme}>
      {status === 'signedIn' && session ? (
        <AppStack.Navigator
          screenOptions={{
            contentStyle: { backgroundColor: palette.canvas },
            headerShadowVisible: false,
          }}
        >
          <AppStack.Screen
            name="Dashboard"
            options={{ headerShown: false }}
          >
            {(props) => (
              <DashboardScreen
                {...props}
                apiBaseUrl={apiBaseUrl}
                gateway={familyActivityGateway}
                session={session}
              />
            )}
          </AppStack.Screen>
          <AppStack.Screen name="Notifications">
            {(props) => (
              <NotificationsScreen
                {...props}
                gateway={familyActivityGateway}
              />
            )}
          </AppStack.Screen>
          <AppStack.Screen name="Reports">
            {(props) => (
              <ReportsScreen
                {...props}
                gateway={familyActivityGateway}
              />
            )}
          </AppStack.Screen>
          <AppStack.Screen name="Settings">
            {(props) => (
              <SettingsScreen
                {...props}
                apiBaseUrl={apiBaseUrl}
                gateway={familyActivityGateway}
                onSignOut={onSignOut}
                preferencesStore={notificationPreferencesStore}
                session={session}
              />
            )}
          </AppStack.Screen>
        </AppStack.Navigator>
      ) : (
        <AuthStack.Navigator
          screenOptions={{
            contentStyle: { backgroundColor: palette.canvas },
            headerShown: false,
          }}
        >
          <AuthStack.Screen name="Login">
            {(props) => (
              <LoginScreen
                {...props}
                error={error}
                onClearError={onClearError}
                onSignIn={onSignIn}
              />
            )}
          </AuthStack.Screen>
          <AuthStack.Screen name="Register">
            {(props) => (
              <RegisterScreen
                {...props}
                error={error}
                onClearError={onClearError}
                onSignUp={onSignUp}
              />
            )}
          </AuthStack.Screen>
        </AuthStack.Navigator>
      )}
    </NavigationContainer>
  );
}
