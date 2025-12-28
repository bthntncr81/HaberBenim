import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { PublicApiService } from '../../services/public-api.service';
import { PublicArticle } from '../../shared/public.models';

@Component({
  selector: 'app-news-detail',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './news-detail.component.html',
  styleUrl: './news-detail.component.scss'
})
export class NewsDetailComponent implements OnInit {
  private api = inject(PublicApiService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  article = signal<PublicArticle | null>(null);
  isLoading = signal(true);
  error = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.loadArticle(id);
    } else {
      this.error.set('Article ID not provided');
      this.isLoading.set(false);
    }
  }

  loadArticle(id: string): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.api.getById(id).subscribe({
      next: (article) => {
        this.article.set(article);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load article', err);
        if (err.status === 404) {
          this.error.set('Article not found');
        } else {
          this.error.set('Failed to load article. Please try again.');
        }
        this.isLoading.set(false);
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/']);
  }

  formatDate(dateStr: string): string {
    const date = new Date(dateStr);
    return date.toLocaleDateString('en-US', {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatBody(body: string): string[] {
    // Split by double newlines or paragraph tags
    return body
      .replace(/<br\s*\/?>/gi, '\n')
      .replace(/<\/p>/gi, '\n\n')
      .replace(/<[^>]+>/g, '')
      .split(/\n\n+/)
      .filter(p => p.trim().length > 0);
  }
}

