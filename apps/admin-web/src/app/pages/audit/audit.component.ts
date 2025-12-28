import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AuditApiService } from '../../services/audit-api.service';
import { AuditLog } from '../../shared/audit.models';

@Component({
  selector: 'app-audit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './audit.component.html',
  styleUrl: './audit.component.scss'
})
export class AuditComponent implements OnInit {
  private api = inject(AuditApiService);

  // Data
  logs = signal<AuditLog[]>([]);

  // Filters
  fromDate = '';
  toDate = '';
  userEmail = '';
  pathFilter = '';
  statusCodeFilter: number | null = null;

  // Pagination
  currentPage = signal(1);
  pageSize = signal(50);
  totalItems = signal(0);

  // UI State
  isLoading = signal(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    // Default to last 24 hours
    const now = new Date();
    const yesterday = new Date(now.getTime() - 24 * 60 * 60 * 1000);
    this.fromDate = yesterday.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];

    this.loadLogs();
  }

  loadLogs(): void {
    this.isLoading.set(true);
    this.error.set(null);

    const fromUtc = this.fromDate ? new Date(this.fromDate).toISOString() : undefined;
    const toUtc = this.toDate ? new Date(this.toDate + 'T23:59:59').toISOString() : undefined;

    this.api.list({
      fromUtc,
      toUtc,
      userEmail: this.userEmail || undefined,
      path: this.pathFilter || undefined,
      statusCode: this.statusCodeFilter || undefined,
      page: this.currentPage(),
      pageSize: this.pageSize()
    }).subscribe({
      next: (response) => {
        this.logs.set(response.items);
        this.totalItems.set(response.total);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load audit logs', err);
        this.error.set('Failed to load audit logs');
        this.isLoading.set(false);
      }
    });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadLogs();
  }

  clearFilters(): void {
    const now = new Date();
    const yesterday = new Date(now.getTime() - 24 * 60 * 60 * 1000);
    this.fromDate = yesterday.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];
    this.userEmail = '';
    this.pathFilter = '';
    this.statusCodeFilter = null;
    this.currentPage.set(1);
    this.loadLogs();
  }

  // Pagination
  nextPage(): void {
    if (this.currentPage() * this.pageSize() < this.totalItems()) {
      this.currentPage.update(p => p + 1);
      this.loadLogs();
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
      this.loadLogs();
    }
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString();
  }

  getMethodClass(method: string): string {
    return method?.toLowerCase() || '';
  }

  getStatusClass(status: number): string {
    if (status >= 500) return 'error';
    if (status >= 400) return 'warning';
    if (status >= 200 && status < 300) return 'success';
    return '';
  }

  getDurationClass(ms: number): string {
    if (ms > 2000) return 'slow';
    if (ms > 500) return 'medium';
    return 'fast';
  }
}

