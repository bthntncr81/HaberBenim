import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import {
  PublishJobListResponse,
  PublishJobQueryParams,
  ChannelPublishLog,
  EnqueueResponse
} from '../shared/publish.models';

@Injectable({
  providedIn: 'root'
})
export class PublishApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  enqueue(contentId: string): Observable<EnqueueResponse> {
    return this.http.post<EnqueueResponse>(
      `${this.baseUrl}/api/v1/publish/enqueue/${contentId}`,
      {}
    );
  }

  listJobs(params: PublishJobQueryParams): Observable<PublishJobListResponse> {
    let httpParams = new HttpParams();

    if (params.status) httpParams = httpParams.set('status', params.status);
    if (params.fromUtc) httpParams = httpParams.set('fromUtc', params.fromUtc);
    if (params.toUtc) httpParams = httpParams.set('toUtc', params.toUtc);
    if (params.page) httpParams = httpParams.set('page', params.page.toString());
    if (params.pageSize) httpParams = httpParams.set('pageSize', params.pageSize.toString());

    return this.http.get<PublishJobListResponse>(
      `${this.baseUrl}/api/v1/publish/jobs`,
      { params: httpParams }
    );
  }

  getLogs(contentId: string): Observable<ChannelPublishLog[]> {
    return this.http.get<ChannelPublishLog[]>(
      `${this.baseUrl}/api/v1/publish/logs`,
      { params: { contentId } }
    );
  }

  runDue(): Observable<{ jobsProcessed: number; message: string }> {
    return this.http.post<{ jobsProcessed: number; message: string }>(
      `${this.baseUrl}/api/v1/publish/run-due`,
      {}
    );
  }
}

