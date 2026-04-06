import * as React from 'react';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { FamilyActivityGateway } from '@/features/familyActivity/createFamilyActivityGateway';
import { DashboardScreen } from '@/screens/DashboardScreen';
import { AuthSession } from '@/features/auth/types';

const session: AuthSession = {
  accessToken: 'access-token',
  refreshToken: 'refresh-token',
  parentId: 'parent-1',
  email: 'parent@kidmonitor.test',
  displayName: 'Parent Test',
};

describe('DashboardScreen', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  test('loads the live dashboard and refreshes on the polling interval', async () => {
    const getDashboard = jest
      .fn<ReturnType<FamilyActivityGateway['getDashboard']>, Parameters<FamilyActivityGateway['getDashboard']>>()
      .mockResolvedValue({
        currentApp: {
          deviceName: 'QA Test PC',
          name: 'Roblox',
          startedAt: '2026-04-04T14:10:00Z',
        },
        devices: [
          { id: 'device-1', name: 'QA Test PC', status: 'online' },
        ],
        lastEvent: {
          id: 'event-1',
          childDeviceName: 'QA Test PC',
          eventType: 'foul_language_detected',
          occurredAt: '2026-04-04T14:12:00Z',
          summary: 'Flagged WhatsApp message',
          detail: 'Context snippet available in history.',
          icon: 'alert',
        },
        recentEvents: [
          {
            id: 'event-1',
            childDeviceName: 'QA Test PC',
            eventType: 'foul_language_detected',
            occurredAt: '2026-04-04T14:12:00Z',
            summary: 'Flagged WhatsApp message',
            detail: 'Context snippet available in history.',
            icon: 'alert',
          },
        ],
        todayEventCount: 18,
        foulLanguageEventCount: 2,
      });

    render(
      <DashboardScreen
        apiBaseUrl="https://kidmonitor.test"
        gateway={{ getDashboard } as unknown as FamilyActivityGateway}
        navigation={{ navigate: jest.fn() } as never}
        route={{ key: 'Dashboard', name: 'Dashboard' } as never}
        session={session}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText('Roblox')).toBeTruthy();
    });
    expect(screen.getAllByText('Flagged WhatsApp message').length).toBeGreaterThan(0);
    expect(screen.getByText('2')).toBeTruthy();

    await act(async () => {
      jest.advanceTimersByTime(30000);
    });

    await waitFor(() => {
      expect(getDashboard).toHaveBeenCalledTimes(2);
    });
  });

  test('navigates to notifications and reports from quick actions', async () => {
    const navigate = jest.fn();

    render(
      <DashboardScreen
        apiBaseUrl="https://kidmonitor.test"
        gateway={{
          getDashboard: async () => ({
            currentApp: null,
            devices: [],
            lastEvent: null,
            recentEvents: [],
            todayEventCount: 0,
            foulLanguageEventCount: 0,
          }),
        } as unknown as FamilyActivityGateway}
        navigation={{ navigate } as never}
        route={{ key: 'Dashboard', name: 'Dashboard' } as never}
        session={session}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText('Today events')).toBeTruthy();
    });

    fireEvent.press(screen.getByText('Notifications'));
    fireEvent.press(screen.getByText('Reports'));

    expect(navigate).toHaveBeenCalledWith('Notifications');
    expect(navigate).toHaveBeenCalledWith('Reports');
  });
});
