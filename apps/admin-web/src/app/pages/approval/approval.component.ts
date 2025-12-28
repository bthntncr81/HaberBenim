import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EditorialApiService } from '../../services/editorial-api.service';
import { FeedApiService } from '../../services/feed-api.service';
import { PublishApiService } from '../../services/publish-api.service';
import { AuthService } from '../../services/auth.service';
import { 
  EditorialInboxItem, 
  EditorialInboxParams, 
  EditorialStatusOptions 
} from '../../shared/editorial.models';
import { Source } from '../../shared/feed.models';

@Component({
  selector: 'app-approval',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './approval.component.html',
  styleUrl: './approval.component.scss'
})
export class ApprovalComponent implements OnInit {
  private editorialApi = inject(EditorialApiService);
  private feedApi = inject(FeedApiService);
  private publishApi = inject(PublishApiService);
  private authService = inject(AuthService);
  private router = inject(Router);

  // Data
  items = signal<EditorialInboxItem[]>([]);
  sources = signal<Source[]>([]);
  total = signal(0);

  // Filters
  status = 'PendingApproval';
  fromUtc = '';
  toUtc = '';
  sourceId = '';
  keyword = '';

  // Pagination
  page = signal(1);
  pageSize = signal(20);

  // UI State
  isLoading = signal(false);
  publishingItemId = signal<string | null>(null);

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  // Options
  statusOptions = EditorialStatusOptions;

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize());
  }

  get canPublish(): boolean {
    return this.authService.hasAnyRole(['Admin', 'Editor']);
  }

  ngOnInit(): void {
    this.loadSources();
    this.loadInbox();
  }

  loadSources(): void {
    this.feedApi.getSources().subscribe({
      next: (sources) => this.sources.set(sources),
      error: (err) => console.error('Failed to load sources', err)
    });
  }

  loadInbox(): void {
    this.isLoading.set(true);

    const params: EditorialInboxParams = {
      status: this.status,
      page: this.page(),
      pageSize: this.pageSize()
    };

    if (this.fromUtc) params.fromUtc = new Date(this.fromUtc).toISOString();
    if (this.toUtc) params.toUtc = new Date(this.toUtc).toISOString();
    if (this.sourceId) params.sourceId = this.sourceId;
    if (this.keyword) params.keyword = this.keyword;

    this.editorialApi.getInbox(params).subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.total.set(response.total);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load inbox', err);
        this.isLoading.set(false);
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.loadInbox();
  }

  onClearFilters(): void {
    this.status = 'PendingApproval';
    this.fromUtc = '';
    this.toUtc = '';
    this.sourceId = '';
    this.keyword = '';
    this.page.set(1);
    this.loadInbox();
  }

  onPageChange(newPage: number): void {
    if (newPage < 1 || newPage > this.totalPages) return;
    this.page.set(newPage);
    this.loadInbox();
  }

  openEditor(item: EditorialInboxItem): void {
    this.router.navigate(['/editor', item.id]);
  }

  publishNow(item: EditorialInboxItem, event: Event): void {
    event.stopPropagation();
    
    if (this.publishingItemId()) return;

    this.publishingItemId.set(item.id);

    this.publishApi.enqueue(item.id).subscribe({
      next: (response) => {
        this.publishingItemId.set(null);
        if (response.alreadyQueued) {
          this.showToastMessage('Already queued for publishing', 'success');
        } else {
          this.showToastMessage('Queued for publishing!', 'success');
        }
        this.loadInbox();
      },
      error: (err) => {
        this.publishingItemId.set(null);
        this.showToastMessage('Failed to enqueue', 'error');
      }
    });
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString();
  }

  truncate(text: string | null, length: number): string {
    if (!text) return '';
    return text.length > length ? text.substring(0, length) + '...' : text;
  }

  getStatusClass(status: string): string {
    return status?.toLowerCase().replace(/\s+/g, '') || '';
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
