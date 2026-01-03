import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';

export interface TimeWindow {
  start: string; // HH:mm
  end: string;   // HH:mm
}

export interface NightModeSettings {
  start: string;
  end: string;
  silencePush: boolean;
  queueForMorning: boolean;
}

export interface PlatformPolicy {
  allowedWindows: TimeWindow[];
  dailyLimit: number;
  minIntervalMinutes: number;
  nightMode: NightModeSettings;
  emergencyOverride: boolean;
  isEnabled: boolean;
}

export interface EmergencyRules {
  keywords: string[];
  emergencyCategories: string[];
  trustedSources: string[];
  minKeywordScore: number;
}

export interface PublishingPolicy {
  platforms: { [key: string]: PlatformPolicy };
  emergencyRules: EmergencyRules;
  timeZoneId: string;
}

export interface DailyPublishingStats {
  platform: string;
  date: string;
  count: number;
  limit: number;
  remaining: number;
  isAtLimit: boolean;
}

export interface SchedulePreview {
  platform: string;
  isEmergency: boolean;
  canPublishNow: boolean;
  scheduledAtUtc: string;
  reason: string | null;
  silencePush: boolean;
  dailyCountSoFar: number;
  dailyLimit: number;
  isNightMode: boolean;
  isWithinWindow: boolean;
  nextSlot: string;
}

export interface EmergencyQueueItem {
  id: string;
  contentItemId: string;
  title: string;
  category: string | null;
  sourceName: string;
  priority: number;
  status: string;
  matchedKeywords: string[];
  detectedAtUtc: string;
  publishedAtUtc: string | null;
}

export interface EmergencyQueueListResponse {
  items: EmergencyQueueItem[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface EmergencyQueueStats {
  pending: number;
  publishedToday: number;
  cancelledToday: number;
  averagePriority: number;
}

export interface DetectionResult {
  contentId: string;
  title: string;
  isEmergency: boolean;
  priority: number;
  matchedKeywords: string[];
  reason: string | null;
  isBreakingNews: boolean;
  categoryMatch: boolean;
  trustedSource: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class PublishingPolicyApiService {
  private http = inject(HttpClient);
  private baseUrl = `${API_CONFIG.baseUrl}/api/v1/publishing/policy`;
  private emergencyUrl = `${API_CONFIG.baseUrl}/api/v1/emergency-queue`;

  // Policy endpoints
  getPolicy(): Observable<PublishingPolicy> {
    return this.http.get<PublishingPolicy>(this.baseUrl);
  }

  updatePolicy(policy: PublishingPolicy): Observable<{ success: boolean; policy: PublishingPolicy }> {
    return this.http.put<{ success: boolean; policy: PublishingPolicy }>(this.baseUrl, policy);
  }

  getPlatformPolicy(platform: string): Observable<PlatformPolicy> {
    return this.http.get<PlatformPolicy>(`${this.baseUrl}/platforms/${platform}`);
  }

  updatePlatformPolicy(platform: string, policy: PlatformPolicy): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.baseUrl}/platforms/${platform}`, policy);
  }

  getStats(): Observable<DailyPublishingStats[]> {
    return this.http.get<DailyPublishingStats[]>(`${this.baseUrl}/stats`);
  }

  getSchedulePreview(platform: string, isEmergency: boolean = false): Observable<SchedulePreview> {
    return this.http.get<SchedulePreview>(
      `${this.baseUrl}/schedule/preview/${platform}?isEmergency=${isEmergency}`
    );
  }

  getEmergencyRules(): Observable<EmergencyRules> {
    return this.http.get<EmergencyRules>(`${this.baseUrl}/emergency-rules`);
  }

  updateEmergencyRules(rules: EmergencyRules): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.baseUrl}/emergency-rules`, rules);
  }

  // Emergency queue endpoints
  getEmergencyQueue(params?: {
    status?: string;
    page?: number;
    pageSize?: number;
  }): Observable<EmergencyQueueListResponse> {
    const queryParams = new URLSearchParams();
    if (params?.status) queryParams.set('status', params.status);
    if (params?.page) queryParams.set('page', String(params.page));
    if (params?.pageSize) queryParams.set('pageSize', String(params.pageSize));

    const url = queryParams.toString() 
      ? `${this.emergencyUrl}?${queryParams}` 
      : this.emergencyUrl;
    return this.http.get<EmergencyQueueListResponse>(url);
  }

  getEmergencyItem(id: string): Observable<EmergencyQueueItem> {
    return this.http.get<EmergencyQueueItem>(`${this.emergencyUrl}/${id}`);
  }

  addToEmergencyQueue(request: {
    contentItemId: string;
    priority?: number;
    reason?: string;
    autoDetect?: boolean;
    platforms?: string[];
  }): Observable<{ success: boolean; item: EmergencyQueueItem }> {
    return this.http.post<{ success: boolean; item: EmergencyQueueItem }>(this.emergencyUrl, request);
  }

  publishEmergency(id: string): Observable<{ success: boolean; publishJobId?: string; message: string }> {
    return this.http.post<{ success: boolean; publishJobId?: string; message: string }>(
      `${this.emergencyUrl}/${id}/publish`, {}
    );
  }

  cancelEmergency(id: string): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.emergencyUrl}/${id}/cancel`, {}
    );
  }

  updateEmergencyPriority(id: string, priority: number): Observable<{ success: boolean; priority: number }> {
    return this.http.put<{ success: boolean; priority: number }>(
      `${this.emergencyUrl}/${id}/priority`, { priority }
    );
  }

  detectEmergency(contentItemId: string): Observable<DetectionResult> {
    return this.http.post<DetectionResult>(`${this.emergencyUrl}/detect`, { contentItemId });
  }

  getEmergencyStats(): Observable<EmergencyQueueStats> {
    return this.http.get<EmergencyQueueStats>(`${this.emergencyUrl}/stats`);
  }
}

