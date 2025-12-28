import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import { DailyReportGenerateResponse, DailyReportRun, ReportRunsQueryParams } from '../shared/reports.models';

@Injectable({
  providedIn: 'root'
})
export class ReportsApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  generateDaily(date: string): Observable<DailyReportGenerateResponse> {
    return this.http.post<DailyReportGenerateResponse>(
      `${this.baseUrl}/api/v1/reports/daily/generate?date=${date}`,
      {}
    );
  }

  downloadDaily(date: string): Observable<Blob> {
    return this.http.get(
      `${this.baseUrl}/api/v1/reports/daily/download?date=${date}`,
      { responseType: 'blob' }
    );
  }

  listRuns(params: ReportRunsQueryParams = {}): Observable<DailyReportRun[]> {
    let httpParams = new HttpParams();

    if (params.from) httpParams = httpParams.set('from', params.from);
    if (params.to) httpParams = httpParams.set('to', params.to);

    return this.http.get<DailyReportRun[]>(
      `${this.baseUrl}/api/v1/reports/daily/runs`,
      { params: httpParams }
    );
  }
}

