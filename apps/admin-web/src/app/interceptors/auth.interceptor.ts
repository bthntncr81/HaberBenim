import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { API_CONFIG } from '../shared/api.config';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  
  // Only add token for API requests
  if (!req.url.startsWith(API_CONFIG.baseUrl)) {
    return next(req);
  }

  const token = authService.getToken();
  
  // Build headers object
  const headers: Record<string, string> = {};
  
  // Add auth token if available
  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }
  
  // Add ngrok header to skip browser warning (for ngrok tunnels)
  if (API_CONFIG.baseUrl.includes('ngrok')) {
    headers['ngrok-skip-browser-warning'] = 'true';
  }

  const authReq = req.clone({ setHeaders: headers });

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        // Token expired or invalid - logout and redirect
        authService.logout();
      }
      return throwError(() => error);
    })
  );
};

