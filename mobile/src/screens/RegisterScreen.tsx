import * as React from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { NativeStackScreenProps } from '@react-navigation/native-stack';
import { SafeAreaView } from 'react-native-safe-area-context';

import { RegisterInput } from '@/features/auth/types';
import { AuthStackParamList } from '@/navigation/types';
import { palette } from '@/ui/theme';

type RegisterScreenProps = NativeStackScreenProps<AuthStackParamList, 'Register'> & {
  error: string | null;
  onClearError(): void;
  onSignUp(input: RegisterInput): Promise<void>;
};

export function RegisterScreen({
  navigation,
  error,
  onClearError,
  onSignUp,
}: RegisterScreenProps) {
  const [displayName, setDisplayName] = React.useState('');
  const [email, setEmail] = React.useState('');
  const [password, setPassword] = React.useState('');
  const [isSubmitting, setIsSubmitting] = React.useState(false);

  const canSubmit =
    displayName.trim().length > 0 &&
    email.trim().length > 0 &&
    password.trim().length > 0 &&
    !isSubmitting;

  async function handleSubmit() {
    if (!canSubmit) {
      return;
    }

    setIsSubmitting(true);

    try {
      await onSignUp({
        displayName: displayName.trim(),
        email: email.trim().toLowerCase(),
        password,
      });
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <SafeAreaView style={styles.page}>
      <KeyboardAvoidingView
        behavior={Platform.select({ ios: 'padding', default: undefined })}
        style={styles.flex}
      >
        <ScrollView
          contentContainerStyle={styles.content}
          keyboardShouldPersistTaps="handled"
        >
          <View style={styles.hero}>
            <Text style={styles.eyebrow}>Family setup</Text>
            <Text style={styles.title}>Create account</Text>
            <Text style={styles.copy}>
              Start with email auth now, then pair devices and consume live activity feeds as the rest of Milestone 5 lands.
            </Text>
          </View>

          <View style={styles.card}>
            {error ? (
              <View style={styles.errorBanner}>
                <Text style={styles.errorText}>{error}</Text>
              </View>
            ) : null}

            <Text style={styles.label}>Display name</Text>
            <TextInput
              onChangeText={setDisplayName}
              placeholder="Parent name"
              placeholderTextColor={palette.muted}
              style={styles.input}
              value={displayName}
            />

            <Text style={styles.label}>Email</Text>
            <TextInput
              autoCapitalize="none"
              autoComplete="email"
              keyboardType="email-address"
              onChangeText={setEmail}
              placeholder="parent@kidmonitor.test"
              placeholderTextColor={palette.muted}
              style={styles.input}
              value={email}
            />

            <Text style={styles.label}>Password</Text>
            <TextInput
              autoComplete="password-new"
              onChangeText={setPassword}
              placeholder="Create a password"
              placeholderTextColor={palette.muted}
              secureTextEntry
              style={styles.input}
              value={password}
            />

            <Pressable
              accessibilityRole="button"
              disabled={!canSubmit}
              onPress={handleSubmit}
              style={[styles.primaryButton, !canSubmit && styles.disabledButton]}
            >
              <Text style={styles.primaryButtonText}>
                {isSubmitting ? 'Creating account...' : 'Create account'}
              </Text>
            </Pressable>

            <Pressable
              accessibilityRole="button"
              onPress={() => {
                onClearError();
                navigation.navigate('Login');
              }}
              style={styles.secondaryButton}
            >
              <Text style={styles.secondaryButtonText}>Sign in</Text>
            </Pressable>
          </View>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: palette.card,
    borderColor: palette.line,
    borderRadius: 28,
    borderWidth: 1,
    padding: 24,
  },
  content: {
    flexGrow: 1,
    justifyContent: 'center',
    padding: 20,
  },
  copy: {
    color: palette.card,
    fontSize: 16,
    lineHeight: 24,
    maxWidth: 300,
  },
  disabledButton: {
    opacity: 0.55,
  },
  errorBanner: {
    backgroundColor: '#FEE4E2',
    borderRadius: 18,
    marginBottom: 18,
    paddingHorizontal: 14,
    paddingVertical: 12,
  },
  errorText: {
    color: palette.danger,
    fontSize: 14,
    lineHeight: 20,
  },
  eyebrow: {
    color: palette.spotlightSoft,
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 1.2,
    marginBottom: 12,
    textTransform: 'uppercase',
  },
  flex: {
    flex: 1,
  },
  hero: {
    backgroundColor: palette.spotlight,
    borderRadius: 32,
    marginBottom: 18,
    padding: 24,
  },
  input: {
    backgroundColor: '#FFFFFF',
    borderColor: palette.line,
    borderRadius: 16,
    borderWidth: 1,
    color: palette.ink,
    fontSize: 16,
    marginBottom: 16,
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
  primaryButton: {
    alignItems: 'center',
    backgroundColor: palette.ink,
    borderRadius: 18,
    marginTop: 8,
    paddingVertical: 16,
  },
  primaryButtonText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '700',
  },
  secondaryButton: {
    alignItems: 'center',
    marginTop: 14,
    paddingVertical: 14,
  },
  secondaryButtonText: {
    color: palette.accent,
    fontSize: 15,
    fontWeight: '700',
  },
  title: {
    color: palette.card,
    fontSize: 34,
    fontWeight: '800',
    marginBottom: 10,
  },
});
