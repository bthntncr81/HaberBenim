import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="login-page">
      <div class="login-card">
        <div class="login-header">
          <span class="logo-icon">📡</span>
          <h1>Haber Benim</h1>
          <p class="subtitle">Sign in to Admin Dashboard</p>
        </div>
        
        <form class="login-form" (ngSubmit)="onSubmit()">
          @if (errorMessage) {
            <div class="error-alert">
              <span class="error-icon">⚠️</span>
              {{ errorMessage }}
            </div>
          }
          
          <div class="form-group">
            <label for="email">Email</label>
            <input 
              type="email" 
              id="email" 
              [(ngModel)]="email"
              name="email"
              placeholder="admin@local"
              [disabled]="isLoading"
              autocomplete="email"
              required
            >
          </div>
          
          <div class="form-group">
            <label for="password">Password</label>
            <input 
              type="password" 
              id="password" 
              [(ngModel)]="password"
              name="password"
              placeholder="••••••••"
              [disabled]="isLoading"
              autocomplete="current-password"
              required
            >
          </div>
          
          <button 
            type="submit" 
            class="btn-primary"
            [disabled]="isLoading || !email || !password"
          >
            @if (isLoading) {
              <span class="spinner"></span>
              Signing in...
            } @else {
              Sign In
            }
          </button>
        </form>
        
        <div class="login-footer">
          <p class="hint">Default: admin&#64;local / Admin123!</p>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .login-page {
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 100vh;
      background: linear-gradient(135deg, var(--bg-primary) 0%, var(--bg-secondary) 100%);
      padding: 20px;
    }

    .login-card {
      background: var(--bg-secondary);
      border: 1px solid var(--border-color);
      border-radius: 20px;
      padding: 48px;
      width: 100%;
      max-width: 420px;
      box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
    }

    .login-header {
      text-align: center;
      margin-bottom: 36px;

      .logo-icon {
        font-size: 48px;
        display: block;
        margin-bottom: 16px;
      }

      h1 {
        font-size: 28px;
        margin-bottom: 8px;
        background: linear-gradient(135deg, var(--accent-primary), var(--accent-secondary));
        -webkit-background-clip: text;
        -webkit-text-fill-color: transparent;
        background-clip: text;
      }

      .subtitle {
        color: var(--text-secondary);
        font-size: 15px;
      }
    }

    .error-alert {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 14px 16px;
      background: rgba(244, 33, 46, 0.12);
      border: 1px solid rgba(244, 33, 46, 0.3);
      border-radius: 10px;
      color: var(--error);
      font-size: 14px;
      margin-bottom: 20px;

      .error-icon {
        font-size: 16px;
      }
    }

    .login-form {
      display: flex;
      flex-direction: column;
      gap: 20px;

      .form-group {
        display: flex;
        flex-direction: column;
        gap: 8px;

        label {
          font-weight: 500;
          font-size: 14px;
          color: var(--text-secondary);
        }

        input {
          padding: 14px 16px;
          background: var(--bg-tertiary);
          border: 1px solid var(--border-color);
          border-radius: 10px;
          color: var(--text-primary);
          font-size: 16px;
          transition: border-color 0.2s, box-shadow 0.2s;

          &:focus {
            outline: none;
            border-color: var(--accent-primary);
            box-shadow: 0 0 0 3px rgba(29, 155, 240, 0.15);
          }

          &:disabled {
            opacity: 0.6;
            cursor: not-allowed;
          }

          &::placeholder {
            color: var(--text-secondary);
            opacity: 0.5;
          }
        }
      }

      .btn-primary {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 10px;
        padding: 16px;
        background: linear-gradient(135deg, var(--accent-primary), #0d8bd9);
        color: white;
        border: none;
        border-radius: 10px;
        font-size: 16px;
        font-weight: 600;
        margin-top: 8px;
        cursor: pointer;
        transition: opacity 0.2s, transform 0.2s;

        &:hover:not(:disabled) {
          opacity: 0.95;
          transform: translateY(-1px);
        }

        &:disabled {
          opacity: 0.6;
          cursor: not-allowed;
          transform: none;
        }
      }
    }

    .spinner {
      width: 18px;
      height: 18px;
      border: 2px solid rgba(255, 255, 255, 0.3);
      border-top-color: white;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to { transform: rotate(360deg); }
    }

    .login-footer {
      margin-top: 28px;
      text-align: center;

      .hint {
        font-size: 13px;
        color: var(--text-secondary);
        opacity: 0.7;
      }
    }
  `]
})
export class LoginComponent {
  private authService = inject(AuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  email = '';
  password = '';
  isLoading = false;
  errorMessage = '';

  constructor() {
    // If already logged in, redirect
    if (this.authService.isLoggedIn()) {
      this.router.navigate(['/feed']);
    }
  }

  onSubmit(): void {
    if (!this.email || !this.password) return;

    this.isLoading = true;
    this.errorMessage = '';

    this.authService.login(this.email, this.password).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParams['returnUrl'] || '/feed';
        this.router.navigateByUrl(returnUrl);
      },
      error: (error) => {
        this.isLoading = false;
        if (error.status === 401) {
          this.errorMessage = 'Invalid email or password';
        } else if (error.status === 0) {
          this.errorMessage = 'Unable to connect to server';
        } else {
          this.errorMessage = error.error?.error || 'Login failed. Please try again.';
        }
      }
    });
  }
}

