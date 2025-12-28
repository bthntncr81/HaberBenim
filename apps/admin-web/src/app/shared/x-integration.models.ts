// X Integration Models (Sprint 9)

export interface XConnectionStatus {
  id: string;
  name: string;
  xUsername: string;
  xUserId: string;
  scopes: string;
  expiresAtUtc: string;
  isDefaultPublisher: boolean;
  isActive: boolean;
  createdAtUtc: string;
}

export interface ConnectionListResponse {
  connections: XConnectionStatus[];
  count: number;
}

export interface ConnectResponse {
  authorizeUrl: string;
  state: string;
}

export interface TestConnectionResponse {
  connected: boolean;
  username?: string;
  userId?: string;
  tokenExpiresAt?: string;
  message: string;
  error?: string;
}

export interface XSettings {
  clientId: string;
  clientSecret: string;
  redirectUri: string;
  appBearerToken: string;
}

export interface SystemSettingDto {
  key: string;
  value: string;
}

export interface XSourceStateDto {
  id: string;
  sourceId: string;
  xUserId: string | null;
  lastSinceId: string | null;
  lastPolledAtUtc: string | null;
  lastSuccessAtUtc: string | null;
  lastFailureAtUtc: string | null;
  lastError: string | null;
  consecutiveFailures: number;
}

export interface SourceWithXState {
  id: string;
  name: string;
  type: string;
  url: string | null;
  isActive: boolean;
  xState?: XSourceStateDto;
}

