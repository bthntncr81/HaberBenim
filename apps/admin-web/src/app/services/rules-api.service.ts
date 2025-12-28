import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import { 
  Rule, 
  CreateRuleRequest, 
  UpdateRuleRequest, 
  RecomputeRequest, 
  RecomputeResult,
  RuleDecisionResult
} from '../shared/rule.models';

@Injectable({
  providedIn: 'root'
})
export class RulesApiService {
  private http = inject(HttpClient);
  private baseUrl = API_CONFIG.baseUrl;

  listRules(): Observable<Rule[]> {
    return this.http.get<Rule[]>(`${this.baseUrl}/api/v1/rules`);
  }

  getRule(id: string): Observable<Rule> {
    return this.http.get<Rule>(`${this.baseUrl}/api/v1/rules/${id}`);
  }

  createRule(payload: CreateRuleRequest): Observable<Rule> {
    return this.http.post<Rule>(`${this.baseUrl}/api/v1/rules`, payload);
  }

  updateRule(id: string, payload: UpdateRuleRequest): Observable<Rule> {
    return this.http.put<Rule>(`${this.baseUrl}/api/v1/rules/${id}`, payload);
  }

  deleteRule(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/v1/rules/${id}`);
  }

  recompute(payload: RecomputeRequest): Observable<RecomputeResult> {
    return this.http.post<RecomputeResult>(`${this.baseUrl}/api/v1/rules/recompute`, payload);
  }

  evaluateContent(contentId: string): Observable<RuleDecisionResult> {
    return this.http.post<RuleDecisionResult>(`${this.baseUrl}/api/v1/rules/evaluate/${contentId}`, {});
  }
}

