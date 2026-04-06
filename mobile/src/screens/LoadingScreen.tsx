import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

import { palette } from '@/ui/theme';

export function LoadingScreen() {
  return (
    <View style={styles.page}>
      <View style={styles.badge}>
        <Text style={styles.badgeText}>Cloud relay</Text>
      </View>
      <Text style={styles.title}>Syncing parent session</Text>
      <Text style={styles.copy}>
        Checking the saved account and refreshing tokens before the app opens.
      </Text>
      <ActivityIndicator
        color={palette.accent}
        size="large"
      />
    </View>
  );
}

const styles = StyleSheet.create({
  badge: {
    backgroundColor: palette.accentSoft,
    borderRadius: 999,
    marginBottom: 16,
    paddingHorizontal: 14,
    paddingVertical: 8,
  },
  badgeText: {
    color: palette.accent,
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 1,
    textTransform: 'uppercase',
  },
  copy: {
    color: palette.muted,
    fontSize: 16,
    lineHeight: 24,
    marginBottom: 24,
    maxWidth: 280,
    textAlign: 'center',
  },
  page: {
    alignItems: 'center',
    backgroundColor: palette.canvas,
    flex: 1,
    justifyContent: 'center',
    padding: 24,
  },
  title: {
    color: palette.ink,
    fontSize: 28,
    fontWeight: '700',
    marginBottom: 12,
    textAlign: 'center',
  },
});
