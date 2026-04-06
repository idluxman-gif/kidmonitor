import * as React from 'react';
import { render, screen, waitFor } from '@testing-library/react-native';

import { FamilyActivityGateway } from '@/features/familyActivity/createFamilyActivityGateway';
import { ReportsScreen } from '@/screens/ReportsScreen';

describe('ReportsScreen', () => {
  test('renders daily and weekly report summaries with chart labels', async () => {
    render(
      <ReportsScreen
        gateway={{
          getDailyReport: async () => ({
            date: '2026-04-04',
            totalScreenTimeMinutes: 310,
            foulLanguageEventCount: 3,
            topApps: [
              { name: 'Chrome', minutes: 120 },
              { name: 'Roblox', minutes: 95 },
            ],
          }),
          getWeeklyReport: async () => ({
            weekLabel: 'Mar 29 - Apr 4',
            totalScreenTimeMinutes: 1675,
            foulLanguageEventCount: 8,
            topApps: [
              { name: 'Chrome', minutes: 530 },
              { name: 'Roblox', minutes: 410 },
            ],
            dailyTotals: [
              { label: 'Sun', minutes: 180 },
              { label: 'Mon', minutes: 260 },
              { label: 'Tue', minutes: 210 },
            ],
          }),
        } as unknown as FamilyActivityGateway}
      />,
    );

    await waitFor(() => {
      expect(screen.getByText('Today')).toBeTruthy();
    });

    expect(screen.getByText('5h 10m')).toBeTruthy();
    expect(screen.getByText('Mar 29 - Apr 4')).toBeTruthy();
    expect(screen.getAllByText('Chrome').length).toBeGreaterThan(0);
    expect(screen.getByText('Sun')).toBeTruthy();
  });
});
