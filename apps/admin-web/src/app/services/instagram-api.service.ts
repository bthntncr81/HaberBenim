import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import {
  InstagramConnectResponse,
  InstagramExchangeResponse,
  InstagramCompleteRequest,
  InstagramCompleteResponse,
  InstagramConnectionListResponse,
  InstagramConfigStatus
} from '../shared/instagram.models';

@Injectable({
  providedIn: 'root'
})
export class InstagramApiService {
  private http = inject(HttpClient);
  private baseUrl = `${API_CONFIG.baseUrl}/api/v1/integrations/instagram`;

  /**
   * Start OAuth flow - get authorization URL
   */
  connect(): Observable<InstagramConnectResponse> {
    return this.http.get<InstagramConnectResponse>(`${this.baseUrl}/connect`);
  }

  /**
   * Exchange OAuth code for pages list
   */
  exchange(code: string, state: string, redirectUri: string): Observable<InstagramExchangeResponse> {
    return this.http.post<InstagramExchangeResponse>(`${this.baseUrl}/exchange`, {
      code,
      state,
      redirectUri
    });
  }

  /**
   * Complete connection by selecting a page
   */
  complete(name: string, pageId: string, state: string): Observable<InstagramCompleteResponse> {
    return this.http.post<InstagramCompleteResponse>(`${this.baseUrl}/complete`, {
      name,
      pageId,
      state
    });
  }

  /**
   * Get all Instagram connections
   */
  status(): Observable<InstagramConnectionListResponse> {
    return this.http.get<InstagramConnectionListResponse>(`${this.baseUrl}/status`);
  }

  /**
   * Get configuration status
   */
  configStatus(): Observable<InstagramConfigStatus> {
    return this.http.get<InstagramConfigStatus>(`${this.baseUrl}/config-status`);
  }

  /**
   * Set a connection as default publisher
   */
  setDefault(connectionId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/set-default/${connectionId}`,
      {}
    );
  }

  /**
   * Disconnect (deactivate) a connection
   */
  disconnect(connectionId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/disconnect/${connectionId}`,
      {}
    );
  }

  /**
   * Manually add Instagram connection with token from Developer Console
   */
  addManual(
    name: string,
    igUserId: string,
    igUsername: string | null,
    accessToken: string,
    expiresInDays?: number
  ): Observable<InstagramCompleteResponse> {
    return this.http.post<InstagramCompleteResponse>(`${this.baseUrl}/add-manual`, {
      name,
      igUserId,
      igUsername,
      accessToken,
      expiresInDays: expiresInDays || 60
    });
  }
}

