import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { OpenAiApiService } from '../../services/openai-api.service';
import { OpenAiKeyStatusResponse } from '../../shared/openai.models';

@Component({
  selector: 'app-openai-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './openai-config.component.html',
  styleUrl: './openai-config.component.scss'
})
export class OpenAiConfigComponent implements OnInit {
  private openAiApi = inject(OpenAiApiService);

  // State
  openAiStatus = signal<OpenAiKeyStatusResponse | null>(null);
  isLoading = signal(false);
  isSaving = signal(false);
  isTesting = signal(false);
  
  // Form
  form = {
    apiKey: '',
    orgId: ''
  };
  showApiKey = signal(false);
  
  // Messages
  errorMessage = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadStatus();
  }

  loadStatus(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);

    this.openAiApi.getStatus().subscribe({
      next: (status) => {
        this.openAiStatus.set(status);
        this.isLoading.set(false);
        if (status.orgId) {
          this.form.orgId = status.orgId;
        }
      },
      error: (err) => {
        this.isLoading.set(false);
        this.errorMessage.set('Failed to load OpenAI status');
        console.error('Failed to load OpenAI status', err);
      }
    });
  }

  saveKey(): void {
    if (this.isSaving()) return;

    const apiKey = this.form.apiKey.trim();
    if (!apiKey) {
      this.errorMessage.set('API key is required');
      return;
    }

    if (apiKey.length < 20) {
      this.errorMessage.set('API key is too short');
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.openAiApi.save({
      apiKey,
      orgId: this.form.orgId.trim() || null
    }).subscribe({
      next: (response) => {
        this.isSaving.set(false);
        if (response.success) {
          this.successMessage.set(response.message || 'API key saved successfully');
          this.form.apiKey = '';
          this.showApiKey.set(false);
          this.loadStatus();
          setTimeout(() => this.successMessage.set(null), 5000);
        } else {
          this.errorMessage.set(response.message || 'Failed to save API key');
        }
      },
      error: (err) => {
        this.isSaving.set(false);
        this.errorMessage.set(err.error?.error || 'Failed to save API key');
      }
    });
  }

  testKey(): void {
    if (this.isTesting()) return;

    this.isTesting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);

    this.openAiApi.test().subscribe({
      next: (response) => {
        this.isTesting.set(false);
        if (response.success) {
          const sora2Msg = response.sora2Available ? 'sora-2 available!' : 'sora-2 not found';
          this.successMessage.set(`Connection successful! ${response.modelCount} models found. ${sora2Msg}`);
          this.loadStatus();
          setTimeout(() => this.successMessage.set(null), 5000);
        } else {
          this.errorMessage.set(response.error || 'Connection test failed');
          this.loadStatus();
        }
      },
      error: (err) => {
        this.isTesting.set(false);
        this.errorMessage.set(err.error?.error || 'Connection test failed');
      }
    });
  }

  deleteKey(): void {
    if (!confirm('Are you sure you want to delete the OpenAI API key?')) {
      return;
    }

    this.openAiApi.delete().subscribe({
      next: () => {
        this.successMessage.set('API key deleted');
        this.loadStatus();
        setTimeout(() => this.successMessage.set(null), 3000);
      },
      error: () => {
        this.errorMessage.set('Failed to delete API key');
      }
    });
  }

  toggleShowApiKey(): void {
    this.showApiKey.set(!this.showApiKey());
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString();
  }
}
