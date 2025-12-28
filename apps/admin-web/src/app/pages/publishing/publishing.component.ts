import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { PublishApiService } from '../../services/publish-api.service';
import { AuthService } from '../../services/auth.service';
import { 
  PublishJob, 
  PublishJobQueryParams, 
  ChannelPublishLog,
  JobStatusOptions 
} from '../../shared/publish.models';

@Component({
  selector: 'app-publishing',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './publishing.component.html',
  styleUrl: './publishing.component.scss'
})
export class PublishingComponent implements OnInit {
  private publishApi = inject(PublishApiService);
  private authService = inject(AuthService);

  // Data
  jobs = signal<PublishJob[]>([]);
  logs = signal<ChannelPublishLog[]>([]);
  total = signal(0);

  // Filters
  statusFilter = '';
  fromUtc = '';
  toUtc = '';

  // Pagination
  page = signal(1);
  pageSize = signal(20);

  // UI State
  isLoading = signal(false);
  isRunningDue = signal(false);
  showLogsModal = signal(false);
  selectedJobContentId = signal<string | null>(null);
  logsLoading = signal(false);

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  // Options
  statusOptions = JobStatusOptions;

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize());
  }

  get isAdmin(): boolean {
    return this.authService.hasRole('Admin');
  }

  ngOnInit(): void {
    this.loadJobs();
  }

  loadJobs(): void {
    this.isLoading.set(true);

    const params: PublishJobQueryParams = {
      page: this.page(),
      pageSize: this.pageSize()
    };

    if (this.statusFilter) params.status = this.statusFilter;
    if (this.fromUtc) params.fromUtc = new Date(this.fromUtc).toISOString();
    if (this.toUtc) params.toUtc = new Date(this.toUtc).toISOString();

    this.publishApi.listJobs(params).subscribe({
      next: (response) => {
        this.jobs.set(response.items);
        this.total.set(response.total);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load jobs', err);
        this.isLoading.set(false);
        this.showToastMessage('Failed to load jobs', 'error');
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.loadJobs();
  }

  onClearFilters(): void {
    this.statusFilter = '';
    this.fromUtc = '';
    this.toUtc = '';
    this.page.set(1);
    this.loadJobs();
  }

  onPageChange(newPage: number): void {
    if (newPage < 1 || newPage > this.totalPages) return;
    this.page.set(newPage);
    this.loadJobs();
  }

  runDueJobs(): void {
    if (this.isRunningDue()) return;

    this.isRunningDue.set(true);
    this.publishApi.runDue().subscribe({
      next: (response) => {
        this.isRunningDue.set(false);
        this.showToastMessage(`Processed ${response.jobsProcessed} jobs`, 'success');
        this.loadJobs();
      },
      error: (err) => {
        this.isRunningDue.set(false);
        this.showToastMessage('Failed to run due jobs', 'error');
      }
    });
  }

  viewLogs(job: PublishJob): void {
    this.selectedJobContentId.set(job.contentItemId);
    this.logs.set([]);
    this.showLogsModal.set(true);
    this.logsLoading.set(true);

    this.publishApi.getLogs(job.contentItemId).subscribe({
      next: (logs) => {
        this.logs.set(logs);
        this.logsLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load logs', err);
        this.logsLoading.set(false);
      }
    });
  }

  closeLogsModal(): void {
    this.showLogsModal.set(false);
    this.selectedJobContentId.set(null);
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString();
  }

  truncate(text: string | null, length: number): string {
    if (!text) return '-';
    return text.length > length ? text.substring(0, length) + '...' : text;
  }

  getStatusClass(status: string): string {
    return status?.toLowerCase() || '';
  }

  getChannelIcon(channel: string): string {
    switch (channel) {
      case 'Web': return '🌐';
      case 'Mobile': return '📱';
      case 'X': return '𝕏';
      default: return '📤';
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

