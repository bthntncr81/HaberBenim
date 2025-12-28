import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import {
  SourceListItem,
  SourceDetail,
  SourceListResponse,
  UpsertSourceRequest,
  ToggleActiveRequest,
  XSourceState,
  SourceQueryParams
} from '../shared/source.models';

@Injectable({
  providedIn: 'root'
})
export class SourceApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  /**
   * List sources with filtering, search, and pagination
   */
  list(params?: SourceQueryParams): Observable<SourceListResponse> {
    let httpParams = new HttpParams();
    
    if (params?.type) httpParams = httpParams.set('type', params.type);
    if (params?.category) httpParams = httpParams.set('category', params.category);
    if (params?.isActive !== undefined) httpParams = httpParams.set('isActive', String(params.isActive));
    if (params?.q) httpParams = httpParams.set('q', params.q);
    if (params?.page) httpParams = httpParams.set('page', String(params.page));
    if (params?.pageSize) httpParams = httpParams.set('pageSize', String(params.pageSize));
    
    return this.http.get<SourceListResponse>(
      `${this.baseUrl}/api/v1/sources`,
      { params: httpParams }
    );
  }

  /**
   * Get a source by ID with full details
   */
  get(id: string): Observable<SourceDetail> {
    return this.http.get<SourceDetail>(`${this.baseUrl}/api/v1/sources/${id}`);
  }

  /**
   * Create a new source (Admin only)
   */
  create(request: UpsertSourceRequest): Observable<SourceDetail> {
    return this.http.post<SourceDetail>(`${this.baseUrl}/api/v1/sources`, request);
  }

  /**
   * Update a source (Admin only)
   */
  update(id: string, request: UpsertSourceRequest): Observable<SourceDetail> {
    return this.http.put<SourceDetail>(`${this.baseUrl}/api/v1/sources/${id}`, request);
  }

  /**
   * Delete a source (Admin only)
   */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/v1/sources/${id}`);
  }

  /**
   * Toggle source active status (Admin only)
   */
  toggleActive(id: string, isActive: boolean): Observable<SourceDetail> {
    const request: ToggleActiveRequest = { isActive };
    return this.http.post<SourceDetail>(
      `${this.baseUrl}/api/v1/sources/${id}/toggle-active`,
      request
    );
  }

  /**
   * Get X source state
   */
  getXState(sourceId: string): Observable<XSourceState | null> {
    return this.http.get<XSourceState | null>(
      `${this.baseUrl}/api/v1/sources/${sourceId}/x-state`
    );
  }

  /**
   * Get all categories
   */
  getCategories(): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/api/v1/sources/categories`);
  }
}
