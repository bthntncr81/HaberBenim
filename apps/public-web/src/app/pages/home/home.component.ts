import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PublicApiService } from '../../services/public-api.service';
import { PublicArticleListItem } from '../../shared/public.models';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements OnInit {
  private api = inject(PublicApiService);
  private router = inject(Router);

  // Data
  articles = signal<PublicArticleListItem[]>([]);
  total = signal(0);

  // Pagination
  page = signal(1);
  pageSize = signal(10);

  // Search
  searchQuery = '';

  // UI State
  isLoading = signal(false);
  error = signal<string | null>(null);

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize());
  }

  get hasNext(): boolean {
    return this.page() < this.totalPages;
  }

  get hasPrev(): boolean {
    return this.page() > 1;
  }

  ngOnInit(): void {
    this.loadArticles();
  }

  loadArticles(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.api.getLatest({
      q: this.searchQuery || undefined,
      page: this.page(),
      pageSize: this.pageSize()
    }).subscribe({
      next: (response) => {
        this.articles.set(response.items);
        this.total.set(response.total);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load articles', err);
        this.error.set('Failed to load articles. Please try again later.');
        this.isLoading.set(false);
      }
    });
  }

  onSearch(): void {
    this.page.set(1);
    this.loadArticles();
  }

  onClearSearch(): void {
    this.searchQuery = '';
    this.page.set(1);
    this.loadArticles();
  }

  onNext(): void {
    if (this.hasNext) {
      this.page.set(this.page() + 1);
      this.loadArticles();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  onPrev(): void {
    if (this.hasPrev) {
      this.page.set(this.page() - 1);
      this.loadArticles();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  openArticle(article: PublicArticleListItem): void {
    this.router.navigate(['/news', article.id]);
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }
}
