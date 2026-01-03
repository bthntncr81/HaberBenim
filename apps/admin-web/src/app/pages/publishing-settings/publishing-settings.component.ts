import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  PublishingPolicyApiService,
  PublishingPolicy,
  PlatformPolicy,
  EmergencyRules,
  DailyPublishingStats,
  TimeWindow
} from '../../services/publishing-policy-api.service';

@Component({
  selector: 'app-publishing-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './publishing-settings.component.html',
  styleUrl: './publishing-settings.component.scss'
})
export class PublishingSettingsComponent implements OnInit {
  private api = inject(PublishingPolicyApiService);

  // Data
  policy = signal<PublishingPolicy | null>(null);
  stats = signal<DailyPublishingStats[]>([]);

  // UI State
  isLoading = signal(true);
  isSaving = signal(false);
  error = signal<string | null>(null);
  selectedPlatform = signal('Instagram');
  activeTab = signal<'platforms' | 'emergency'>('platforms');

  // Platforms
  platforms = ['Instagram', 'X', 'TikTok', 'YouTube', 'Web', 'Mobile'];

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  // Computed
  currentPlatformPolicy = computed(() => {
    const p = this.policy();
    if (!p) return null;
    return p.platforms[this.selectedPlatform()] || null;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.api.getPolicy().subscribe({
      next: (policy) => {
        this.policy.set(policy);
        this.isLoading.set(false);
        this.loadStats();
      },
      error: (err) => {
        console.error('Failed to load policy', err);
        this.error.set('Failed to load publishing policy');
        this.isLoading.set(false);
      }
    });
  }

  loadStats(): void {
    this.api.getStats().subscribe({
      next: (stats) => this.stats.set(stats),
      error: (err) => console.error('Failed to load stats', err)
    });
  }

  selectPlatform(platform: string): void {
    this.selectedPlatform.set(platform);
  }

  getPlatformIcon(platform: string): string {
    switch (platform) {
      case 'Instagram': return '📷';
      case 'X': return '𝕏';
      case 'TikTok': return '🎵';
      case 'YouTube': return '▶️';
      case 'Web': return '🌐';
      case 'Mobile': return '📱';
      default: return '📄';
    }
  }

  getPlatformStats(platform: string): DailyPublishingStats | null {
    return this.stats().find(s => s.platform === platform) || null;
  }

  // Platform policy updates
  updatePlatformEnabled(enabled: boolean): void {
    this.updatePlatformField('isEnabled', enabled);
  }

  updateDailyLimit(limit: number): void {
    this.updatePlatformField('dailyLimit', Math.max(0, limit));
  }

  updateMinInterval(minutes: number): void {
    this.updatePlatformField('minIntervalMinutes', Math.max(0, minutes));
  }

  updateEmergencyOverride(enabled: boolean): void {
    this.updatePlatformField('emergencyOverride', enabled);
  }

  updateNightModeStart(time: string): void {
    const p = this.policy();
    if (!p) return;
    const platform = this.selectedPlatform();
    p.platforms[platform].nightMode.start = time;
    this.policy.set({ ...p });
  }

  updateNightModeEnd(time: string): void {
    const p = this.policy();
    if (!p) return;
    const platform = this.selectedPlatform();
    p.platforms[platform].nightMode.end = time;
    this.policy.set({ ...p });
  }

  updateNightModeSilencePush(enabled: boolean): void {
    const p = this.policy();
    if (!p) return;
    const platform = this.selectedPlatform();
    p.platforms[platform].nightMode.silencePush = enabled;
    this.policy.set({ ...p });
  }

  updateNightModeQueueForMorning(enabled: boolean): void {
    const p = this.policy();
    if (!p) return;
    const platform = this.selectedPlatform();
    p.platforms[platform].nightMode.queueForMorning = enabled;
    this.policy.set({ ...p });
  }

  updateWindowStart(index: number, time: string): void {
    const p = this.policy();
    if (!p) return;
    const platform = this.selectedPlatform();
    p.platforms[platform].allowedWindows[index].start = time;
    this.policy.set({ ...p });
  }

  updateWindowEnd(index: number, time: string): void {
    const p = this.policy();
    if (!p) return;
    const platform = this.selectedPlatform();
    p.platforms[platform].allowedWindows[index].end = time;
    this.policy.set({ ...p });
  }

  addWindow(): void {
    const p = this.policy();
    if (!p) return;
    const platform = this.selectedPlatform();
    p.platforms[platform].allowedWindows.push({ start: '09:00', end: '18:00' });
    this.policy.set({ ...p });
  }

  removeWindow(index: number): void {
    const p = this.policy();
    if (!p) return;
    const platform = this.selectedPlatform();
    p.platforms[platform].allowedWindows.splice(index, 1);
    this.policy.set({ ...p });
  }

  private updatePlatformField(field: keyof PlatformPolicy, value: any): void {
    const p = this.policy();
    if (!p) return;
    const platform = this.selectedPlatform();
    (p.platforms[platform] as any)[field] = value;
    this.policy.set({ ...p });
  }

  // Emergency rules updates
  updateKeywords(keywordsText: string): void {
    const p = this.policy();
    if (!p) return;
    p.emergencyRules.keywords = keywordsText
      .split('\n')
      .map(k => k.trim())
      .filter(k => k.length > 0);
    this.policy.set({ ...p });
  }

  updateEmergencyCategories(categoriesText: string): void {
    const p = this.policy();
    if (!p) return;
    p.emergencyRules.emergencyCategories = categoriesText
      .split('\n')
      .map(c => c.trim())
      .filter(c => c.length > 0);
    this.policy.set({ ...p });
  }

  updateMinKeywordScore(score: number): void {
    const p = this.policy();
    if (!p) return;
    p.emergencyRules.minKeywordScore = Math.max(1, Math.min(10, score));
    this.policy.set({ ...p });
  }

  updateTimeZone(tz: string): void {
    const p = this.policy();
    if (!p) return;
    p.timeZoneId = tz;
    this.policy.set({ ...p });
  }

  // Save
  save(): void {
    const p = this.policy();
    if (!p || this.isSaving()) return;

    this.isSaving.set(true);

    this.api.updatePolicy(p).subscribe({
      next: (response) => {
        this.isSaving.set(false);
        if (response.success) {
          this.policy.set(response.policy);
          this.showToastMessage('Settings saved successfully', 'success');
        }
      },
      error: (err) => {
        this.isSaving.set(false);
        this.showToastMessage(err.error?.error || 'Failed to save settings', 'error');
      }
    });
  }

  // Helpers
  getKeywordsText(): string {
    return this.policy()?.emergencyRules.keywords.join('\n') || '';
  }

  getEmergencyCategoriesText(): string {
    return this.policy()?.emergencyRules.emergencyCategories.join('\n') || '';
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}

