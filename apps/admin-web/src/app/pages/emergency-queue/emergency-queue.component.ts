import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  PublishingPolicyApiService,
  EmergencyQueueItem,
  EmergencyQueueStats
} from '../../services/publishing-policy-api.service';

@Component({
  selector: 'app-emergency-queue',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './emergency-queue.component.html',
  styleUrl: './emergency-queue.component.scss'
})
export class EmergencyQueueComponent implements OnInit {
  private api = inject(PublishingPolicyApiService);
  private router = inject(Router);

  // Data
  items = signal<EmergencyQueueItem[]>([]);
  stats = signal<EmergencyQueueStats | null>(null);
  total = signal(0);

  // Filters
  statusFilter = 'Pending';
  statuses = ['Pending', 'Publishing', 'Published', 'Cancelled'];

  // Pagination
  page = signal(1);
  pageSize = signal(20);

  // UI State
  isLoading = signal(true);
  error = signal<string | null>(null);
  publishingId = signal<string | null>(null);
  cancellingId = signal<string | null>(null);

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize());
  }

  ngOnInit(): void {
    this.loadItems();
    this.loadStats();
  }

  loadItems(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.api.getEmergencyQueue({
      status: this.statusFilter,
      page: this.page(),
      pageSize: this.pageSize()
    }).subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.total.set(response.total);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load emergency queue', err);
        this.error.set('Failed to load emergency queue');
        this.isLoading.set(false);
      }
    });
  }

  loadStats(): void {
    this.api.getEmergencyStats().subscribe({
      next: (stats) => this.stats.set(stats),
      error: (err) => console.error('Failed to load stats', err)
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

  publish(item: EmergencyQueueItem): void {
    if (this.publishingId()) return;

    this.publishingId.set(item.id);

    this.api.publishEmergency(item.id).subscribe({
      next: (response) => {
        this.publishingId.set(null);
        if (response.success) {
          this.showToastMessage('Emergency content published!', 'success');
          this.loadItems();
          this.loadStats();
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

  cancel(item: EmergencyQueueItem): void {
    if (this.cancellingId()) return;

    if (!confirm('Are you sure you want to cancel this emergency item?')) {
      return;
    }

    this.cancellingId.set(item.id);

    this.api.cancelEmergency(item.id).subscribe({
      next: (response) => {
        this.cancellingId.set(null);
        if (response.success) {
          this.showToastMessage('Emergency item cancelled', 'success');
          this.loadItems();
          this.loadStats();
        }
      },
      error: (err) => {
        this.cancellingId.set(null);
        this.showToastMessage(err.error?.error || 'Failed to cancel', 'error');
      }
    });
  }

  goToEditor(contentId: string): void {
    this.router.navigate(['/editor', contentId]);
  }

  getPriorityClass(priority: number): string {
    if (priority >= 80) return 'priority-critical';
    if (priority >= 50) return 'priority-high';
    if (priority >= 30) return 'priority-medium';
    return 'priority-low';
  }

  getPriorityLabel(priority: number): string {
    if (priority >= 80) return 'CRITICAL';
    if (priority >= 50) return 'HIGH';
    if (priority >= 30) return 'MEDIUM';
    return 'LOW';
  }

  getStatusClass(status: string): string {
    switch (status) {
      case 'Pending': return 'status-pending';
      case 'Publishing': return 'status-publishing';
      case 'Published': return 'status-published';
      case 'Cancelled': return 'status-cancelled';
      default: return '';
    }
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

