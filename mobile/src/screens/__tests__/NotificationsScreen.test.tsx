import * as React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react-native';

import { FamilyActivityGateway } from '@/features/familyActivity/createFamilyActivityGateway';
import { NotificationsScreen } from '@/screens/NotificationsScreen';

describe('NotificationsScreen', () => {
  test('renders notification history and expands event details on tap', async () => {
    const getNotifications = jest
      .fn<ReturnType<FamilyActivityGateway['getNotifications']>, Parameters<FamilyActivityGateway['getNotifications']>>()
      .mockResolvedValue({
        items: [
          {
            id: 'event-1',
            childDeviceName: 'QA Test PC',
            eventType: 'foul_language_detected',
            occurredAt: '2026-04-04T14:12:00Z',
            summary: 'Flagged Discord voice clip',
            detail: 'Matched term from voice transcript.',
            icon: 'alert',
          },
        ],
        nextPage: 2,
      });

    render(
      <NotificationsScreen
        gateway={{ getNotifications } as unknown as FamilyActivityGateway}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText('Flagged Discord voice clip')).toBeTruthy();
    });

    fireEvent.press(screen.getByText('Flagged Discord voice clip'));

    expect(screen.getByText('Matched term from voice transcript.')).toBeTruthy();
    expect(screen.getByText('Load older events')).toBeTruthy();
  });
});
