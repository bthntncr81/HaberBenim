import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import {
  EditorialInboxResponse,
  EditorialInboxParams,
  EditorialItem,
  SaveDraftRequest,
  SaveDraftResponse,
  CorrectionRequest,
  CorrectionResponse
} from '../shared/editorial.models';

@Injectable({
  providedIn: 'root'
})
export class EditorialApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  getInbox(params: EditorialInboxParams): Observable<EditorialInboxResponse> {
    let httpParams = new HttpParams();

    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.fromUtc) httpParams = httpParams.set('fromUtc', params.fromUtc);
    if (params.toUtc) httpParams = httpParams.set('toUtc', params.toUtc);
    if (params.sourceId) httpParams = httpParams.set('sourceId', params.sourceId);
    if (params.keyword) httpParams = httpParams.set('keyword', params.keyword);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<EditorialInboxResponse>(`${this.baseUrl}/api/v1/editorial/inbox`, { params: httpParams });
  }

  getItem(id: string): Observable<EditorialItem> {
    return this.http.get<EditorialItem>(`${this.baseUrl}/api/v1/editorial/items/${id}`);
  }

  saveDraft(id: string, payload: SaveDraftRequest): Observable<SaveDraftResponse> {
    return this.http.put<SaveDraftResponse>(`${this.baseUrl}/api/v1/editorial/items/${id}/draft`, payload);
  }

  approve(id: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/api/v1/editorial/items/${id}/approve`, {});
  }

  reject(id: string, reason: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/api/v1/editorial/items/${id}/reject`, { reason });
  }

  schedule(id: string, scheduledAtUtc: string): Observable<{ message: string; scheduledAtUtc: string }> {
    return this.http.post<{ message: string; scheduledAtUtc: string }>(
      `${this.baseUrl}/api/v1/editorial/items/${id}/schedule`, 
      { scheduledAtUtc }
    );
  }

  /**
   * Submit a correction for a published content item (Sprint 7)
   */
  correct(id: string, payload: CorrectionRequest): Observable<CorrectionResponse> {
    return this.http.post<CorrectionResponse>(
      `${this.baseUrl}/api/v1/editorial/items/${id}/correct`,
      payload
    );
  }

  /**
   * Retract (takedown) a published content item (Sprint 8)
   */
  retract(id: string, reason: string): Observable<RetractResponse> {
    return this.http.post<RetractResponse>(
      `${this.baseUrl}/api/v1/editorial/items/${id}/retract`,
      { reason }
    );
  }
}

export interface RetractResponse {
  ok: boolean;
  versionNo: number;
  error: string | null;
}

