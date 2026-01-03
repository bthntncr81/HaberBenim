import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';

export interface RenderJobDto {
  id: string;
  platform: string;
  format: string;
  templateName: string;
  status: string;
  outputType: 'Image' | 'Video'; // Sprint 17: distinguish image/video renders
  outputUrl: string | null;
  progress: number; // 0-100 for video renders
  error: string | null;
  resolvedTextSpecJson: string | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
}

export interface ReadyQueueItemDto {
  id: string;
  title: string;
  summary: string | null;
  sourceName: string;
  category: string | null;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  primaryImageUrl: string | null;
  renderJobs: RenderJobDto[];
}

export interface ReadyQueueListResponse {
  items: ReadyQueueItemDto[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface QueueSettingsDto {
  publishMode: string;
  modes: string[];
  description: string;
}

export interface CreateRenderJobsRequest {
  platforms: string[];
  force?: boolean;
}

export interface CreateRenderJobsResponse {
  success: boolean;
  createdJobs: RenderJobDto[];
  message: string;
}

export interface PublishResponse {
  success: boolean;
  message: string;
  jobId?: string;
  alreadyQueued?: boolean;
  renderJobCount?: number;
  platforms?: string[];
}

@Injectable({
  providedIn: 'root'
})
export class ReadyQueueApiService {
  private http = inject(HttpClient);
  private baseUrl = `${API_CONFIG.baseUrl}/api/v1/ready-queue`;

  // Get ready queue list
  getList(params?: {
    platform?: string;
    page?: number;
    pageSize?: number;
  }): Observable<ReadyQueueListResponse> {
    const queryParams = new URLSearchParams();
    if (params?.platform) queryParams.set('platform', params.platform);
    if (params?.page) queryParams.set('page', String(params.page));
    if (params?.pageSize) queryParams.set('pageSize', String(params.pageSize));

    const url = queryParams.toString() ? `${this.baseUrl}?${queryParams}` : this.baseUrl;
    return this.http.get<ReadyQueueListResponse>(url);
  }

  // Get single item
  getItem(id: string): Observable<ReadyQueueItemDto> {
    return this.http.get<ReadyQueueItemDto>(`${this.baseUrl}/${id}`);
  }

  // Create render jobs
  createRenderJobs(contentId: string, request: CreateRenderJobsRequest): Observable<CreateRenderJobsResponse> {
    return this.http.post<CreateRenderJobsResponse>(`${this.baseUrl}/${contentId}/render`, request);
  }

  // Publish from queue
  publish(contentId: string, options?: { priority?: number; scheduledAtUtc?: string }): Observable<PublishResponse> {
    return this.http.post<PublishResponse>(`${this.baseUrl}/${contentId}/publish`, options || {});
  }

  // Get queue settings
  getSettings(): Observable<QueueSettingsDto> {
    return this.http.get<QueueSettingsDto>(`${this.baseUrl}/settings`);
  }

  // Update queue settings
  updateSettings(publishMode: string): Observable<{ success: boolean; publishMode: string }> {
    return this.http.put<{ success: boolean; publishMode: string }>(`${this.baseUrl}/settings`, { publishMode });
  }
}

