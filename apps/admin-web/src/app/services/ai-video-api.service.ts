import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';

export interface AiVideoGenerateRequest {
  force?: boolean;
  mode?: 'AutoPrompt' | 'CustomPrompt';
  promptOverride?: string;
  model?: string;
  seconds?: string;
  size?: string;
}

export interface AiVideoJob {
  id: string;
  contentItemId: string;
  provider: string;
  model: string;
  prompt: string;
  seconds: string;
  size: string;
  status: 'Queued' | 'InProgress' | 'Completed' | 'Failed' | 'Cancelled';
  openAiVideoId?: string;
  progress: number;
  error?: string;
  mediaUrl?: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  completedAtUtc?: string;
}

export interface AiVideoGenerateResponse {
  success: boolean;
  message: string;
  job?: AiVideoJob;
  enabled?: boolean;
}

export interface AiVideoStatusResponse {
  success: boolean;
  hasVideo: boolean;
  message?: string;
  job?: AiVideoJob;
}

export interface AiVideoPromptPreviewResponse {
  success: boolean;
  preview?: {
    prompt: string;
    model: string;
    seconds: string;
    size: string;
  };
}

export interface AiVideoConfigResponse {
  enabled: boolean;
  configured: boolean;
  allowedModels: string[];
  allowedSeconds: string[];
  allowedSizes: string[];
}

@Injectable({
  providedIn: 'root'
})
export class AiVideoApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  /**
   * Generate AI video for content
   */
  generate(contentId: string, request?: AiVideoGenerateRequest): Observable<AiVideoGenerateResponse> {
    return this.http.post<AiVideoGenerateResponse>(
      `${this.baseUrl}/api/v1/editorial/items/${contentId}/ai-video/generate`,
      request || {}
    );
  }

  /**
   * Get AI video job status
   */
  getStatus(contentId: string): Observable<AiVideoStatusResponse> {
    return this.http.get<AiVideoStatusResponse>(
      `${this.baseUrl}/api/v1/editorial/items/${contentId}/ai-video/status`
    );
  }

  /**
   * Get all AI video jobs for content
   */
  getJobs(contentId: string): Observable<{ success: boolean; count: number; jobs: AiVideoJob[] }> {
    return this.http.get<{ success: boolean; count: number; jobs: AiVideoJob[] }>(
      `${this.baseUrl}/api/v1/editorial/items/${contentId}/ai-video/jobs`
    );
  }

  /**
   * Get prompt preview
   */
  getPromptPreview(contentId: string, customPrompt?: string): Observable<AiVideoPromptPreviewResponse> {
    let url = `${this.baseUrl}/api/v1/editorial/items/${contentId}/ai-video/prompt`;
    if (customPrompt) {
      url += `?customPrompt=${encodeURIComponent(customPrompt)}`;
    }
    return this.http.get<AiVideoPromptPreviewResponse>(url);
  }

  /**
   * Cancel in-progress video job
   */
  cancel(contentId: string): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.baseUrl}/api/v1/editorial/items/${contentId}/ai-video/cancel`,
      {}
    );
  }

  /**
   * Get AI video configuration
   */
  getConfig(): Observable<AiVideoConfigResponse> {
    // We'll use a specific content ID endpoint, but config doesn't need one
    // The backend returns config without needing a specific content
    return this.http.get<AiVideoConfigResponse>(
      `${this.baseUrl}/api/v1/editorial/items/00000000-0000-0000-0000-000000000000/ai-video/config`
    );
  }
}

