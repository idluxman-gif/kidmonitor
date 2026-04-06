export type PushPlatform = 'fcm' | 'apns';

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  parentId: string;
  email: string;
  displayName: string;
}

export interface LoginInput {
  email: string;
  password: string;
}

export interface RegisterInput extends LoginInput {
  displayName: string;
}

export interface PushTokenRegistration {
  platform: PushPlatform;
  token: string;
}
