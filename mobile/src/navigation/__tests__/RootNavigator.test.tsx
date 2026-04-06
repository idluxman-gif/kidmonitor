import { render, screen } from '@testing-library/react-native';

import { RootNavigator } from '@/navigation/RootNavigator';
import { AuthSession } from '@/features/auth/types';

const signedInSession: AuthSession = {
  accessToken: 'access-token',
  refreshToken: 'refresh-token',
  parentId: '58de27f1-1c95-4f2c-8a1c-0bfdf609fe59',
  email: 'parent@kidmonitor.test',
  displayName: 'Parent Test',
};

describe('RootNavigator', () => {
  test('renders the auth flow when the parent is signed out', () => {
    render(
      <RootNavigator
        status="signedOut"
        session={null}
        onSignOut={jest.fn()}
      />,
    );

    expect(screen.getByText('Parent app')).toBeTruthy();
    expect(screen.getByText('Create account')).toBeTruthy();
  });

  test('renders the app flow when the parent is signed in', () => {
    render(
      <RootNavigator
        status="signedIn"
        session={signedInSession}
        onSignOut={jest.fn()}
      />,
    );

    expect(screen.getByText('Family activity')).toBeTruthy();
    expect(screen.getByText('Notifications')).toBeTruthy();
  });
});
