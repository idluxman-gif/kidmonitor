import * as React from 'react';
import { Alert } from 'react-native';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import {
  FamilyActivityGateway,
  NotificationPreferences,
  NotificationPreferencesStore,
} from '@/features/familyActivity/createFamilyActivityGateway';
import { SettingsScreen } from '@/screens/SettingsScreen';
import { AuthSession } from '@/features/auth/types';

const session: AuthSession = {
  accessToken: 'access-token',
  refreshToken: 'refresh-token',
  parentId: 'parent-1',
  email: 'parent@kidmonitor.test',
  displayName: 'Parent Test',
};

describe('SettingsScreen', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  test('loads devices, removes a device, and persists notification preferences', async () => {
    const removeDevice = jest.fn().mockResolvedValue(undefined);
    const save = jest.fn<Promise<void>, [NotificationPreferences]>().mockResolvedValue(undefined);

    const preferencesStore: NotificationPreferencesStore = {
      load: async () => ({
        contentAlerts: true,
        dailySummaries: false,
        weeklyReports: true,
      }),
      save,
    };

    render(
      <SettingsScreen
        apiBaseUrl="https://kidmonitor.test"
        gateway={{
          getDevices: async () => ([
            { id: 'device-1', name: 'QA Test PC', status: 'online' },
            { id: 'device-2', name: 'Family Laptop', status: 'offline' },
          ]),
          removeDevice,
        } as unknown as FamilyActivityGateway}
        navigation={{ navigate: jest.fn() } as never}
        onSignOut={jest.fn()}
        preferencesStore={preferencesStore}
        route={{ key: 'Settings', name: 'Settings' } as never}
        session={session}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText('QA Test PC')).toBeTruthy();
    });

    fireEvent.press(screen.getByText('Remove Family Laptop'));
    await waitFor(() => {
      expect(removeDevice).toHaveBeenCalledWith('device-2');
    });

    fireEvent.press(screen.getByLabelText('Toggle daily summaries'));
    await waitFor(() => {
      expect(save).toHaveBeenCalledWith({
        contentAlerts: true,
        dailySummaries: true,
        weeklyReports: true,
      });
    });
  });

  test('claims a device pairing code from settings', async () => {
    const alertSpy = jest.spyOn(Alert, 'alert').mockImplementation(jest.fn());
    const claimPairing = jest.fn().mockResolvedValue({
      id: 'device-3',
      name: 'Study PC',
      status: 'offline',
    });

    render(
      <SettingsScreen
        apiBaseUrl="https://kidmonitor.test"
        gateway={{
          claimPairing,
          getDevices: async () => ([]),
          removeDevice: jest.fn(),
        } as unknown as FamilyActivityGateway}
        navigation={{ navigate: jest.fn() } as never}
        onSignOut={jest.fn()}
        route={{ key: 'Settings', name: 'Settings' } as never}
        session={session}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText('No paired devices yet.')).toBeTruthy();
    });

    fireEvent.press(screen.getByText('Add device'));
    fireEvent.changeText(screen.getByPlaceholderText('123456'), '123456');
    fireEvent.press(screen.getByText('Claim device'));

    await waitFor(() => {
      expect(claimPairing).toHaveBeenCalledWith('123456');
    });

    expect(alertSpy).toHaveBeenCalledWith('Device paired', 'Study PC is now linked to this parent account.');
    expect(screen.getByText('Study PC')).toBeTruthy();
  });
});
