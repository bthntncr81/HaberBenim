import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
    OpenAiKeyStatusResponse,
    SaveOpenAiKeyRequest,
    SaveOpenAiKeyResponse,
    TestOpenAiResponse
} from '../shared/openai.models';

@Injectable({
  providedIn: 'root'
})
export class OpenAiApiService {
  private http = inject(HttpClient);
  private baseUrl = `${environment.apiBaseUrl}/api/v1/integrations/openai`;

  /**
   * Get current OpenAI API key configuration status
   */
  getStatus(): Observable<OpenAiKeyStatusResponse> {
    return this.http.get<OpenAiKeyStatusResponse>(`${this.baseUrl}/status`);
  }

  /**
   * Save OpenAI API key (encrypted at rest)
   */
  save(request: SaveOpenAiKeyRequest): Observable<SaveOpenAiKeyResponse> {
    return this.http.post<SaveOpenAiKeyResponse>(`${this.baseUrl}/save`, request);
  }

  /**
   * Test the stored OpenAI API key
   */
  test(): Observable<TestOpenAiResponse> {
    return this.http.post<TestOpenAiResponse>(`${this.baseUrl}/test`, {});
  }

  /**
   * Delete the stored OpenAI API key
   */
  delete(): Observable<void> {
    return this.http.delete<void>(this.baseUrl);
  }
}
