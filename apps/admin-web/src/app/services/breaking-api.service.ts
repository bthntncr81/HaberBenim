import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import {
  MarkBreakingRequest,
  MarkBreakingResponse,
  BreakingInboxResponse,
  BreakingInboxParams
} from '../shared/breaking.models';

@Injectable({
  providedIn: 'root'
})
export class BreakingApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  /**
   * Mark content as breaking news
   */
  mark(contentId: string, payload: MarkBreakingRequest): Observable<MarkBreakingResponse> {
    return this.http.post<MarkBreakingResponse>(
      `${this.baseUrl}/api/v1/breaking/mark/${contentId}`,
      payload
    );
  }

  /**
   * Get breaking news inbox
   */
  inbox(params: BreakingInboxParams = {}): Observable<BreakingInboxResponse> {
    let httpParams = new HttpParams();

    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<BreakingInboxResponse>(
      `${this.baseUrl}/api/v1/breaking/inbox`,
      { params: httpParams }
    );
  }

  /**
   * Publish breaking content now
   */
  publishNow(contentId: string): Observable<MarkBreakingResponse> {
    return this.http.post<MarkBreakingResponse>(
      `${this.baseUrl}/api/v1/breaking/publish-now/${contentId}`,
      {}
    );
  }
}

