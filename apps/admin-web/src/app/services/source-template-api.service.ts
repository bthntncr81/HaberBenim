import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';

export interface SourceTemplateAssignmentDto {
  id: string;
  sourceId: string;
  sourceName: string;
  platform: string;
  mode: string;
  templateId: string;
  templateName: string;
  templateFormat: string;
  priorityOverride: number | null;
  effectivePriority: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateSourceTemplateAssignmentRequest {
  sourceId: string;
  platform: string;
  templateId: string;
  priorityOverride?: number | null;
  isActive?: boolean;
}

export interface UpdateSourceTemplateAssignmentRequest {
  priorityOverride?: number | null;
  isActive?: boolean;
}

export interface ApplyTemplateRequest {
  platform: string;
}

export interface ResolvedTextSpec {
  instagramCaption?: string;
  xText?: string;
  tiktokHook?: string;
  youtubeTitle?: string;
  youtubeDescription?: string;
}

export interface ApplyTemplateResponse {
  success: boolean;
  selectedTemplateId?: string;
  selectedTemplateName?: string;
  format?: string;
  mediaType?: string;
  skipReason?: string;
  error?: string;
  resolvedTextSpec?: ResolvedTextSpec;
  previewVisualUrl?: string;
}

export interface TemplateOptionDto {
  id: string;
  name: string;
  platform: string;
  format: string;
  priority: number;
  isActive: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class SourceTemplateApiService {
  private http = inject(HttpClient);
  private baseUrl = `${API_CONFIG.baseUrl}/api/v1/source-templates`;

  // List assignments
  list(params?: { 
    sourceId?: string; 
    platform?: string; 
    templateId?: string; 
    active?: boolean;
    page?: number;
    pageSize?: number;
  }): Observable<{ items: SourceTemplateAssignmentDto[]; total: number }> {
    const queryParams = new URLSearchParams();
    if (params?.sourceId) queryParams.set('sourceId', params.sourceId);
    if (params?.platform) queryParams.set('platform', params.platform);
    if (params?.templateId) queryParams.set('templateId', params.templateId);
    if (params?.active !== undefined) queryParams.set('active', String(params.active));
    if (params?.page) queryParams.set('page', String(params.page));
    if (params?.pageSize) queryParams.set('pageSize', String(params.pageSize));
    
    const url = queryParams.toString() ? `${this.baseUrl}?${queryParams}` : this.baseUrl;
    return this.http.get<{ items: SourceTemplateAssignmentDto[]; total: number }>(url);
  }

  // Get assignments for a source
  getBySource(sourceId: string): Observable<{ items: SourceTemplateAssignmentDto[] }> {
    return this.http.get<{ items: SourceTemplateAssignmentDto[] }>(`${this.baseUrl}/by-source/${sourceId}`);
  }

  // Create assignment
  create(request: CreateSourceTemplateAssignmentRequest): Observable<SourceTemplateAssignmentDto> {
    return this.http.post<SourceTemplateAssignmentDto>(this.baseUrl, request);
  }

  // Update assignment
  update(id: string, request: UpdateSourceTemplateAssignmentRequest): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.baseUrl}/${id}`, request);
  }

  // Delete assignment
  delete(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/${id}`);
  }

  // Get templates for dropdown (filtered by platform)
  getTemplatesForPlatform(platform: string): Observable<{ items: TemplateOptionDto[] }> {
    return this.http.get<{ items: TemplateOptionDto[] }>(
      `${API_CONFIG.baseUrl}/api/v1/templates?platform=${platform}&active=true&pageSize=100`
    );
  }

  // Apply template to content
  applyTemplate(contentItemId: string, request: ApplyTemplateRequest): Observable<ApplyTemplateResponse> {
    return this.http.post<ApplyTemplateResponse>(
      `${API_CONFIG.baseUrl}/api/v1/editorial/items/${contentItemId}/template/apply`,
      request
    );
  }

  // Preview template selection for all platforms
  previewSelection(contentItemId: string): Observable<Record<string, any>> {
    return this.http.get<Record<string, any>>(
      `${API_CONFIG.baseUrl}/api/v1/editorial/items/${contentItemId}/template/preview-selection`
    );
  }
}

