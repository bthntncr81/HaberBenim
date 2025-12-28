import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AnalyticsApiService } from '../../services/analytics-api.service';
import { AnalyticsOverview, TopSource } from '../../shared/analytics.models';

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.scss'
})
export class AnalyticsComponent implements OnInit {
  private api = inject(AnalyticsApiService);

  // Data
  overview = signal<AnalyticsOverview | null>(null);

  // Date range
  fromDate = '';
  toDate = '';

  // UI State
  isLoading = signal(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    // Default to last 7 days
    const now = new Date();
    const weekAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
    
    this.fromDate = weekAgo.toISOString().split('T')[0];
    this.toDate = now.toISOString().split('T')[0];
    
    this.loadData();
  }

  loadData(): void {
    this.isLoading.set(true);
    this.error.set(null);

    const fromUtc = this.fromDate ? new Date(this.fromDate).toISOString() : undefined;
    const toUtc = this.toDate ? new Date(this.toDate + 'T23:59:59').toISOString() : undefined;

    this.api.getOverview({ fromUtc, toUtc }).subscribe({
      next: (data) => {
        this.overview.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load analytics', err);
        this.error.set('Failed to load analytics data');
        this.isLoading.set(false);
      }
    });
  }

  getChannelKeys(): string[] {
    const data = this.overview();
    return data ? Object.keys(data.byChannel) : [];
  }

  getOriginKeys(): string[] {
    const data = this.overview();
    return data ? Object.keys(data.autoVsEditorial).filter(k => k !== 'Unknown') : [];
  }

  getChannelIcon(channel: string): string {
    switch (channel) {
      case 'Web': return '🌐';
      case 'Mobile': return '📱';
      case 'X': return '𝕏';
      default: return '📤';
    }
  }
}

