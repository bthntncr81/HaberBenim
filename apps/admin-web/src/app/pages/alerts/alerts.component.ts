import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AlertsApiService } from '../../services/alerts-api.service';
import { AuthService } from '../../services/auth.service';
import { AdminAlert, AlertSeverities, AlertTypes } from '../../shared/alerts.models';

@Component({
  selector: 'app-alerts',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './alerts.component.html',
  styleUrl: './alerts.component.scss'
})
export class AlertsComponent implements OnInit {
  private api = inject(AlertsApiService);
  private authService = inject(AuthService);

  // Data
  alerts = signal<AdminAlert[]>([]);

  // Filter options
  severityOptions = AlertSeverities;
  typeOptions = AlertTypes;

  // Filters
  severityFilter = '';
  typeFilter = '';
  acknowledgedFilter: boolean | null = null;
  fromDate = '';
  toDate = '';

  // Pagination
  currentPage = signal(1);
  pageSize = signal(20);
  totalItems = signal(0);

  // UI State
  isLoading = signal(false);
  error = signal<string | null>(null);
  ackingId = signal<string | null>(null);

  // Detail Modal
  showDetailModal = signal(false);
  selectedAlert = signal<AdminAlert | null>(null);

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  get isAdmin(): boolean {
    return this.authService.hasAnyRole(['Admin']);
  }

  ngOnInit(): void {
    // Default to last 7 days
    const now = new Date();
    const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
    this.fromDate = weekAgo.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];

    this.loadAlerts();
  }

  loadAlerts(): void {
    this.isLoading.set(true);
    this.error.set(null);

    const fromUtc = this.fromDate ? new Date(this.fromDate).toISOString() : undefined;
    const toUtc = this.toDate ? new Date(this.toDate + 'T23:59:59').toISOString() : undefined;

    this.api.list({
      severity: this.severityFilter || undefined,
      type: this.typeFilter || undefined,
      acknowledged: this.acknowledgedFilter ?? undefined,
      fromUtc,
      toUtc,
      page: this.currentPage(),
      pageSize: this.pageSize()
    }).subscribe({
      next: (response) => {
        this.alerts.set(response.items);
        this.totalItems.set(response.total);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load alerts', err);
        this.error.set('Failed to load alerts');
        this.isLoading.set(false);
      }
    });
  }

  applyFilters(): void {
    this.currentPage.set(1);
    this.loadAlerts();
  }

  acknowledgeAlert(alert: AdminAlert): void {
    if (!this.isAdmin || this.ackingId()) return;

    this.ackingId.set(alert.id);

    this.api.ack(alert.id).subscribe({
      next: (response) => {
        this.ackingId.set(null);
        if (response.ok) {
          this.showToastMessage('Alert acknowledged', 'success');
          this.loadAlerts();
        } else {
          this.showToastMessage(response.error || 'Failed to acknowledge', 'error');
        }
      },
      error: (err) => {
        this.ackingId.set(null);
        this.showToastMessage('Failed to acknowledge alert', 'error');
      }
    });
  }

  openDetail(alert: AdminAlert): void {
    this.selectedAlert.set(alert);
    this.showDetailModal.set(true);
  }

  closeDetailModal(): void {
    this.showDetailModal.set(false);
  }

  // Pagination
  nextPage(): void {
    if (this.currentPage() * this.pageSize() < this.totalItems()) {
      this.currentPage.update(p => p + 1);
      this.loadAlerts();
    }
  }

  prevPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(p => p - 1);
      this.loadAlerts();
    }
  }

  formatDate(dateStr: string): string {
    return new Date(dateStr).toLocaleString();
  }

  getSeverityClass(severity: string): string {
    return severity?.toLowerCase() || '';
  }

  getSeverityIcon(severity: string): string {
    switch (severity) {
      case 'Critical': return '🔴';
      case 'Warn': return '🟠';
      case 'Info': return '🔵';
      default: return '⚪';
    }
  }

  getTypeIcon(type: string): string {
    switch (type) {
      case 'IngestionDown': return '⚠️';
      case 'FailoverActivated': return '🔄';
      case 'ComplianceViolation': return '📋';
      case 'Retract': return '🚫';
      default: return '📢';
    }
  }

  parseMetaJson(metaJson: string | null): any {
    if (!metaJson) return null;
    try {
      return JSON.parse(metaJson);
    } catch {
      return null;
    }
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}

