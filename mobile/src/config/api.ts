export const fallbackApiBaseUrl = 'https://kidmonitor-api-staging.onrender.com';

export function resolveApiBaseUrl(): string {
  const configuredBaseUrl = process.env.EXPO_PUBLIC_API_BASE_URL?.trim();

  return configuredBaseUrl && configuredBaseUrl.length > 0
    ? configuredBaseUrl
    : fallbackApiBaseUrl;
}
