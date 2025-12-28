import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import {
  PublicArticle,
  PublicArticleListResponse,
  PublicQueryParams
} from '../shared/public.models';

@Injectable({
  providedIn: 'root'
})
export class PublicApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  getLatest(params: PublicQueryParams = {}): Observable<PublicArticleListResponse> {
    let httpParams = new HttpParams();

    if (params.q) httpParams = httpParams.set('q', params.q);
    if (params.fromUtc) httpParams = httpParams.set('fromUtc', params.fromUtc);
    if (params.toUtc) httpParams = httpParams.set('toUtc', params.toUtc);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<PublicArticleListResponse>(
      `${this.baseUrl}/api/v1/public/latest`,
      { params: httpParams }
    );
  }

  /**
   * Get article by GUID id
   */
  getById(id: string): Observable<PublicArticle> {
    // The API expects a GUID at /api/v1/public/{id:guid}
    return this.http.get<PublicArticle>(`${this.baseUrl}/api/v1/public/${id}`);
  }

  /**
   * Get article by path
   */
  getByPath(path: string): Observable<PublicArticle> {
    const httpParams = new HttpParams().set('path', path);
    return this.http.get<PublicArticle>(
      `${this.baseUrl}/api/v1/public/by-path`,
      { params: httpParams }
    );
  }

  search(q: string, page = 1, pageSize = 20): Observable<PublicArticleListResponse> {
    return this.getLatest({ q, page, pageSize });
  }
}

