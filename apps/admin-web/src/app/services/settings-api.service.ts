import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';

export interface SystemSetting {
  key: string;
  value: string;
}

export interface SettingsUpdateRequest {
  settings: SystemSetting[];
}

export interface SettingsUpdateResponse {
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class SettingsApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  /**
   * Get settings by prefix
   */
  getByPrefix(prefix: string): Observable<SystemSetting[]> {
    return this.http.get<SystemSetting[]>(`${this.baseUrl}/api/v1/settings?prefix=${encodeURIComponent(prefix)}`);
  }

  /**
   * Update multiple settings at once
   */
  updateBatch(settings: SystemSetting[]): Observable<SettingsUpdateResponse> {
    return this.http.put<SettingsUpdateResponse>(`${this.baseUrl}/api/v1/settings/batch`, { settings });
  }

  /**
   * Get a single setting value
   */
  get(key: string): Observable<SystemSetting | null> {
    return this.http.get<SystemSetting | null>(`${this.baseUrl}/api/v1/settings/${encodeURIComponent(key)}`);
  }
}

