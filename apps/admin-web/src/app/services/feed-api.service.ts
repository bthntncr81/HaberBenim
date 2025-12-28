import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import { 
  FeedResponse, 
  FeedItemDetail, 
  FeedQueryParams, 
  Source, 
  IngestionResult 
} from '../shared/feed.models';

interface SourcesResponse {
  items: Source[];
  total: number;
  page: number;
  pageSize: number;
}

@Injectable({
  providedIn: 'root'
})
export class FeedApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  getFeed(params: FeedQueryParams): Observable<FeedResponse> {
    let httpParams = new HttpParams();

    if (params.fromUtc) httpParams = httpParams.set('fromUtc', params.fromUtc);
    if (params.toUtc) httpParams = httpParams.set('toUtc', params.toUtc);
    if (params.sourceId) httpParams = httpParams.set('sourceId', params.sourceId);
    if (params.keyword) httpParams = httpParams.set('keyword', params.keyword);
    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.decisionType) httpParams = httpParams.set('decisionType', params.decisionType);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<FeedResponse>(`${this.baseUrl}/api/v1/feed`, { params: httpParams });
  }

  getFeedItem(id: string): Observable<FeedItemDetail> {
    return this.http.get<FeedItemDetail>(`${this.baseUrl}/api/v1/feed/${id}`);
  }

  getSources(): Observable<Source[]> {
    return this.http.get<SourcesResponse>(`${this.baseUrl}/api/v1/sources`).pipe(
      map(response => response.items || [])
    );
  }

  runRssIngestion(): Observable<IngestionResult> {
    return this.http.post<IngestionResult>(`${this.baseUrl}/api/v1/ingestion/rss/run-now`, {});
  }
}
