import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import {
  MediaAsset,
  MediaRefreshResponse,
  MediaEnsureResponse,
  GenerateImageRequest,
  ImageGenerationResult
} from '../shared/media.models';

@Injectable({
  providedIn: 'root'
})
export class EditorialMediaApiService {
  private http = inject(HttpClient);
  private baseUrl = `${API_CONFIG.baseUrl}/api/v1/editorial/items`;

  /**
   * Get all media assets linked to a content item
   */
  list(contentId: string): Observable<MediaAsset[]> {
    return this.http.get<MediaAsset[]>(`${this.baseUrl}/${contentId}/media`);
  }

  /**
   * Refresh media from source (re-discover and download from original source)
   */
  refreshFromSource(contentId: string): Observable<MediaRefreshResponse> {
    return this.http.post<MediaRefreshResponse>(
      `${this.baseUrl}/${contentId}/media/refresh-from-source`,
      {}
    );
  }

  /**
   * Generate AI image for content
   */
  generate(contentId: string, request?: GenerateImageRequest): Observable<ImageGenerationResult> {
    return this.http.post<ImageGenerationResult>(
      `${this.baseUrl}/${contentId}/media/generate`,
      request ?? {}
    );
  }

  /**
   * Set an asset as primary for content
   */
  setPrimary(contentId: string, assetId: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.baseUrl}/${contentId}/media/set-primary/${assetId}`,
      {}
    );
  }

  /**
   * Remove a media asset from content
   */
  delete(contentId: string, assetId: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(
      `${this.baseUrl}/${contentId}/media/${assetId}`
    );
  }

  /**
   * Ensure content has a primary image (auto-discover or generate if AI enabled)
   */
  ensurePrimary(contentId: string): Observable<MediaEnsureResponse> {
    return this.http.post<MediaEnsureResponse>(
      `${this.baseUrl}/${contentId}/media/ensure-primary`,
      {}
    );
  }
}

