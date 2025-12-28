import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { XIntegrationApiService, OAuth1TestResult, BearerTestResult, ConfigStatusResult, TestPostResult, IngestionStatusResult, IngestionSourceStatus } from '../../services/x-integration-api.service';
import { XConnectionStatus, SystemSettingDto } from '../../shared/x-integration.models';

@Component({
  selector: 'app-x-integration',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './x-integration.component.html',
  styleUrl: './x-integration.component.scss'
})
export class XIntegrationComponent implements OnInit, OnDestroy {
  private api = inject(XIntegrationApiService);
  private pollInterval: any = null;

  // Settings - All X API Credentials
  settings = {
    apiKey: '',
    apiSecretKey: '',
    bearerToken: '',
    accessToken: '',
    accessTokenSecret: '',
    // OAuth2 settings (for PKCE flow)
    clientId: '',
    clientSecret: '',
    redirectUri: 'http://localhost:5078/api/v1/integrations/x/callback'
  };

  // Connections
  connections = signal<XConnectionStatus[]>([]);
  
  // UI State
  isLoadingSettings = signal(false);
  isSavingSettings = signal(false);
  isLoadingConnections = signal(false);
  isConnecting = signal(false);
  isTesting = signal(false);
  settingsError = signal<string | null>(null);
  connectionsError = signal<string | null>(null);

  // Test results
  testResult = signal<{ connected: boolean; message: string; username?: string } | null>(null);
  oauth1TestResult = signal<OAuth1TestResult | null>(null);
  bearerTestResult = signal<BearerTestResult | null>(null);
  configStatus = signal<ConfigStatusResult | null>(null);
  testPostResult = signal<TestPostResult | null>(null);
  
  // Test states
  isTestingOAuth1 = signal(false);
  isTestingBearer = signal(false);
  isTestingPost = signal(false);
  testTweetText = '';

  // Pending actions
  settingDefaultId = signal<string | null>(null);
  disconnectingId = signal<string | null>(null);

  // Ingestion status
  ingestionStatus = signal<IngestionStatusResult | null>(null);
  isLoadingIngestion = signal(false);
  isTriggeringIngestion = signal(false);
  triggeringSourceId = signal<string | null>(null);

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  ngOnInit(): void {
    this.loadSettings();
    this.loadConnections();
    this.loadConfigStatus();
    this.loadIngestionStatus();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  loadSettings(): void {
    this.isLoadingSettings.set(true);
    this.settingsError.set(null);

    this.api.getSettings().subscribe({
      next: (settings) => {
        // Map settings to form
        settings.forEach(s => {
          switch (s.key) {
            case 'X_API_KEY': this.settings.apiKey = s.value; break;
            case 'X_API_SECRET_KEY': this.settings.apiSecretKey = s.value; break;
            // canonical: X_APP_BEARER_TOKEN, legacy: X_BEARER_TOKEN
            case 'X_APP_BEARER_TOKEN': this.settings.bearerToken = s.value; break;
            case 'X_BEARER_TOKEN': if (!this.settings.bearerToken) this.settings.bearerToken = s.value; break;
            case 'X_ACCESS_TOKEN': this.settings.accessToken = s.value; break;
            case 'X_ACCESS_TOKEN_SECRET': this.settings.accessTokenSecret = s.value; break;
            case 'X_CLIENT_ID': this.settings.clientId = s.value; break;
            case 'X_CLIENT_SECRET': this.settings.clientSecret = s.value; break;
            case 'X_REDIRECT_URI': this.settings.redirectUri = s.value; break;
          }
        });
        this.isLoadingSettings.set(false);
      },
      error: (err) => {
        console.error('Failed to load X settings', err);
        this.settingsError.set('Failed to load settings');
        this.isLoadingSettings.set(false);
      }
    });
  }

  saveSettings(): void {
    if (this.isSavingSettings()) return;

    this.isSavingSettings.set(true);
    this.settingsError.set(null);

    // Normalize bearer token (handle accidental URL-encoding)
    let bearer = (this.settings.bearerToken || '').trim();
    try {
      if (bearer.includes('%')) bearer = decodeURIComponent(bearer);
    } catch {
      // ignore
    }

    const settingsToSave: SystemSettingDto[] = [
      { key: 'X_API_KEY', value: this.settings.apiKey },
      { key: 'X_API_SECRET_KEY', value: this.settings.apiSecretKey },
      // canonical key used by backend
      { key: 'X_APP_BEARER_TOKEN', value: bearer },
      // legacy key kept for compatibility (can be removed later)
      { key: 'X_BEARER_TOKEN', value: bearer },
      { key: 'X_ACCESS_TOKEN', value: this.settings.accessToken },
      { key: 'X_ACCESS_TOKEN_SECRET', value: this.settings.accessTokenSecret },
      { key: 'X_CLIENT_ID', value: this.settings.clientId },
      { key: 'X_CLIENT_SECRET', value: this.settings.clientSecret },
      { key: 'X_REDIRECT_URI', value: this.settings.redirectUri }
    ];

    this.api.updateSettings(settingsToSave).subscribe({
      next: () => {
        this.isSavingSettings.set(false);
        this.showToastMessage('Settings saved successfully', 'success');
      },
      error: (err) => {
        console.error('Failed to save settings', err);
        this.settingsError.set(err.error?.message || 'Failed to save settings');
        this.isSavingSettings.set(false);
      }
    });
  }

  loadConnections(): void {
    this.isLoadingConnections.set(true);
    this.connectionsError.set(null);

    this.api.getStatus().subscribe({
      next: (response) => {
        this.connections.set(response.connections);
        this.isLoadingConnections.set(false);
      },
      error: (err) => {
        console.error('Failed to load connections', err);
        this.connectionsError.set('Failed to load connections');
        this.isLoadingConnections.set(false);
      }
    });
  }

  connectToX(): void {
    if (this.isConnecting()) return;

    // Validate client ID is set
    if (!this.settings.clientId.trim()) {
      this.showToastMessage('Please save your X Client ID first', 'error');
      return;
    }

    this.isConnecting.set(true);

    this.api.connect().subscribe({
      next: (response) => {
        // Open authorization URL in new window
        const authWindow = window.open(response.authorizeUrl, '_blank', 'width=600,height=700');
        
        if (authWindow) {
          this.showToastMessage('Please complete authorization in the popup window', 'success');
          // Start polling for connection status
          this.startPolling();
        } else {
          this.showToastMessage('Popup blocked! Please allow popups and try again.', 'error');
          this.isConnecting.set(false);
        }
      },
      error: (err) => {
        console.error('Failed to start connect flow', err);
        this.showToastMessage(err.error?.message || 'Failed to connect to X', 'error');
        this.isConnecting.set(false);
      }
    });
  }

  startPolling(): void {
    const initialCount = this.connections().length;
    let attempts = 0;
    const maxAttempts = 60; // 2 minutes max

    this.pollInterval = setInterval(() => {
      attempts++;

      this.api.getStatus().subscribe({
        next: (response) => {
          this.connections.set(response.connections);

          // Check if new connection was added
          if (response.connections.length > initialCount) {
            this.stopPolling();
            this.isConnecting.set(false);
            this.showToastMessage('Successfully connected to X!', 'success');
          } else if (attempts >= maxAttempts) {
            this.stopPolling();
            this.isConnecting.set(false);
            this.showToastMessage('Connection timeout. Please try again.', 'error');
          }
        },
        error: () => {
          // Ignore polling errors
        }
      });
    }, 2000);
  }

  stopPolling(): void {
    if (this.pollInterval) {
      clearInterval(this.pollInterval);
      this.pollInterval = null;
    }
  }

  testConnection(): void {
    if (this.isTesting()) return;

    this.isTesting.set(true);
    this.testResult.set(null);

    this.api.testConnection().subscribe({
      next: (result) => {
        this.testResult.set(result);
        this.isTesting.set(false);
      },
      error: (err) => {
        this.testResult.set({
          connected: false,
          message: err.error?.message || 'Connection test failed'
        });
        this.isTesting.set(false);
      }
    });
  }

  testOAuth1(): void {
    if (this.isTestingOAuth1()) return;

    this.isTestingOAuth1.set(true);
    this.oauth1TestResult.set(null);

    this.api.testOAuth1().subscribe({
      next: (result) => {
        this.oauth1TestResult.set(result);
        this.isTestingOAuth1.set(false);
        if (result.success) {
          this.showToastMessage(`OAuth 1.0a test successful! Connected as @${result.user?.username}`, 'success');
        }
      },
      error: (err) => {
        this.oauth1TestResult.set({
          success: false,
          configured: false,
          message: err.error?.message || 'OAuth 1.0a test failed',
          error: err.message
        });
        this.isTestingOAuth1.set(false);
      }
    });
  }

  testBearer(): void {
    if (this.isTestingBearer()) return;

    this.isTestingBearer.set(true);
    this.bearerTestResult.set(null);

    this.api.testBearer().subscribe({
      next: (result) => {
        this.bearerTestResult.set(result);
        this.isTestingBearer.set(false);
        if (result.success) {
          this.showToastMessage(`Bearer Token test successful! Found ${result.resultCount} tweets`, 'success');
        }
      },
      error: (err) => {
        this.bearerTestResult.set({
          success: false,
          configured: false,
          message: err.error?.message || 'Bearer Token test failed',
          error: err.message
        });
        this.isTestingBearer.set(false);
      }
    });
  }

  loadConfigStatus(): void {
    this.api.getConfigStatus().subscribe({
      next: (result) => {
        this.configStatus.set(result);
      },
      error: (err) => {
        console.error('Failed to load config status', err);
      }
    });
  }

  testTweet(): void {
    if (this.isTestingPost() || !this.testTweetText.trim()) return;

    this.isTestingPost.set(true);
    this.testPostResult.set(null);

    this.api.testPost(this.testTweetText).subscribe({
      next: (result) => {
        this.testPostResult.set(result);
        this.isTestingPost.set(false);
        if (result.success) {
          this.showToastMessage('Tweet posted successfully!', 'success');
          this.testTweetText = '';
        } else {
          this.showToastMessage(result.message || 'Failed to post tweet', 'error');
        }
      },
      error: (err) => {
        this.testPostResult.set({
          success: false,
          message: err.error?.message || 'Failed to post tweet',
          error: err.message
        });
        this.isTestingPost.set(false);
        this.showToastMessage(err.error?.message || 'Failed to post tweet', 'error');
      }
    });
  }

  setDefault(connection: XConnectionStatus): void {
    if (this.settingDefaultId()) return;

    this.settingDefaultId.set(connection.id);

    this.api.setDefault(connection.id).subscribe({
      next: () => {
        this.settingDefaultId.set(null);
        this.showToastMessage(`Set @${connection.xUsername} as default publisher`, 'success');
        this.loadConnections();
      },
      error: (err) => {
        this.settingDefaultId.set(null);
        this.showToastMessage(err.error?.message || 'Failed to set default', 'error');
      }
    });
  }

  disconnect(connection: XConnectionStatus): void {
    if (this.disconnectingId()) return;

    if (!confirm(`Are you sure you want to disconnect @${connection.xUsername}?`)) {
      return;
    }

    this.disconnectingId.set(connection.id);

    this.api.disconnect(connection.id).subscribe({
      next: () => {
        this.disconnectingId.set(null);
        this.showToastMessage(`Disconnected @${connection.xUsername}`, 'success');
        this.loadConnections();
      },
      error: (err) => {
        this.disconnectingId.set(null);
        this.showToastMessage(err.error?.message || 'Failed to disconnect', 'error');
      }
    });
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString();
  }

  isExpiringSoon(expiresAt: string): boolean {
    const expires = new Date(expiresAt);
    const now = new Date();
    const hourFromNow = new Date(now.getTime() + 60 * 60 * 1000);
    return expires < hourFromNow;
  }

  loadIngestionStatus(): void {
    this.isLoadingIngestion.set(true);

    this.api.getIngestionStatus().subscribe({
      next: (result) => {
        this.ingestionStatus.set(result);
        this.isLoadingIngestion.set(false);
      },
      error: (err) => {
        console.error('Failed to load ingestion status', err);
        this.isLoadingIngestion.set(false);
      }
    });
  }

  triggerIngestion(): void {
    if (this.isTriggeringIngestion()) return;

    this.isTriggeringIngestion.set(true);

    this.api.triggerIngestion().subscribe({
      next: (result) => {
        this.isTriggeringIngestion.set(false);
        if (result.success) {
          this.showToastMessage(`Ingestion completed! Polled ${result.sourcesPolled} sources, total ${result.totalXContentItems} items`, 'success');
          this.loadIngestionStatus();
        } else {
          this.showToastMessage(result.error || 'Ingestion failed', 'error');
        }
      },
      error: (err) => {
        this.isTriggeringIngestion.set(false);
        this.showToastMessage(err.error?.error || 'Ingestion failed', 'error');
      }
    });
  }

  triggerSourceIngestion(source: IngestionSourceStatus): void {
    if (this.triggeringSourceId()) return;

    this.triggeringSourceId.set(source.sourceId);

    this.api.triggerSourceIngestion(source.sourceId).subscribe({
      next: (result) => {
        this.triggeringSourceId.set(null);
        if (result.success) {
          this.showToastMessage(`Ingestion completed for ${source.sourceName}! ${result.contentItemCount} items`, 'success');
          this.loadIngestionStatus();
        } else {
          this.showToastMessage(result.error || 'Ingestion failed', 'error');
        }
      },
      error: (err) => {
        this.triggeringSourceId.set(null);
        this.showToastMessage(err.error?.error || 'Ingestion failed', 'error');
      }
    });
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}

