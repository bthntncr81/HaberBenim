// Instagram OAuth flow models

export interface InstagramConnectResponse {
  authorizeUrl: string;
  state: string;
}

export interface InstagramExchangeRequest {
  code: string;
  state: string;
  redirectUri: string;
}

export interface InstagramPageInfo {
  pageId: string;
  pageName: string;
  pageAccessToken: string;
  igUserId: string | null;
  igUsername: string | null;
  hasInstagram: boolean;
}

export interface InstagramExchangeResponse {
  facebookUserId: string;
  pages: InstagramPageInfo[];
}

export interface InstagramCompleteRequest {
  name: string;
  pageId: string;
  state: string;
}

export interface InstagramCompleteResponse {
  success: boolean;
  message: string | null;
  connectionId: string | null;
}

export interface InstagramConnectionDto {
  id: string;
  name: string;
  pageId: string;
  pageName: string;
  igUserId: string;
  igUsername: string | null;
  scopesCsv: string;
  tokenExpiresAtUtc: string;
  isDefaultPublisher: boolean;
  isActive: boolean;
  createdAtUtc: string;
}

export interface InstagramConnectionListResponse {
  connections: InstagramConnectionDto[];
  count: number;
}

export interface InstagramConfigStatus {
  hasAppId: boolean;
  hasRedirectUri: boolean;
  hasPublicAssetUrl: boolean;
  publicAssetUrlConfigured: boolean;
  connectionCount: number;
  activeConnectionCount: number;
  defaultConnection: {
    id: string;
    name: string;
    username: string | null;
    expiresAt: string;
    isExpired: boolean;
  } | null;
  warnings: string[];
}

