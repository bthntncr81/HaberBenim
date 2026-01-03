import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';

export interface TemplateDto {
  id: string;
  name: string;
  platform: string;
  format: string;
  priority: number;
  isActive: boolean;
  ruleJson: string | null;
  hasSpec: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TemplateSpecDto {
  id: string;
  templateId: string;
  visualSpecJson: string | null;
  textSpecJson: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TemplateListResponse {
  items: TemplateDto[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface TemplateOptionsResponse {
  platforms: string[];
  formats: string[];
}

export interface CreateTemplateRequest {
  name: string;
  platform: string;
  format: string;
  priority?: number;
  isActive?: boolean;
  ruleJson?: string;
}

export interface UpdateTemplateRequest {
  name?: string;
  platform?: string;
  format?: string;
  priority?: number;
  isActive?: boolean;
  ruleJson?: string;
}

export interface UpdateTemplateSpecRequest {
  visualSpecJson?: string;
  textSpecJson?: string;
}

export interface TemplatePreviewRequest {
  contentItemId: string;
  variant?: string;
}

export interface TemplatePreviewResponse {
  previewUrl: string;
  resolvedVars: Record<string, string>;
  resolvedTextSpec: TextSpec | null;
}

export interface VisualSpec {
  canvas: CanvasSpec;
  layers: LayerSpec[];
}

export interface CanvasSpec {
  w: number;
  h: number;
  bg: string;
}

export interface LayerSpec {
  id: string;
  type: 'rect' | 'text' | 'image' | 'asset';
  x: number;
  y: number;
  w: number;
  h: number;
  // Common properties
  opacity?: number; // 0-1
  // Text properties
  bind?: string;
  fontSize?: number;
  fontWeight?: number;
  color?: string;
  align?: string;
  lineClamp?: number;
  // Rect properties
  fill?: string;
  fillGradient?: GradientSpec; // gradient fill
  radius?: number;
  // Image properties
  source?: string;
  fit?: string;
  // Asset properties
  assetKey?: string;
}

export interface GradientSpec {
  type: 'linear' | 'radial';
  angle?: number; // for linear, 0-360
  colors: string[]; // array of colors
  stops?: number[]; // array of stops 0-1
}

export interface TextSpec {
  instagramCaption?: string;
  xText?: string;
  tiktokHook?: string;
  youtubeTitle?: string;
  youtubeDescription?: string;
}

export interface TemplateAssetDto {
  id: string;
  key: string;
  contentType: string;
  storagePath: string;
  width: number;
  height: number;
  url: string;
  createdAtUtc: string;
}

export interface ContentItemBasic {
  id: string;
  title: string;
  webTitle?: string;
  sourceName: string;
  publishedAtUtc: string;
}

@Injectable({
  providedIn: 'root'
})
export class TemplateApiService {
  private http = inject(HttpClient);
  private baseUrl = `${API_CONFIG.baseUrl}/api/v1/templates`;

  // Template CRUD
  list(params?: { platform?: string; format?: string; active?: boolean; q?: string; page?: number; pageSize?: number }): Observable<TemplateListResponse> {
    const queryParams = new URLSearchParams();
    if (params?.platform) queryParams.set('platform', params.platform);
    if (params?.format) queryParams.set('format', params.format);
    if (params?.active !== undefined) queryParams.set('active', String(params.active));
    if (params?.q) queryParams.set('q', params.q);
    if (params?.page) queryParams.set('page', String(params.page));
    if (params?.pageSize) queryParams.set('pageSize', String(params.pageSize));
    
    const url = queryParams.toString() ? `${this.baseUrl}?${queryParams}` : this.baseUrl;
    return this.http.get<TemplateListResponse>(url);
  }

  get(id: string): Observable<TemplateDto> {
    return this.http.get<TemplateDto>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateTemplateRequest): Observable<TemplateDto> {
    return this.http.post<TemplateDto>(this.baseUrl, request);
  }

  update(id: string, request: UpdateTemplateRequest): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/${id}`);
  }

  // Spec
  getSpec(id: string): Observable<TemplateSpecDto> {
    return this.http.get<TemplateSpecDto>(`${this.baseUrl}/${id}/spec`);
  }

  saveSpec(id: string, request: UpdateTemplateSpecRequest): Observable<{ success: boolean }> {
    return this.http.put<{ success: boolean }>(`${this.baseUrl}/${id}/spec`, request);
  }

  // Preview
  preview(id: string, request: TemplatePreviewRequest): Observable<TemplatePreviewResponse> {
    return this.http.post<TemplatePreviewResponse>(`${this.baseUrl}/${id}/preview`, request);
  }

  // Options
  getOptions(): Observable<TemplateOptionsResponse> {
    return this.http.get<TemplateOptionsResponse>(`${this.baseUrl}/options`);
  }

  // Assets
  listAssets(): Observable<{ items: TemplateAssetDto[] }> {
    return this.http.get<{ items: TemplateAssetDto[] }>(`${this.baseUrl}/assets`);
  }

  uploadAsset(key: string, file: File): Observable<TemplateAssetDto> {
    const formData = new FormData();
    formData.append('key', key);
    formData.append('file', file);
    return this.http.post<TemplateAssetDto>(`${this.baseUrl}/assets`, formData);
  }

  deleteAsset(key: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.baseUrl}/assets/${key}`);
  }

  // Get recent content items for preview selection
  getRecentContent(): Observable<{ items: ContentItemBasic[] }> {
    return this.http.get<{ items: ContentItemBasic[] }>(`${API_CONFIG.baseUrl}/api/v1/public/latest?pageSize=10`);
  }
}

