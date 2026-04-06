import * as React from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';

import { FamilyActivityEvent, FamilyActivityGateway } from '@/features/familyActivity/createFamilyActivityGateway';
import { AppStackParamList } from '@/navigation/types';
import { palette } from '@/ui/theme';

type NotificationsScreenProps = NativeStackScreenProps<AppStackParamList, 'Notifications'> & {
  gateway?: FamilyActivityGateway;
};

export function NotificationsScreen({ gateway }: Partial<NotificationsScreenProps>) {
  const [items, setItems] = React.useState<FamilyActivityEvent[]>([]);
  const [expandedEventId, setExpandedEventId] = React.useState<string | null>(null);
  const [error, setError] = React.useState<string | null>(null);
  const [nextPage, setNextPage] = React.useState<number | null>(null);

  React.useEffect(() => {
    const currentGateway = gateway;
    if (!currentGateway) {
      return undefined;
    }
    const activeGateway: FamilyActivityGateway = currentGateway;

    let active = true;

    async function loadPage(page: number, append: boolean) {
      try {
        const response = await activeGateway.getNotifications({ page });
        if (!active) {
          return;
        }

        setItems((currentItems) => (append ? [...currentItems, ...response.items] : response.items));
        setNextPage(response.nextPage);
        setError(null);
      } catch (loadError) {
        if (!active) {
          return;
        }

        setError(loadError instanceof Error ? loadError.message : 'Unable to load notifications.');
      }
    }

    void loadPage(1, false);

    return () => {
      active = false;
    };
  }, [gateway]);

  async function loadOlderEvents() {
    const currentGateway = gateway;
    if (!currentGateway || nextPage === null) {
      return;
    }
    const activeGateway: FamilyActivityGateway = currentGateway;

    try {
      const response = await activeGateway.getNotifications({ page: nextPage });
      setItems((currentItems) => [...currentItems, ...response.items]);
      setNextPage(response.nextPage);
      setError(null);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : 'Unable to load older notifications.');
    }
  }

  return (
    <ScrollView
      contentContainerStyle={styles.content}
      style={styles.page}
    >
      <Text style={styles.title}>Notifications</Text>
      <Text style={styles.copy}>
        Review foul-language alerts, daily summaries, and child-device activity as they reach the
        parent account.
      </Text>

      {error ? <Text style={styles.error}>{error}</Text> : null}

      {items.length === 0 && !error ? (
        <View style={styles.emptyCard}>
          <Text style={styles.cardTitle}>No notifications yet</Text>
          <Text style={styles.cardCopy}>
            The history feed will populate after the first cloud event arrives.
          </Text>
        </View>
      ) : null}

      {items.map((item) => {
        const isExpanded = expandedEventId === item.id;

        return (
          <Pressable
            key={item.id}
            accessibilityRole="button"
            onPress={() => setExpandedEventId(isExpanded ? null : item.id)}
            style={styles.card}
          >
            <Text style={styles.icon}>{iconForEvent(item.icon)}</Text>
            <Text style={styles.cardTitle}>{item.summary}</Text>
            <Text style={styles.cardMeta}>
              {item.childDeviceName} at {formatTimestamp(item.occurredAt)}
            </Text>
            {isExpanded ? <Text style={styles.cardCopy}>{item.detail}</Text> : null}
          </Pressable>
        );
      })}

      {nextPage !== null ? (
        <Pressable
          accessibilityRole="button"
          onPress={() => {
            void loadOlderEvents();
          }}
          style={styles.loadMoreButton}
        >
          <Text style={styles.loadMoreText}>Load older events</Text>
        </Pressable>
      ) : null}
    </ScrollView>
  );
}

function iconForEvent(icon: FamilyActivityEvent['icon']): string {
  switch (icon) {
    case 'alert':
      return '!';
    case 'summary':
      return '#';
    default:
      return '*';
  }
}

function formatTimestamp(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat('en-US', {
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    month: 'short',
  }).format(date);
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: palette.card,
    borderColor: palette.line,
    borderRadius: 24,
    borderWidth: 1,
    marginBottom: 14,
    padding: 18,
  },
  cardCopy: {
    color: palette.ink,
    fontSize: 15,
    lineHeight: 22,
    marginTop: 10,
  },
  cardMeta: {
    color: palette.muted,
    fontSize: 13,
    marginTop: 4,
  },
  cardTitle: {
    color: palette.ink,
    fontSize: 18,
    fontWeight: '700',
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
  emptyCard: {
    backgroundColor: palette.card,
    borderColor: palette.line,
    borderRadius: 24,
    borderWidth: 1,
    marginBottom: 14,
    padding: 20,
  },
  error: {
    color: palette.danger,
    fontSize: 14,
    marginBottom: 12,
  },
  icon: {
    color: palette.spotlight,
    fontSize: 18,
    fontWeight: '700',
    marginBottom: 8,
  },
  loadMoreButton: {
    alignItems: 'center',
    backgroundColor: palette.accentSoft,
    borderRadius: 16,
    marginTop: 4,
    paddingVertical: 14,
  },
  loadMoreText: {
    color: palette.accent,
    fontSize: 15,
    fontWeight: '700',
  },
  page: {
    backgroundColor: palette.canvas,
    flex: 1,
  },
  title: {
    color: palette.ink,
    fontSize: 30,
    fontWeight: '800',
    marginBottom: 10,
  },
});
