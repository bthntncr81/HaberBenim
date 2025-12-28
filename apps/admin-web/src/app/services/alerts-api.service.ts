import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import {
  AlertListResponse,
  AlertQueryParams,
  AlertAckResponse,
  AlertCountResponse
} from '../shared/alerts.models';

@Injectable({
  providedIn: 'root'
})
export class AlertsApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  /**
   * List alerts with filters
   */
  list(params: AlertQueryParams = {}): Observable<AlertListResponse> {
    let httpParams = new HttpParams();

    if (params.severity) httpParams = httpParams.set('severity', params.severity);
    if (params.type) httpParams = httpParams.set('type', params.type);
    if (params.acknowledged !== undefined) httpParams = httpParams.set('acknowledged', params.acknowledged.toString());
    if (params.fromUtc) httpParams = httpParams.set('fromUtc', params.fromUtc);
    if (params.toUtc) httpParams = httpParams.set('toUtc', params.toUtc);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<AlertListResponse>(
      `${this.baseUrl}/api/v1/alerts`,
      { params: httpParams }
    );
  }

  /**
   * Acknowledge an alert (Admin only)
   */
  ack(id: string): Observable<AlertAckResponse> {
    return this.http.post<AlertAckResponse>(
      `${this.baseUrl}/api/v1/alerts/${id}/ack`,
      {}
    );
  }

  /**
   * Get unacknowledged alert count
   */
  getCount(): Observable<AlertCountResponse> {
    return this.http.get<AlertCountResponse>(
      `${this.baseUrl}/api/v1/alerts/count`
    );
  }
}

