import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import {
  ConnectionListResponse,
  ConnectResponse,
  TestConnectionResponse,
  SystemSettingDto
} from '../shared/x-integration.models';

@Injectable({
  providedIn: 'root'
})
export class XIntegrationApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  /**
   * Start OAuth2 connect flow
   */
  connect(scopes?: string): Observable<ConnectResponse> {
    const params = scopes ? `?scopes=${encodeURIComponent(scopes)}` : '';
    return this.http.get<ConnectResponse>(
      `${this.baseUrl}/api/v1/integrations/x/connect${params}`
    );
  }

  /**
   * Get all X connections status
   */
  getStatus(): Observable<ConnectionListResponse> {
    return this.http.get<ConnectionListResponse>(
      `${this.baseUrl}/api/v1/integrations/x/status`
    );
  }

  /**
   * Test default connection health
   */
  testConnection(): Observable<TestConnectionResponse> {
    return this.http.get<TestConnectionResponse>(
      `${this.baseUrl}/api/v1/integrations/x/test`
    );
  }

  /**
   * Set connection as default publisher
   */
  setDefault(connectionId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/api/v1/integrations/x/set-default/${connectionId}`,
      {}
    );
  }

  /**
   * Disconnect (deactivate) a connection
   */
  disconnect(connectionId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/api/v1/integrations/x/disconnect/${connectionId}`,
      {}
    );
  }

  /**
   * Get X-related system settings
   */
  getSettings(): Observable<SystemSettingDto[]> {
    return this.http.get<SystemSettingDto[]>(
      `${this.baseUrl}/api/v1/settings?prefix=X_`
    );
  }

  /**
   * Update system settings
   */
  updateSettings(settings: SystemSettingDto[]): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(
      `${this.baseUrl}/api/v1/settings/batch`,
      { settings }
    );
  }

  /**
   * Test OAuth 1.0a credentials
   */
  testOAuth1(): Observable<OAuth1TestResult> {
    return this.http.get<OAuth1TestResult>(
      `${this.baseUrl}/api/v1/integrations/x/test-oauth1`
    );
  }

  /**
   * Test Bearer Token
   */
  testBearer(query?: string): Observable<BearerTestResult> {
    const params = query ? `?query=${encodeURIComponent(query)}` : '';
    return this.http.get<BearerTestResult>(
      `${this.baseUrl}/api/v1/integrations/x/test-bearer${params}`
    );
  }

  /**
   * Get configuration status
   */
  getConfigStatus(): Observable<ConfigStatusResult> {
    return this.http.get<ConfigStatusResult>(
      `${this.baseUrl}/api/v1/integrations/x/config-status`
    );
  }

  /**
   * Post a test tweet
   */
  testPost(text: string): Observable<TestPostResult> {
    return this.http.post<TestPostResult>(
      `${this.baseUrl}/api/v1/integrations/x/test-post`,
      { text }
    );
  }

  /**
   * Get ingestion status for all X sources
   */
  getIngestionStatus(): Observable<IngestionStatusResult> {
    return this.http.get<IngestionStatusResult>(
      `${this.baseUrl}/api/v1/integrations/x/ingestion-status`
    );
  }

  /**
   * Trigger manual ingestion for all X sources
   */
  triggerIngestion(): Observable<TriggerIngestionResult> {
    return this.http.post<TriggerIngestionResult>(
      `${this.baseUrl}/api/v1/integrations/x/trigger-ingestion`,
      {}
    );
  }

  /**
   * Trigger manual ingestion for a specific source
   */
  triggerSourceIngestion(sourceId: string): Observable<TriggerSourceIngestionResult> {
    return this.http.post<TriggerSourceIngestionResult>(
      `${this.baseUrl}/api/v1/integrations/x/trigger-ingestion/${sourceId}`,
      {}
    );
  }
}

// Response types for new endpoints
export interface OAuth1TestResult {
  success: boolean;
  configured: boolean;
  message: string;
  user?: {
    id: string;
    name: string;
    username: string;
  };
  error?: string;
}

export interface BearerTestResult {
  success: boolean;
  configured: boolean;
  message: string;
  resultCount?: number;
  tweets?: {
    id: string;
    text: string;
    createdAt: string;
  }[];
  error?: string;
  resetAt?: string;
}

export interface ConfigStatusResult {
  oauth1: {
    configured: boolean;
    hasApiKey: boolean;
    hasApiSecretKey: boolean;
    hasAccessToken: boolean;
    hasAccessTokenSecret: boolean;
    description: string;
  };
  oauth2: {
    configured: boolean;
    hasBearerToken: boolean;
    description: string;
  };
  recommendations: string[];
}

export interface TestPostResult {
  success: boolean;
  message: string;
  tweet?: {
    id: string;
    text: string;
    url: string;
  };
  error?: string;
  details?: string;
}

export interface IngestionSourceStatus {
  sourceId: string;
  sourceName: string;
  identifier: string;
  isActive: boolean;
  xUserId: string | null;
  lastSinceId: string | null;
  lastPolledAt: string | null;
  lastSuccessAt: string | null;
  lastFailureAt: string | null;
  lastError: string | null;
  consecutiveFailures: number;
  contentItemCount: number;
}

export interface IngestionStatusResult {
  hasCredentials: boolean;
  credentialType: 'BearerToken' | 'OAuth1' | 'None';
  totalXSources: number;
  totalContentItems: number;
  xContentItems: number;
  sources: IngestionSourceStatus[];
}

export interface TriggerIngestionResult {
  success: boolean;
  message: string;
  sourcesPolled: number;
  totalXContentItems: number;
  error?: string;
}

export interface TriggerSourceIngestionResult {
  success: boolean;
  message: string;
  sourceId: string;
  sourceName: string;
  xUserId: string | null;
  lastSinceId: string | null;
  lastSuccessAt: string | null;
  contentItemCount: number;
  error?: string;
}

