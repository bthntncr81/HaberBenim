import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FeedApiService } from '../../services/feed-api.service';
import { RulesApiService } from '../../services/rules-api.service';
import { AuthService } from '../../services/auth.service';
import { FeedItem, FeedItemDetail, FeedQueryParams, Source, IngestionResult } from '../../shared/feed.models';
import { 
  DecisionTypeOptions, 
  ContentStatusOptions, 
  RecomputeRequest, 
  RecomputeResult 
} from '../../shared/rule.models';

@Component({
  selector: 'app-feed',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe],
  templateUrl: './feed.component.html',
  styleUrl: './feed.component.scss'
})
export class FeedComponent implements OnInit {
  private feedApi = inject(FeedApiService);
  private rulesApi = inject(RulesApiService);
  private authService = inject(AuthService);

  // Data
  items = signal<FeedItem[]>([]);
  sources = signal<Source[]>([]);
  total = signal(0);
  
  // Filters
  fromUtc = '';
  toUtc = '';
  sourceId = '';
  keyword = '';
  status = '';
  decisionType = '';
  
  // Pagination
  page = signal(1);
  pageSize = signal(20);
  
  // UI State
  isLoading = signal(false);
  isLoadingDetail = signal(false);
  isRunningIngestion = signal(false);
  showDetailModal = signal(false);
  selectedItem = signal<FeedItemDetail | null>(null);
  
  // Recompute modal
  showRecomputeModal = signal(false);
  isRecomputing = signal(false);
  recomputeForm = {
    fromUtc: '',
    toUtc: '',
    sourceId: '',
    status: ''
  };
  recomputeResult = signal<RecomputeResult | null>(null);
  
  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  // Options
  decisionTypeOptions = DecisionTypeOptions;
  statusOptions = ContentStatusOptions;

  get isAdmin(): boolean {
    return this.authService.hasRole('Admin');
  }

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize());
  }

  ngOnInit(): void {
    this.loadSources();
    this.loadFeed();
  }

  loadSources(): void {
    this.feedApi.getSources().subscribe({
      next: (sources) => this.sources.set(sources),
      error: (err) => console.error('Failed to load sources', err)
    });
  }

  loadFeed(): void {
    this.isLoading.set(true);

    const params: FeedQueryParams = {
      page: this.page(),
      pageSize: this.pageSize()
    };

    if (this.fromUtc) params.fromUtc = new Date(this.fromUtc).toISOString();
    if (this.toUtc) params.toUtc = new Date(this.toUtc).toISOString();
    if (this.sourceId) params.sourceId = this.sourceId;
    if (this.keyword) params.keyword = this.keyword;
    if (this.status) params.status = this.status;
    if (this.decisionType) params.decisionType = this.decisionType;

    this.feedApi.getFeed(params).subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.total.set(response.total);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load feed', err);
        this.isLoading.set(false);
        this.showToastMessage('Failed to load feed', 'error');
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.loadFeed();
  }

  onClearFilters(): void {
    this.fromUtc = '';
    this.toUtc = '';
    this.sourceId = '';
    this.keyword = '';
    this.status = '';
    this.decisionType = '';
    this.page.set(1);
    this.loadFeed();
  }

  onPageChange(newPage: number): void {
    if (newPage < 1 || newPage > this.totalPages) return;
    this.page.set(newPage);
    this.loadFeed();
  }

  viewItem(item: FeedItem): void {
    this.isLoadingDetail.set(true);
    this.showDetailModal.set(true);

    this.feedApi.getFeedItem(item.id).subscribe({
      next: (detail) => {
        this.selectedItem.set(detail);
        this.isLoadingDetail.set(false);
      },
      error: (err) => {
        console.error('Failed to load item detail', err);
        this.isLoadingDetail.set(false);
        this.showToastMessage('Failed to load item details', 'error');
      }
    });
  }

  closeModal(): void {
    this.showDetailModal.set(false);
    this.selectedItem.set(null);
  }

  runRssIngestion(): void {
    if (this.isRunningIngestion()) return;

    this.isRunningIngestion.set(true);

    this.feedApi.runRssIngestion().subscribe({
      next: (result) => {
        this.isRunningIngestion.set(false);
        let message = `Ingestion complete: ${result.itemsInserted} items inserted, ${result.duplicates} duplicates`;
        if (result.byDecisionTypeCounts) {
          const decisions = Object.entries(result.byDecisionTypeCounts)
            .map(([k, v]) => `${k}: ${v}`)
            .join(', ');
          message += ` (${decisions})`;
        }
        this.showToastMessage(message, 'success');
        this.loadFeed();
      },
      error: (err) => {
        this.isRunningIngestion.set(false);
        this.showToastMessage('Ingestion failed: ' + (err.error?.error || err.message), 'error');
      }
    });
  }

  // Recompute modal
  openRecomputeModal(): void {
    this.recomputeForm = {
      fromUtc: '',
      toUtc: '',
      sourceId: '',
      status: ''
    };
    this.recomputeResult.set(null);
    this.showRecomputeModal.set(true);
  }

  closeRecomputeModal(): void {
    this.showRecomputeModal.set(false);
    this.recomputeResult.set(null);
  }

  runRecompute(): void {
    if (this.isRecomputing()) return;

    this.isRecomputing.set(true);
    this.recomputeResult.set(null);

    const payload: RecomputeRequest = {};
    if (this.recomputeForm.fromUtc) payload.fromUtc = new Date(this.recomputeForm.fromUtc).toISOString();
    if (this.recomputeForm.toUtc) payload.toUtc = new Date(this.recomputeForm.toUtc).toISOString();
    if (this.recomputeForm.sourceId) payload.sourceId = this.recomputeForm.sourceId;
    if (this.recomputeForm.status) payload.status = this.recomputeForm.status;

    this.rulesApi.recompute(payload).subscribe({
      next: (result) => {
        this.isRecomputing.set(false);
        this.recomputeResult.set(result);
        this.showToastMessage(`Recompute complete: ${result.processed} processed, ${result.changed} changed`, 'success');
      },
      error: (err) => {
        this.isRecomputing.set(false);
        this.showToastMessage('Recompute failed: ' + (err.error?.error || err.message), 'error');
      }
    });
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return date.toLocaleString();
  }

  truncate(text: string | null, length: number): string {
    if (!text) return '';
    return text.length > length ? text.substring(0, length) + '...' : text;
  }

  getPageNumbers(): number[] {
    const total = this.totalPages;
    const current = this.page();
    const pages: number[] = [];

    if (total <= 7) {
      for (let i = 1; i <= total; i++) pages.push(i);
      return pages;
    }

    // Always show first page
    pages.push(1);

    if (current > 3) {
      pages.push(-1); // ellipsis
    }

    // Pages around current
    for (let i = Math.max(2, current - 1); i <= Math.min(total - 1, current + 1); i++) {
      pages.push(i);
    }

    if (current < total - 2) {
      pages.push(-1); // ellipsis
    }

    // Always show last page
    if (total > 1) pages.push(total);

    return pages;
  }

  getStatusClass(status: string): string {
    return status?.toLowerCase().replace(/\s+/g, '') || '';
  }

  getDecisionClass(decisionType: string | null): string {
    return decisionType?.toLowerCase() || '';
  }

  getDecisionLabel(decisionType: string | null): string {
    if (!decisionType) return '-';
    const opt = this.decisionTypeOptions.find(o => o.value === decisionType);
    return opt?.label || decisionType;
  }

  getRecomputeResultEntries(): { key: string; value: number }[] {
    const result = this.recomputeResult();
    if (!result?.byDecisionTypeCounts) return [];
    return Object.entries(result.byDecisionTypeCounts).map(([key, value]) => ({ key, value }));
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}
