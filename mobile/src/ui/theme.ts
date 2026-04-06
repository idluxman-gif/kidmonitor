import { DefaultTheme, Theme } from '@react-navigation/native';

export const palette = {
  accent: '#0F766E',
  accentSoft: '#D9F3EE',
  canvas: '#F6F0E8',
  card: '#FFFDF8',
  danger: '#B42318',
  ink: '#17212B',
  line: '#D8CDC0',
  muted: '#667085',
  spotlight: '#F97316',
  spotlightSoft: '#FFE8D5',
} as const;

export const navigationTheme: Theme = {
  ...DefaultTheme,
  colors: {
    ...DefaultTheme.colors,
    background: palette.canvas,
    border: palette.line,
    card: palette.canvas,
    notification: palette.spotlight,
    primary: palette.accent,
    text: palette.ink,
  },
};
