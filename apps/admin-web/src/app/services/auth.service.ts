import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, tap, catchError, throwError } from 'rxjs';
import { API_CONFIG } from '../shared/api.config';
import { AuthState, LoginRequest, LoginResponse, User } from '../shared/auth.models';

const AUTH_STORAGE_KEY = 'haber_auth';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);
  
  private authState$ = new BehaviorSubject<AuthState | null>(this.loadFromStorage());

  constructor() {
    // Check token expiration on init
    this.checkTokenExpiration();
  }

  login(email: string, password: string): Observable<LoginResponse> {
    const request: LoginRequest = { email, password };
    
    return this.http.post<LoginResponse>(`${API_CONFIG.baseUrl}/api/v1/auth/login`, request)
      .pipe(
        tap(response => {
          const authState: AuthState = {
            accessToken: response.accessToken,
            expiresAtUtc: response.expiresAtUtc,
            user: response.user
          };
          this.saveToStorage(authState);
          this.authState$.next(authState);
        }),
        catchError(error => {
          return throwError(() => error);
        })
      );
  }

  logout(): void {
    localStorage.removeItem(AUTH_STORAGE_KEY);
    this.authState$.next(null);
    this.router.navigate(['/login']);
  }

  getUser(): User | null {
    return this.authState$.value?.user ?? null;
  }

  getRoles(): string[] {
    return this.authState$.value?.user?.roles ?? [];
  }

  isLoggedIn(): boolean {
    const state = this.authState$.value;
    if (!state) return false;
    
    // Check if token is expired
    const expiresAt = new Date(state.expiresAtUtc);
    return expiresAt > new Date();
  }

  getToken(): string | null {
    if (!this.isLoggedIn()) return null;
    return this.authState$.value?.accessToken ?? null;
  }

  hasRole(role: string): boolean {
    return this.getRoles().includes(role);
  }

  hasAnyRole(roles: string[]): boolean {
    const userRoles = this.getRoles();
    return roles.some(role => userRoles.includes(role));
  }

  getAuthState(): Observable<AuthState | null> {
    return this.authState$.asObservable();
  }

  private loadFromStorage(): AuthState | null {
    try {
      const stored = localStorage.getItem(AUTH_STORAGE_KEY);
      if (!stored) return null;
      
      const authState: AuthState = JSON.parse(stored);
      
      // Check if token is expired
      const expiresAt = new Date(authState.expiresAtUtc);
      if (expiresAt <= new Date()) {
        localStorage.removeItem(AUTH_STORAGE_KEY);
        return null;
      }
      
      return authState;
    } catch {
      localStorage.removeItem(AUTH_STORAGE_KEY);
      return null;
    }
  }

  private saveToStorage(authState: AuthState): void {
    localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(authState));
  }

  private checkTokenExpiration(): void {
    const state = this.authState$.value;
    if (!state) return;

    const expiresAt = new Date(state.expiresAtUtc);
    const now = new Date();
    
    if (expiresAt <= now) {
      this.logout();
    }
  }
}

