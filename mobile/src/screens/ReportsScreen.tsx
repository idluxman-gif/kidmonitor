import * as React from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';

import { DailyReportSnapshot, FamilyActivityGateway, WeeklyReportSnapshot } from '@/features/familyActivity/createFamilyActivityGateway';
import { AppStackParamList } from '@/navigation/types';
import { palette } from '@/ui/theme';

type ReportsScreenProps = NativeStackScreenProps<AppStackParamList, 'Reports'> & {
  gateway?: FamilyActivityGateway;
};

export function ReportsScreen({ gateway }: Partial<ReportsScreenProps>) {
  const [dailyReport, setDailyReport] = React.useState<DailyReportSnapshot | null>(null);
  const [weeklyReport, setWeeklyReport] = React.useState<WeeklyReportSnapshot | null>(null);
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
        const today = new Date().toISOString().slice(0, 10);
        const [nextDailyReport, nextWeeklyReport] = await Promise.all([
          activeGateway.getDailyReport(today),
          activeGateway.getWeeklyReport(),
        ]);

        if (!active) {
          return;
        }

        setDailyReport(nextDailyReport);
        setWeeklyReport(nextWeeklyReport);
        setError(null);
      } catch (loadError) {
        if (!active) {
          return;
        }

        setError(loadError instanceof Error ? loadError.message : 'Unable to load reports.');
      }
    }

    void load();

    return () => {
      active = false;
    };
  }, [gateway]);

  return (
    <ScrollView
      contentContainerStyle={styles.content}
      style={styles.page}
    >
      <Text style={styles.title}>Reports</Text>
      <Text style={styles.copy}>
        Daily and weekly rollups turn raw monitoring events into a quick parent readout.
      </Text>
      {error ? <Text style={styles.error}>{error}</Text> : null}

      <View style={styles.card}>
        <Text style={styles.label}>Today</Text>
        {dailyReport ? (
          <>
            <Text style={styles.metric}>{formatDuration(dailyReport.totalScreenTimeMinutes)}</Text>
            <Text style={styles.summary}>
              {dailyReport.foulLanguageEventCount} flagged language events today.
            </Text>
            <Text style={styles.sectionTitle}>Top apps</Text>
            {dailyReport.topApps.map((app) => (
              <View
                key={app.name}
                style={styles.listRow}
              >
                <Text style={styles.listLabel}>{app.name}</Text>
                <Text style={styles.listValue}>{formatDuration(app.minutes)}</Text>
              </View>
            ))}
          </>
        ) : (
          <Text style={styles.summary}>Waiting for the first daily report from the cloud.</Text>
        )}
      </View>

      <View style={styles.card}>
        <Text style={styles.label}>{weeklyReport?.weekLabel ?? 'This week'}</Text>
        {weeklyReport ? (
          <>
            <Text style={styles.metric}>{formatDuration(weeklyReport.totalScreenTimeMinutes)}</Text>
            <Text style={styles.summary}>
              {weeklyReport.foulLanguageEventCount} flagged language events this week.
            </Text>
            <Text style={styles.sectionTitle}>Daily pattern</Text>
            <View style={styles.chart}>
              {weeklyReport.dailyTotals.map((day) => (
                <View
                  key={day.label}
                  style={styles.barGroup}
                >
                  <View style={styles.barTrack}>
                    <View
                      style={[
                        styles.barFill,
                        { height: `${resolveBarHeight(day.minutes, weeklyReport.dailyTotals)}%` as `${number}%` },
                      ]}
                    />
                  </View>
                  <Text style={styles.barLabel}>{day.label}</Text>
                </View>
              ))}
            </View>
            <Text style={styles.sectionTitle}>Top apps</Text>
            {weeklyReport.topApps.map((app) => (
              <View
                key={app.name}
                style={styles.listRow}
              >
                <Text style={styles.listLabel}>{app.name}</Text>
                <Text style={styles.listValue}>{formatDuration(app.minutes)}</Text>
              </View>
            ))}
          </>
        ) : (
          <Text style={styles.summary}>Weekly rollups appear after enough cloud data accumulates.</Text>
        )}
      </View>
    </ScrollView>
  );
}

function formatDuration(minutes: number): string {
  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;

  if (hours === 0) {
    return `${remainingMinutes}m`;
  }

  return `${hours}h ${remainingMinutes}m`;
}

function resolveBarHeight(minutes: number, days: WeeklyReportSnapshot['dailyTotals']): number {
  const maxMinutes = days.reduce((largest, day) => Math.max(largest, day.minutes), 0);
  if (maxMinutes === 0) {
    return 0;
  }

  return Math.max(12, Math.round((minutes / maxMinutes) * 100));
}

const styles = StyleSheet.create({
  barFill: {
    backgroundColor: palette.accent,
    borderRadius: 999,
    width: '100%',
  },
  barGroup: {
    alignItems: 'center',
    flex: 1,
    gap: 8,
  },
  barLabel: {
    color: palette.muted,
    fontSize: 12,
    fontWeight: '600',
  },
  barTrack: {
    alignItems: 'flex-end',
    backgroundColor: palette.accentSoft,
    borderRadius: 999,
    height: 120,
    justifyContent: 'flex-end',
    overflow: 'hidden',
    padding: 6,
    width: 26,
  },
  card: {
    backgroundColor: palette.card,
    borderColor: palette.line,
    borderRadius: 24,
    borderWidth: 1,
    marginBottom: 14,
    padding: 20,
  },
  chart: {
    flexDirection: 'row',
    gap: 12,
    marginBottom: 18,
  },
  content: {
    padding: 20,
  },
  copy: {
    color: palette.muted,
    fontSize: 15,
    lineHeight: 22,
    marginBottom: 18,
  },
  error: {
    color: palette.danger,
    fontSize: 14,
    marginBottom: 12,
  },
  label: {
    color: palette.accent,
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 1,
    marginBottom: 10,
    textTransform: 'uppercase',
  },
  listLabel: {
    color: palette.ink,
    fontSize: 15,
    fontWeight: '600',
  },
  listRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 10,
  },
  listValue: {
    color: palette.muted,
    fontSize: 14,
  },
  metric: {
    color: palette.ink,
    fontSize: 32,
    fontWeight: '800',
    marginBottom: 8,
  },
  page: {
    backgroundColor: palette.canvas,
    flex: 1,
  },
  sectionTitle: {
    color: palette.ink,
    fontSize: 16,
    fontWeight: '700',
    marginBottom: 12,
    marginTop: 10,
  },
  summary: {
    color: palette.ink,
    fontSize: 16,
    lineHeight: 23,
  },
  title: {
    color: palette.ink,
    fontSize: 30,
    fontWeight: '800',
    marginBottom: 10,
  },
});
