import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { InstagramApiService } from '../../services/instagram-api.service';
import { SettingsApiService } from '../../services/settings-api.service';
import {
    InstagramConfigStatus,
    InstagramConnectionDto,
    InstagramPageInfo
} from '../../shared/instagram.models';

type ViewState = 'loading' | 'main' | 'callback' | 'select-page';

@Component({
  selector: 'app-instagram-integration',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './instagram-integration.component.html',
  styleUrl: './instagram-integration.component.scss'
})
export class InstagramIntegrationComponent implements OnInit {
  private api = inject(InstagramApiService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  // State
  viewState = signal<ViewState>('loading');
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  // Data
  configStatus = signal<InstagramConfigStatus | null>(null);
  connections = signal<InstagramConnectionDto[]>([]);
  
  // OAuth flow state
  oauthState = signal<string | null>(null);
  availablePages = signal<InstagramPageInfo[]>([]);
  selectedPageId = signal<string | null>(null);
  connectionName = '';

  // Actions in progress
  isConnecting = signal(false);
  isExchanging = signal(false);
  isCompleting = signal(false);
  actionInProgress = signal<string | null>(null);

  // Settings
  private settingsApi = inject(SettingsApiService);
  showSettingsForm = signal(false);
  isSavingSettings = signal(false);
  settingsForm = {
    appId: '',
    appSecret: '',
    redirectUri: 'http://localhost:4200/integrations/instagram',
    publicAssetBaseUrl: ''
  };

  // Add new account toggle
  showAddAccount = signal(false);

  // Manual connection form
  isSavingManual = signal(false);
  manualForm = {
    name: '',
    igUserId: '',
    igUsername: '',
    accessToken: ''
  };

  ngOnInit(): void {
    // Check if this is a callback from Facebook OAuth
    const code = this.route.snapshot.queryParamMap.get('code');
    const state = this.route.snapshot.queryParamMap.get('state');
    const error = this.route.snapshot.queryParamMap.get('error');
    const errorDescription = this.route.snapshot.queryParamMap.get('error_description');

    if (error) {
      this.errorMessage.set(`Authorization denied: ${errorDescription || error}`);
      this.viewState.set('main');
      this.loadData();
    } else if (code && state) {
      // Handle OAuth callback
      this.handleCallback(code, state);
    } else {
      // Normal page load
      this.loadData();
    }
  }

  loadData(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    // Load settings
    this.loadSettings();

    // Load config status and connections in parallel
    this.api.configStatus().subscribe({
      next: (status) => {
        this.configStatus.set(status);
      },
      error: (err) => {
        console.error('Failed to load config status', err);
      }
    });

    this.api.status().subscribe({
      next: (response) => {
        this.connections.set(response.connections);
        this.isLoading.set(false);
        this.viewState.set('main');
      },
      error: (err) => {
        console.error('Failed to load connections', err);
        this.errorMessage.set('Failed to load Instagram connections');
        this.isLoading.set(false);
        this.viewState.set('main');
      }
    });
  }

  loadSettings(): void {
    this.settingsApi.getByPrefix('META').subscribe({
      next: (settings) => {
        for (const s of settings) {
          if (s.key === 'META_APP_ID') this.settingsForm.appId = s.value;
          if (s.key === 'META_APP_SECRET') this.settingsForm.appSecret = s.value;
          if (s.key === 'META_REDIRECT_URI') this.settingsForm.redirectUri = s.value;
        }
      },
      error: (err) => console.error('Failed to load META settings', err)
    });

    this.settingsApi.getByPrefix('PUBLIC_ASSET').subscribe({
      next: (settings) => {
        for (const s of settings) {
          if (s.key === 'PUBLIC_ASSET_BASE_URL') this.settingsForm.publicAssetBaseUrl = s.value;
        }
      },
      error: (err) => console.error('Failed to load PUBLIC_ASSET settings', err)
    });
  }

  toggleSettingsForm(): void {
    this.showSettingsForm.set(!this.showSettingsForm());
  }

  saveSettings(): void {
    if (this.isSavingSettings()) return;

    this.isSavingSettings.set(true);
    this.errorMessage.set(null);

    const settings = [
      { key: 'META_APP_ID', value: this.settingsForm.appId.trim() },
      { key: 'META_APP_SECRET', value: this.settingsForm.appSecret.trim() },
      { key: 'META_REDIRECT_URI', value: this.settingsForm.redirectUri.trim() },
      { key: 'PUBLIC_ASSET_BASE_URL', value: this.settingsForm.publicAssetBaseUrl.trim() }
    ].filter(s => s.value); // Only send non-empty values

    this.settingsApi.updateBatch(settings).subscribe({
      next: () => {
        this.isSavingSettings.set(false);
        this.successMessage.set('Ayarlar kaydedildi!');
        this.showSettingsForm.set(false);
        this.loadData(); // Reload to update config status
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: (err) => {
        this.isSavingSettings.set(false);
        this.errorMessage.set('Ayarlar kaydedilemedi: ' + (err.error?.message || err.message));
      }
    });
  }

  toggleAddAccount(): void {
    this.showAddAccount.set(!this.showAddAccount());
  }

  saveManualConnection(): void {
    if (this.isSavingManual()) return;

    const { name, igUserId, igUsername, accessToken } = this.manualForm;
    
    if (!name.trim() || !igUserId.trim() || !accessToken.trim()) {
      this.errorMessage.set('Ad, Instagram User ID ve Access Token zorunludur.');
      return;
    }

    this.isSavingManual.set(true);
    this.errorMessage.set(null);

    this.api.addManual(
      name.trim(),
      igUserId.trim(),
      igUsername.trim() || null,
      accessToken.trim()
    ).subscribe({
      next: (response) => {
        this.isSavingManual.set(false);
        if (response.success) {
          this.successMessage.set(response.message || 'Instagram bağlantısı eklendi!');
          this.showAddAccount.set(false);
          this.manualForm = { name: '', igUserId: '', igUsername: '', accessToken: '' };
          this.loadData();
          setTimeout(() => this.successMessage.set(null), 3000);
        } else {
          this.errorMessage.set(response.message || 'Bağlantı eklenemedi.');
        }
      },
      error: (err) => {
        this.isSavingManual.set(false);
        this.errorMessage.set('Bağlantı eklenemedi: ' + (err.error?.message || err.message));
      }
    });
  }

  startConnect(): void {
    if (this.isConnecting()) return;

    this.isConnecting.set(true);
    this.errorMessage.set(null);

    this.api.connect().subscribe({
      next: (response) => {
        // Store state for later verification
        this.oauthState.set(response.state);
        sessionStorage.setItem('instagram_oauth_state', response.state);
        
        // Redirect to Facebook OAuth
        window.location.href = response.authorizeUrl;
      },
      error: (err) => {
        this.isConnecting.set(false);
        const errorMsg = err.error?.details || err.error?.error || 'Failed to start connection';
        this.errorMessage.set(errorMsg);
      }
    });
  }

  private handleCallback(code: string, state: string): void {
    this.viewState.set('callback');
    this.isExchanging.set(true);
    this.errorMessage.set(null);

    // Get redirect URI (current page without query params)
    const redirectUri = window.location.origin + window.location.pathname;

    this.api.exchange(code, state, redirectUri).subscribe({
      next: (response) => {
        this.isExchanging.set(false);
        this.oauthState.set(state);
        this.availablePages.set(response.pages);
        
        // Filter to only pages with Instagram
        const pagesWithInstagram = response.pages.filter(p => p.hasInstagram);
        
        if (pagesWithInstagram.length === 0) {
          this.errorMessage.set('No Facebook Pages with connected Professional Instagram accounts found. Please connect an Instagram Business or Creator account to one of your Facebook Pages.');
          this.viewState.set('main');
          this.loadData();
        } else if (pagesWithInstagram.length === 1) {
          // Auto-select if only one option
          this.selectedPageId.set(pagesWithInstagram[0].pageId);
          this.connectionName = pagesWithInstagram[0].igUsername || pagesWithInstagram[0].pageName;
          this.viewState.set('select-page');
        } else {
          this.viewState.set('select-page');
        }

        // Clear URL params
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: {},
          replaceUrl: true
        });
      },
      error: (err) => {
        this.isExchanging.set(false);
        const errorMsg = err.error?.error || 'Failed to exchange authorization code';
        this.errorMessage.set(errorMsg);
        this.viewState.set('main');
        this.loadData();
        
        // Clear URL params
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: {},
          replaceUrl: true
        });
      }
    });
  }

  selectPage(pageId: string): void {
    this.selectedPageId.set(pageId);
    const page = this.availablePages().find(p => p.pageId === pageId);
    if (page) {
      this.connectionName = page.igUsername || page.pageName;
    }
  }

  completeConnection(): void {
    const pageId = this.selectedPageId();
    const state = this.oauthState() || sessionStorage.getItem('instagram_oauth_state');

    if (!pageId || !state || !this.connectionName.trim()) {
      this.errorMessage.set('Please select a page and provide a connection name');
      return;
    }

    this.isCompleting.set(true);
    this.errorMessage.set(null);

    this.api.complete(this.connectionName.trim(), pageId, state).subscribe({
      next: (response) => {
        this.isCompleting.set(false);
        sessionStorage.removeItem('instagram_oauth_state');

        if (response.success) {
          this.successMessage.set(response.message || 'Instagram connected successfully!');
          this.viewState.set('main');
          this.showAddAccount.set(false);
          this.loadData();
        } else {
          this.errorMessage.set(response.message || 'Failed to complete connection');
        }
      },
      error: (err) => {
        this.isCompleting.set(false);
        const errorMsg = err.error?.message || 'Failed to complete connection';
        this.errorMessage.set(errorMsg);
      }
    });
  }

  cancelPageSelection(): void {
    this.viewState.set('main');
    this.selectedPageId.set(null);
    this.availablePages.set([]);
    this.connectionName = '';
    sessionStorage.removeItem('instagram_oauth_state');
    this.loadData();
  }

  setDefault(connectionId: string): void {
    if (this.actionInProgress()) return;

    this.actionInProgress.set(connectionId);
    this.errorMessage.set(null);

    this.api.setDefault(connectionId).subscribe({
      next: () => {
        this.actionInProgress.set(null);
        this.successMessage.set('Default connection updated');
        this.loadData();
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: (err) => {
        this.actionInProgress.set(null);
        this.errorMessage.set('Failed to set default');
      }
    });
  }

  disconnect(connectionId: string, connectionName: string): void {
    if (this.actionInProgress()) return;

    if (!confirm(`Disconnect "${connectionName}"? This will disable Instagram publishing for this account.`)) {
      return;
    }

    this.actionInProgress.set(connectionId);
    this.errorMessage.set(null);

    this.api.disconnect(connectionId).subscribe({
      next: () => {
        this.actionInProgress.set(null);
        this.successMessage.set('Connection disconnected');
        this.loadData();
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: (err) => {
        this.actionInProgress.set(null);
        this.errorMessage.set('Failed to disconnect');
      }
    });
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString();
  }

  isExpiringSoon(dateStr: string): boolean {
    const expiresAt = new Date(dateStr);
    const now = new Date();
    const daysUntilExpiry = (expiresAt.getTime() - now.getTime()) / (1000 * 60 * 60 * 24);
    return daysUntilExpiry < 7 && daysUntilExpiry > 0;
  }

  isExpired(dateStr: string): boolean {
    return new Date(dateStr) < new Date();
  }

  get pagesWithInstagram(): InstagramPageInfo[] {
    return this.availablePages().filter(p => p.hasInstagram);
  }

  get pagesWithoutInstagram(): InstagramPageInfo[] {
    return this.availablePages().filter(p => !p.hasInstagram);
  }

  get activeConnections(): InstagramConnectionDto[] {
    return this.connections().filter(c => c.isActive);
  }

  get inactiveConnections(): InstagramConnectionDto[] {
    return this.connections().filter(c => !c.isActive);
  }
}

