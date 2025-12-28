import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import { AnalyticsOverview, AnalyticsQueryParams, DailyTrend } from '../shared/analytics.models';

@Injectable({
  providedIn: 'root'
})
export class AnalyticsApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  getOverview(params: AnalyticsQueryParams = {}): Observable<AnalyticsOverview> {
    let httpParams = new HttpParams();

    if (params.fromUtc) httpParams = httpParams.set('fromUtc', params.fromUtc);
    if (params.toUtc) httpParams = httpParams.set('toUtc', params.toUtc);

    return this.http.get<AnalyticsOverview>(
      `${this.baseUrl}/api/v1/analytics/overview`,
      { params: httpParams }
    );
  }

  getTrends(params: AnalyticsQueryParams = {}): Observable<DailyTrend[]> {
    let httpParams = new HttpParams();

    if (params.fromUtc) httpParams = httpParams.set('fromUtc', params.fromUtc);
    if (params.toUtc) httpParams = httpParams.set('toUtc', params.toUtc);

    return this.http.get<DailyTrend[]>(
      `${this.baseUrl}/api/v1/analytics/trends`,
      { params: httpParams }
    );
  }
}

