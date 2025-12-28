import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { BreakingApiService } from '../../services/breaking-api.service';
import { AuthService } from '../../services/auth.service';
import { BreakingInboxItem } from '../../shared/breaking.models';

@Component({
  selector: 'app-breaking',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './breaking.component.html',
  styleUrl: './breaking.component.scss'
})
export class BreakingComponent implements OnInit {
  private api = inject(BreakingApiService);
  private authService = inject(AuthService);
  private router = inject(Router);

  // Data
  items = signal<BreakingInboxItem[]>([]);
  
  // Filters
  statusFilter = '';
  
  // Pagination
  currentPage = signal(1);
  pageSize = signal(20);
  totalItems = signal(0);

  // UI State
  isLoading = signal(false);
  error = signal<string | null>(null);

  // Mark Breaking Modal
  showMarkModal = signal(false);
  markContentId = '';
  markNote = '';
  markPriority = 100;
  markPushRequired = true;
  isMarking = signal(false);

  // Publish Now
  publishingId = signal<string | null>(null);

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  get canManageBreaking(): boolean {
    return this.authService.hasAnyRole(['Admin', 'Editor']);
  }

  ngOnInit(): void {
    this.loadItems();
  }

  loadItems(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.api.inbox({
      status: this.statusFilter || undefined,
      page: this.currentPage(),
      pageSize: this.pageSize()
    }).subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.totalItems.set(response.total);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load breaking news', err);
        this.error.set('Failed to load breaking news');
        this.isLoading.set(false);
      }
    });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadItems();
  }

  openEditor(id: string): void {
    this.router.navigate(['/editor', id]);
  }

  publishNow(item: BreakingInboxItem): void {
    if (this.publishingId()) return;

    this.publishingId.set(item.id);

    this.api.publishNow(item.id).subscribe({
      next: (response) => {
        this.publishingId.set(null);
        if (response.ok) {
          this.showToastMessage(`Publish job ${response.jobId} created`, 'success');
          this.loadItems();
        } else {
          this.showToastMessage(response.error || 'Failed to publish', 'error');
        }
      },
      error: (err) => {
        this.publishingId.set(null);
        this.showToastMessage('Failed to create publish job', 'error');
      }
    });
  }

  // Mark Breaking Modal
  openMarkModal(): void {
    this.markContentId = '';
    this.markNote = '';
    this.markPriority = 100;
    this.markPushRequired = true;
    this.showMarkModal.set(true);
  }

  closeMarkModal(): void {
    this.showMarkModal.set(false);
  }

  submitMark(): void {
    if (!this.markContentId.trim() || this.isMarking()) return;

    this.isMarking.set(true);

    this.api.mark(this.markContentId, {
      note: this.markNote || undefined,
      priority: this.markPriority,
      pushRequired: this.markPushRequired
    }).subscribe({
      next: (response) => {
        this.isMarking.set(false);
        if (response.ok) {
          this.showToastMessage(
            `Marked as breaking (v${response.versionNo}). Job: ${response.jobId}`,
            'success'
          );
          this.closeMarkModal();
          this.loadItems();
        } else {
          this.showToastMessage(response.error || 'Failed to mark as breaking', 'error');
        }
      },
      error: (err) => {
        this.isMarking.set(false);
        const errorMsg = err.error?.error || 'Failed to mark as breaking';
        this.showToastMessage(errorMsg, 'error');
      }
    });
  }

  // Pagination
  nextPage(): void {
    if (this.currentPage() * this.pageSize() < this.totalItems()) {
      this.currentPage.update(p => p + 1);
      this.loadItems();
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
      this.loadItems();
    }
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString();
  }

  getStatusClass(status: string): string {
    return status?.toLowerCase().replace(/\s+/g, '') || '';
  }

  getPriorityClass(priority: number): string {
    if (priority >= 200) return 'high';
    if (priority >= 100) return 'normal';
    return 'low';
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}

