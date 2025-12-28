import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import { AuditLogListResponse, AuditLogQueryParams } from '../shared/audit.models';

@Injectable({
  providedIn: 'root'
})
export class AuditApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  /**
   * List audit logs with filters (Admin only)
   */
  list(params: AuditLogQueryParams = {}): Observable<AuditLogListResponse> {
    let httpParams = new HttpParams();

    if (params.fromUtc) httpParams = httpParams.set('fromUtc', params.fromUtc);
    if (params.toUtc) httpParams = httpParams.set('toUtc', params.toUtc);
    if (params.userEmail) httpParams = httpParams.set('userEmail', params.userEmail);
    if (params.path) httpParams = httpParams.set('path', params.path);
    if (params.statusCode) httpParams = httpParams.set('statusCode', params.statusCode.toString());
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<AuditLogListResponse>(
      `${this.baseUrl}/api/v1/audit/logs`,
      { params: httpParams }
    );
  }
}

