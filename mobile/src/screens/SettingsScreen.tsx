import * as React from 'react';
import { Alert, Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';

import {
  defaultNotificationPreferences,
  DeviceSummary,
  FamilyActivityGateway,
  NotificationPreferences,
  NotificationPreferencesStore,
} from '@/features/familyActivity/createFamilyActivityGateway';
import { AuthSession } from '@/features/auth/types';
import { AppStackParamList } from '@/navigation/types';
import { palette } from '@/ui/theme';

type SettingsScreenProps = NativeStackScreenProps<AppStackParamList, 'Settings'> & {
  apiBaseUrl: string;
  gateway?: FamilyActivityGateway;
  onSignOut(): Promise<void>;
  preferencesStore?: NotificationPreferencesStore;
  session: AuthSession;
};

export function SettingsScreen({
  apiBaseUrl,
  gateway,
  onSignOut,
  preferencesStore,
  session,
}: SettingsScreenProps) {
  const [devices, setDevices] = React.useState<DeviceSummary[]>([]);
  const [error, setError] = React.useState<string | null>(null);
  const [isSigningOut, setIsSigningOut] = React.useState(false);
  const [isClaimingDevice, setIsClaimingDevice] = React.useState(false);
  const [isPairingFormOpen, setIsPairingFormOpen] = React.useState(false);
  const [pairingCode, setPairingCode] = React.useState('');
  const [preferences, setPreferences] = React.useState<NotificationPreferences>(defaultNotificationPreferences);
  const [removingDeviceId, setRemovingDeviceId] = React.useState<string | null>(null);

  React.useEffect(() => {
    const currentGateway = gateway;
    if (!currentGateway) {
      return undefined;
    }
    const activeGateway: FamilyActivityGateway = currentGateway;

    let active = true;

    async function loadDevices() {
      try {
        const nextDevices = await activeGateway.getDevices();
        if (!active) {
          return;
        }

        setDevices(nextDevices);
        setError(null);
      } catch (loadError) {
        if (!active) {
          return;
        }

        setError(loadError instanceof Error ? loadError.message : 'Unable to load paired devices.');
      }
    }

    void loadDevices();

    return () => {
      active = false;
    };
  }, [gateway]);

  React.useEffect(() => {
    const currentPreferencesStore = preferencesStore;
    if (!currentPreferencesStore) {
      return undefined;
    }
    const activePreferencesStore: NotificationPreferencesStore = currentPreferencesStore;

    let active = true;

    async function loadPreferences() {
      const nextPreferences = await activePreferencesStore.load();
      if (active) {
        setPreferences(nextPreferences);
      }
    }

    void loadPreferences();

    return () => {
      active = false;
    };
  }, [preferencesStore]);

  async function handleSignOut() {
    setIsSigningOut(true);

    try {
      await onSignOut();
    } finally {
      setIsSigningOut(false);
    }
  }

  async function handleRemoveDevice(deviceId: string) {
    const currentGateway = gateway;
    if (!currentGateway) {
      return;
    }
    const activeGateway: FamilyActivityGateway = currentGateway;

    setRemovingDeviceId(deviceId);

    try {
      await activeGateway.removeDevice(deviceId);
      setDevices((currentDevices) => currentDevices.filter((device) => device.id !== deviceId));
      setError(null);
    } catch (removeError) {
      setError(removeError instanceof Error ? removeError.message : 'Unable to remove the device.');
    } finally {
      setRemovingDeviceId(null);
    }
  }

  async function togglePreference(key: keyof NotificationPreferences) {
    const nextPreferences = {
      ...preferences,
      [key]: !preferences[key],
    };

    setPreferences(nextPreferences);
    await preferencesStore?.save(nextPreferences);
  }

  async function handleClaimDevice() {
    const currentGateway = gateway;
    if (!currentGateway || pairingCode.trim().length === 0) {
      return;
    }
    const activeGateway: FamilyActivityGateway = currentGateway;

    setIsClaimingDevice(true);

    try {
      const claimedDevice = await activeGateway.claimPairing(pairingCode.trim());
      setDevices((currentDevices) => {
        const withoutExisting = currentDevices.filter((device) => device.id !== claimedDevice.id);
        return [...withoutExisting, claimedDevice].sort((left, right) => left.name.localeCompare(right.name));
      });
      setPairingCode('');
      setIsPairingFormOpen(false);
      setError(null);
      Alert.alert('Device paired', `${claimedDevice.name} is now linked to this parent account.`);
    } catch (claimError) {
      setError(claimError instanceof Error ? claimError.message : 'Unable to claim the pairing code.');
    } finally {
      setIsClaimingDevice(false);
    }
  }

  return (
    <ScrollView
      contentContainerStyle={styles.content}
      style={styles.page}
    >
      <Text style={styles.title}>Settings</Text>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Signed in parent</Text>
        <Text style={styles.value}>{session.displayName}</Text>
        <Text style={styles.helper}>{session.email}</Text>
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Devices</Text>
        {error ? <Text style={styles.error}>{error}</Text> : null}
        <Pressable
          accessibilityRole="button"
          onPress={() => setIsPairingFormOpen((currentValue) => !currentValue)}
          style={styles.addDeviceButton}
        >
          <Text style={styles.addDeviceButtonText}>{isPairingFormOpen ? 'Close pairing' : 'Add device'}</Text>
        </Pressable>
        {isPairingFormOpen ? (
          <View style={styles.pairingForm}>
            <Text style={styles.label}>Pairing code</Text>
            <TextInput
              autoCapitalize="characters"
              keyboardType="number-pad"
              maxLength={6}
              onChangeText={(value) => setPairingCode(value.replace(/[^0-9]/g, ''))}
              placeholder="123456"
              placeholderTextColor={palette.muted}
              style={styles.input}
              value={pairingCode}
            />
            <Pressable
              accessibilityRole="button"
              disabled={pairingCode.trim().length !== 6 || isClaimingDevice}
              onPress={() => {
                void handleClaimDevice();
              }}
              style={[
                styles.claimButton,
                (pairingCode.trim().length !== 6 || isClaimingDevice) && styles.disabledButton,
              ]}
            >
              <Text style={styles.claimButtonText}>
                {isClaimingDevice ? 'Claiming...' : 'Claim device'}
              </Text>
            </Pressable>
            <Text style={styles.helper}>
              Enter the 6-digit code shown on the child's PC pairing dialog.
            </Text>
          </View>
        ) : null}
        {devices.length > 0 ? (
          devices.map((device) => (
            <View
              key={device.id}
              style={styles.deviceRow}
            >
              <View>
                <Text style={styles.value}>{device.name}</Text>
                <Text style={styles.helper}>{device.status === 'online' ? 'Online now' : 'Offline'}</Text>
              </View>
              <Pressable
                accessibilityRole="button"
                onPress={() => {
                  void handleRemoveDevice(device.id);
                }}
                style={styles.removeButton}
              >
                <Text style={styles.removeButtonText}>
                  {removingDeviceId === device.id ? 'Removing...' : `Remove ${device.name}`}
                </Text>
              </Pressable>
            </View>
          ))
        ) : (
          <Text style={styles.helper}>No paired devices yet.</Text>
        )}
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>Notification preferences</Text>
        <PreferenceRow
          accessibilityLabel="Toggle content alerts"
          description="Immediate alerts for foul language and suspicious activity."
          enabled={preferences.contentAlerts}
          label="Content alerts"
          onPress={() => {
            void togglePreference('contentAlerts');
          }}
        />
        <PreferenceRow
          accessibilityLabel="Toggle daily summaries"
          description="Evening summary cards for total screen time and flagged activity."
          enabled={preferences.dailySummaries}
          label="Daily summaries"
          onPress={() => {
            void togglePreference('dailySummaries');
          }}
        />
        <PreferenceRow
          accessibilityLabel="Toggle weekly reports"
          description="Longer weekly report digests with top apps and trend bars."
          enabled={preferences.weeklyReports}
          label="Weekly reports"
          onPress={() => {
            void togglePreference('weeklyReports');
          }}
        />
      </View>

      <View style={styles.card}>
        <Text style={styles.cardTitle}>API endpoint</Text>
        <Text style={styles.value}>{apiBaseUrl}</Text>
        <Text style={styles.helper}>
          Use `EXPO_PUBLIC_API_BASE_URL` to switch between staging and local cloud targets.
        </Text>
      </View>

      <Pressable
        accessibilityRole="button"
        onPress={handleSignOut}
        style={styles.signOutButton}
      >
        <Text style={styles.signOutText}>
          {isSigningOut ? 'Signing out...' : 'Sign out'}
        </Text>
      </Pressable>
    </ScrollView>
  );
}

function PreferenceRow({
  accessibilityLabel,
  description,
  enabled,
  label,
  onPress,
}: {
  accessibilityLabel: string;
  description: string;
  enabled: boolean;
  label: string;
  onPress(): void;
}) {
  return (
    <Pressable
      accessibilityLabel={accessibilityLabel}
      accessibilityRole="switch"
      accessibilityState={{ checked: enabled }}
      onPress={onPress}
      style={styles.preferenceRow}
    >
      <View style={styles.preferenceText}>
        <Text style={styles.value}>{label}</Text>
        <Text style={styles.helper}>{description}</Text>
      </View>
      <View style={[styles.preferencePill, enabled ? styles.preferencePillOn : styles.preferencePillOff]}>
        <Text style={styles.preferencePillText}>{enabled ? 'On' : 'Off'}</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  addDeviceButton: {
    alignSelf: 'flex-start',
    backgroundColor: palette.accentSoft,
    borderRadius: 999,
    marginBottom: 14,
    paddingHorizontal: 14,
    paddingVertical: 10,
  },
  addDeviceButtonText: {
    color: palette.accent,
    fontSize: 13,
    fontWeight: '700',
  },
  card: {
    backgroundColor: palette.card,
    borderColor: palette.line,
    borderRadius: 24,
    borderWidth: 1,
    marginBottom: 14,
    padding: 20,
  },
  cardTitle: {
    color: palette.ink,
    fontSize: 17,
    fontWeight: '700',
    marginBottom: 10,
  },
  claimButton: {
    alignItems: 'center',
    backgroundColor: palette.ink,
    borderRadius: 16,
    marginBottom: 10,
    paddingVertical: 14,
  },
  claimButtonText: {
    color: '#FFFFFF',
    fontSize: 15,
    fontWeight: '700',
  },
  content: {
    padding: 20,
  },
  disabledButton: {
    opacity: 0.55,
  },
  deviceRow: {
    borderTopColor: palette.line,
    borderTopWidth: 1,
    flexDirection: 'row',
    justifyContent: 'space-between',
    paddingTop: 14,
  },
  error: {
    color: palette.danger,
    fontSize: 14,
    marginBottom: 10,
  },
  helper: {
    color: palette.muted,
    fontSize: 14,
    lineHeight: 22,
  },
  input: {
    backgroundColor: '#FFFFFF',
    borderColor: palette.line,
    borderRadius: 16,
    borderWidth: 1,
    color: palette.ink,
    fontSize: 16,
    marginBottom: 12,
    paddingHorizontal: 16,
    paddingVertical: 14,
  },
  label: {
    color: palette.ink,
    fontSize: 14,
    fontWeight: '600',
    marginBottom: 8,
  },
  page: {
    backgroundColor: palette.canvas,
    flex: 1,
  },
  pairingForm: {
    borderTopColor: palette.line,
    borderTopWidth: 1,
    marginBottom: 14,
    paddingTop: 14,
  },
  preferencePill: {
    borderRadius: 999,
    justifyContent: 'center',
    minWidth: 62,
    paddingHorizontal: 14,
    paddingVertical: 10,
  },
  preferencePillOff: {
    backgroundColor: palette.spotlightSoft,
  },
  preferencePillOn: {
    backgroundColor: palette.accentSoft,
  },
  preferencePillText: {
    color: palette.ink,
    fontSize: 13,
    fontWeight: '700',
    textAlign: 'center',
  },
  preferenceRow: {
    alignItems: 'center',
    borderTopColor: palette.line,
    borderTopWidth: 1,
    flexDirection: 'row',
    gap: 12,
    justifyContent: 'space-between',
    paddingTop: 14,
  },
  preferenceText: {
    flex: 1,
  },
  removeButton: {
    alignSelf: 'flex-start',
    backgroundColor: palette.spotlightSoft,
    borderRadius: 999,
    paddingHorizontal: 12,
    paddingVertical: 10,
  },
  removeButtonText: {
    color: palette.danger,
    fontSize: 13,
    fontWeight: '700',
  },
  signOutButton: {
    alignItems: 'center',
    backgroundColor: palette.ink,
    borderRadius: 18,
    marginTop: 8,
    paddingVertical: 16,
  },
  signOutText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '700',
  },
  title: {
    color: palette.ink,
    fontSize: 30,
    fontWeight: '800',
    marginBottom: 16,
  },
  value: {
    color: palette.ink,
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 6,
  },
});
