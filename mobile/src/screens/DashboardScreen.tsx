import * as React from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';

import { DashboardSnapshot, FamilyActivityGateway } from '@/features/familyActivity/createFamilyActivityGateway';
import { AuthSession } from '@/features/auth/types';
import { AppStackParamList } from '@/navigation/types';
import { palette } from '@/ui/theme';

type DashboardScreenProps = NativeStackScreenProps<AppStackParamList, 'Dashboard'> & {
  apiBaseUrl: string;
  gateway?: FamilyActivityGateway;
  pollIntervalMs?: number;
  session: AuthSession;
};

export function DashboardScreen({
  navigation,
  apiBaseUrl,
  gateway,
  pollIntervalMs = 30000,
  session,
}: DashboardScreenProps) {
  const [snapshot, setSnapshot] = React.useState<DashboardSnapshot | null>(null);
  const [error, setError] = React.useState<string | null>(null);

  React.useEffect(() => {
    const currentGateway = gateway;
    if (!currentGateway) {
      return undefined;
    }
    const activeGateway: FamilyActivityGateway = currentGateway;

    let active = true;

    async function load() {
      try {
        const nextSnapshot = await activeGateway.getDashboard();
        if (!active) {
          return;
        }

        setSnapshot(nextSnapshot);
        setError(null);
      } catch (loadError) {
        if (!active) {
          return;
        }

        setError(loadError instanceof Error ? loadError.message : 'Unable to load the dashboard.');
      }
    }

    void load();
    const timer = setInterval(() => {
      void load();
    }, pollIntervalMs);

    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [gateway, pollIntervalMs]);

  const metrics = [
    {
      label: 'Today events',
      value: String(snapshot?.todayEventCount ?? 0),
    },
    {
      label: 'Flagged language',
      value: String(snapshot?.foulLanguageEventCount ?? 0),
    },
    {
      label: 'Devices online',
      value: String(snapshot?.devices.filter((device) => device.status === 'online').length ?? 0),
    },
  ];

  return (
    <ScrollView
      contentContainerStyle={styles.content}
      style={styles.page}
    >
      <View style={styles.hero}>
        <Text style={styles.eyebrow}>Parent dashboard</Text>
        <Text style={styles.title}>Family activity</Text>
        <Text style={styles.copy}>
          Signed in as {session.displayName}. Live activity refreshes every 30 seconds so the parent
          view stays close to real time.
        </Text>
      </View>

      <View style={styles.card}>
        <Text style={styles.sectionTitle}>Live status</Text>
        {snapshot?.currentApp ? (
          <View style={styles.livePanel}>
            <Text style={styles.liveLabel}>Active right now</Text>
            <Text style={styles.liveValue}>{snapshot.currentApp.name}</Text>
            <Text style={styles.liveMeta}>
              {snapshot.currentApp.deviceName} started at {formatTime(snapshot.currentApp.startedAt)}
            </Text>
          </View>
        ) : (
          <Text style={styles.helper}>
            {error ?? 'Waiting for a child device to report activity.'}
          </Text>
        )}
        <View style={styles.grid}>
          {metrics.map((card) => (
            <View
              key={card.label}
              style={styles.metric}
            >
              <Text style={styles.metricValue}>{card.value}</Text>
              <Text style={styles.metricLabel}>{card.label}</Text>
            </View>
          ))}
        </View>
      </View>

      <View style={styles.card}>
        <Text style={styles.sectionTitle}>Latest alert</Text>
        {snapshot?.lastEvent ? (
          <View style={styles.alertCard}>
            <Text style={styles.alertTitle}>{snapshot.lastEvent.summary}</Text>
            <Text style={styles.alertMeta}>
              {snapshot.lastEvent.childDeviceName} at {formatTime(snapshot.lastEvent.occurredAt)}
            </Text>
            <Text style={styles.helper}>{snapshot.lastEvent.detail}</Text>
          </View>
        ) : (
          <Text style={styles.helper}>No alert events have reached the parent feed yet.</Text>
        )}
      </View>

      <View style={styles.card}>
        <Text style={styles.sectionTitle}>Quick routes</Text>
        <View style={styles.actions}>
          <RouteButton
            label="Notifications"
            onPress={() => navigation.navigate('Notifications')}
          />
          <RouteButton
            label="Reports"
            onPress={() => navigation.navigate('Reports')}
          />
          <RouteButton
            label="Settings"
            onPress={() => navigation.navigate('Settings')}
          />
        </View>
      </View>

      <View style={styles.card}>
        <Text style={styles.sectionTitle}>Recent activity</Text>
        {snapshot && snapshot.recentEvents.length > 0 ? (
          snapshot.recentEvents.slice(0, 3).map((event) => (
            <View
              key={event.id}
              style={styles.activityRow}
            >
              <Text style={styles.activityTitle}>{event.summary}</Text>
              <Text style={styles.activityMeta}>
                {event.childDeviceName} at {formatTime(event.occurredAt)}
              </Text>
            </View>
          ))
        ) : (
          <Text style={styles.helper}>Recent activity will appear here once the cloud feed is live.</Text>
        )}
      </View>

      <View style={styles.card}>
        <Text style={styles.sectionTitle}>Cloud endpoint</Text>
        <Text style={styles.endpoint}>{apiBaseUrl}</Text>
        <Text style={styles.helper}>
          Override it with `EXPO_PUBLIC_API_BASE_URL` for local emulator or staging targets.
        </Text>
      </View>
    </ScrollView>
  );
}

function RouteButton({
  label,
  onPress,
}: {
  label: string;
  onPress(): void;
}) {
  return (
    <Pressable
      accessibilityRole="button"
      onPress={onPress}
      style={styles.routeButton}
    >
      <Text style={styles.routeButtonText}>{label}</Text>
    </Pressable>
  );
}

function formatTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat('en-US', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(date);
}

const styles = StyleSheet.create({
  actions: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 12,
  },
  activityMeta: {
    color: palette.muted,
    fontSize: 13,
    marginTop: 4,
  },
  activityRow: {
    borderTopColor: palette.line,
    borderTopWidth: 1,
    paddingTop: 14,
  },
  activityTitle: {
    color: palette.ink,
    fontSize: 16,
    fontWeight: '600',
  },
  alertCard: {
    backgroundColor: palette.spotlightSoft,
    borderRadius: 22,
    padding: 18,
  },
  alertMeta: {
    color: palette.muted,
    fontSize: 13,
    marginBottom: 8,
  },
  alertTitle: {
    color: palette.ink,
    fontSize: 18,
    fontWeight: '700',
    marginBottom: 6,
  },
  card: {
    backgroundColor: palette.card,
    borderColor: palette.line,
    borderRadius: 28,
    borderWidth: 1,
    marginBottom: 16,
    padding: 22,
  },
  content: {
    padding: 20,
  },
  copy: {
    color: palette.card,
    fontSize: 16,
    lineHeight: 24,
  },
  endpoint: {
    color: palette.ink,
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 10,
  },
  eyebrow: {
    color: palette.spotlightSoft,
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 1.2,
    marginBottom: 12,
    textTransform: 'uppercase',
  },
  grid: {
    gap: 12,
  },
  helper: {
    color: palette.muted,
    fontSize: 14,
    lineHeight: 20,
  },
  hero: {
    backgroundColor: palette.ink,
    borderRadius: 32,
    marginBottom: 16,
    padding: 24,
  },
  liveLabel: {
    color: palette.accent,
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 1,
    marginBottom: 8,
    textTransform: 'uppercase',
  },
  liveMeta: {
    color: palette.muted,
    fontSize: 14,
    lineHeight: 20,
  },
  livePanel: {
    backgroundColor: palette.canvas,
    borderRadius: 22,
    marginBottom: 14,
    padding: 18,
  },
  liveValue: {
    color: palette.ink,
    fontSize: 28,
    fontWeight: '800',
    marginBottom: 6,
  },
  metric: {
    backgroundColor: palette.canvas,
    borderRadius: 18,
    padding: 18,
  },
  metricLabel: {
    color: palette.muted,
    fontSize: 13,
    marginTop: 6,
  },
  metricValue: {
    color: palette.ink,
    fontSize: 18,
    fontWeight: '700',
  },
  page: {
    backgroundColor: palette.canvas,
    flex: 1,
  },
  routeButton: {
    backgroundColor: palette.accentSoft,
    borderRadius: 16,
    minWidth: 140,
    paddingHorizontal: 16,
    paddingVertical: 14,
  },
  routeButtonText: {
    color: palette.accent,
    fontSize: 15,
    fontWeight: '700',
  },
  sectionTitle: {
    color: palette.ink,
    fontSize: 18,
    fontWeight: '700',
    marginBottom: 14,
  },
  title: {
    color: palette.card,
    fontSize: 34,
    fontWeight: '800',
    marginBottom: 12,
  },
});
