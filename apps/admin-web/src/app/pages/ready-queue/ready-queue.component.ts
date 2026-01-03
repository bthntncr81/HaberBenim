import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { 
  ReadyQueueApiService, 
  ReadyQueueItemDto, 
  RenderJobDto,
  QueueSettingsDto 
} from '../../services/ready-queue-api.service';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-ready-queue',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ready-queue.component.html',
  styleUrl: './ready-queue.component.scss'
})
export class ReadyQueueComponent implements OnInit {
  private readyQueueApi = inject(ReadyQueueApiService);
  private authService = inject(AuthService);
  private router = inject(Router);

  // Role check
  isAdmin = computed(() => this.authService.hasRole('Admin'));

  // Data
  items = signal<ReadyQueueItemDto[]>([]);
  total = signal(0);
  settings = signal<QueueSettingsDto | null>(null);

  // Filters
  platformFilter = '';
  platforms = ['Instagram', 'X', 'TikTok', 'YouTube'];

  // Pagination
  page = signal(1);
  pageSize = signal(20);

  // UI State
  isLoading = signal(false);
  error = signal<string | null>(null);
  publishingId = signal<string | null>(null);
  updatingSettings = signal(false);

  // Detail modal
  showDetailModal = signal(false);
  selectedItem = signal<ReadyQueueItemDto | null>(null);

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize());
  }

  ngOnInit(): void {
    this.loadItems();
    this.loadSettings();
  }

  loadItems(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.readyQueueApi.getList({
      platform: this.platformFilter || undefined,
      page: this.page(),
      pageSize: this.pageSize()
    }).subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.total.set(response.total);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load ready queue', err);
        this.error.set('Failed to load ready queue');
        this.isLoading.set(false);
      }
    });
  }

  loadSettings(): void {
    this.readyQueueApi.getSettings().subscribe({
      next: (settings) => {
        this.settings.set(settings);
      },
      error: (err) => {
        console.error('Failed to load settings', err);
      }
    });
  }

  onFilterChange(): void {
    this.page.set(1);
    this.loadItems();
  }

  onPageChange(newPage: number): void {
    if (newPage < 1 || newPage > this.totalPages) return;
    this.page.set(newPage);
    this.loadItems();
  }

  openDetail(item: ReadyQueueItemDto): void {
    this.selectedItem.set(item);
    this.showDetailModal.set(true);
  }

  closeDetailModal(): void {
    this.showDetailModal.set(false);
    this.selectedItem.set(null);
  }

  goToEditor(contentId: string): void {
    this.router.navigate(['/editor', contentId]);
  }

  publish(item: ReadyQueueItemDto): void {
    if (this.publishingId()) return;

    this.publishingId.set(item.id);

    this.readyQueueApi.publish(item.id).subscribe({
      next: (response) => {
        this.publishingId.set(null);
        if (response.success) {
          this.showToastMessage(
            response.alreadyQueued 
              ? 'Already queued for publishing' 
              : `Publishing queued for ${response.platforms?.join(', ') || 'all platforms'}`,
            'success'
          );
          this.loadItems();
        } else {
          this.showToastMessage(response.message || 'Failed to publish', 'error');
        }
      },
      error: (err) => {
        this.publishingId.set(null);
        this.showToastMessage(err.error?.error || 'Failed to publish', 'error');
      }
    });
  }

  updatePublishMode(mode: string): void {
    if (this.updatingSettings()) return;

    this.updatingSettings.set(true);
    this.readyQueueApi.updateSettings(mode).subscribe({
      next: (response) => {
        this.updatingSettings.set(false);
        if (response.success) {
          this.loadSettings();
          this.showToastMessage(`Publish mode updated to ${mode}`, 'success');
        }
      },
      error: (err) => {
        this.updatingSettings.set(false);
        this.showToastMessage('Failed to update settings', 'error');
      }
    });
  }

  getPlatformIcon(platform: string): string {
    switch (platform) {
      case 'Instagram': return '📷';
      case 'X': return '𝕏';
      case 'TikTok': return '🎵';
      case 'YouTube': return '▶️';
      default: return '📄';
    }
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Completed': return 'status-completed';
      case 'Rendering': return 'status-rendering';
      case 'Queued': return 'status-queued';
      case 'Failed': return 'status-failed';
      default: return '';
    }
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case 'Completed': return 'Ready';
      case 'Rendering': return 'Rendering...';
      case 'Queued': return 'In Queue';
      case 'Failed': return 'Failed';
      default: return status;
    }
  }

  hasCompletedRenders(item: ReadyQueueItemDto): boolean {
    return item.renderJobs.some(j => j.status === 'Completed');
  }

  getCompletedRenders(item: ReadyQueueItemDto): RenderJobDto[] {
    return item.renderJobs.filter(j => j.status === 'Completed');
  }

  getFirstRenderImage(item: ReadyQueueItemDto): string | null {
    const completed = item.renderJobs.find(j => j.status === 'Completed' && j.outputUrl);
    return completed?.outputUrl || null;
  }

  getFirstRenderOutput(item: ReadyQueueItemDto): { url: string; isVideo: boolean } | null {
    const completed = item.renderJobs.find(j => j.status === 'Completed' && j.outputUrl);
    if (!completed?.outputUrl) return null;
    return {
      url: completed.outputUrl,
      isVideo: completed.outputType === 'Video'
    };
  }

  isVideoRender(job: RenderJobDto): boolean {
    return job.outputType === 'Video';
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString();
  }

  formatRelativeTime(dateStr: string | null): string {
    if (!dateStr) return 'Never';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;

    const diffHours = Math.floor(diffMins / 60);
    if (diffHours < 24) return `${diffHours}h ago`;

    const diffDays = Math.floor(diffHours / 24);
    return `${diffDays}d ago`;
  }

  parseResolvedText(json: string | null): Record<string, string> {
    if (!json) return {};
    try {
      return JSON.parse(json);
    } catch {
      return {};
    }
  }

  getPageNumbers(): number[] {
    const total = this.totalPages;
    const current = this.page();
    const pages: number[] = [];

    if (total <= 7) {
      for (let i = 1; i <= total; i++) pages.push(i);
      return pages;
    }

    pages.push(1);
    if (current > 3) pages.push(-1);
    for (let i = Math.max(2, current - 1); i <= Math.min(total - 1, current + 1); i++) {
      pages.push(i);
    }
    if (current < total - 2) pages.push(-1);
    if (total > 1) pages.push(total);

    return pages;
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}

