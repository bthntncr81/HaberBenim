import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TemplateApiService, TemplateDto, CreateTemplateRequest } from '../../services/template-api.service';

@Component({
  selector: 'app-templates',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './templates.component.html',
  styleUrls: ['./templates.component.scss']
})
export class TemplatesComponent implements OnInit {
  private api = inject(TemplateApiService);
  private router = inject(Router);

  templates = signal<TemplateDto[]>([]);
  total = signal(0);
  loading = signal(false);
  error = signal<string | null>(null);

  // Options
  platforms = signal<string[]>([]);
  formats = signal<string[]>([]);

  // Filters
  filterPlatform = signal('');
  filterFormat = signal('');
  filterActive = signal<string>('');
  filterSearch = signal('');
  currentPage = signal(1);
  pageSize = 20;

  // Create modal
  showCreateModal = signal(false);
  newTemplate = signal<CreateTemplateRequest>({
    name: '',
    platform: 'Instagram',
    format: 'Post',
    priority: 100,
    isActive: true
  });
  creating = signal(false);

  ngOnInit() {
    this.loadOptions();
    this.loadTemplates();
  }

  loadOptions() {
    this.api.getOptions().subscribe({
      next: (res) => {
        this.platforms.set(res.platforms);
        this.formats.set(res.formats);
      }
    });
  }

  loadTemplates() {
    this.loading.set(true);
    this.error.set(null);

    const params: any = {
      page: this.currentPage(),
      pageSize: this.pageSize
    };

    if (this.filterPlatform()) params.platform = this.filterPlatform();
    if (this.filterFormat()) params.format = this.filterFormat();
    if (this.filterActive() !== '') params.active = this.filterActive() === 'true';
    if (this.filterSearch()) params.q = this.filterSearch();

    this.api.list(params).subscribe({
      next: (res) => {
        this.templates.set(res.items);
        this.total.set(res.total);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(err.error?.error || 'Failed to load templates');
        this.loading.set(false);
      }
    });
  }

  applyFilters() {
    this.currentPage.set(1);
    this.loadTemplates();
  }

  clearFilters() {
    this.filterPlatform.set('');
    this.filterFormat.set('');
    this.filterActive.set('');
    this.filterSearch.set('');
    this.currentPage.set(1);
    this.loadTemplates();
  }

  openDesigner(template: TemplateDto) {
    this.router.navigate(['/templates', template.id, 'designer']);
  }

  toggleActive(template: TemplateDto) {
    this.api.update(template.id, { isActive: !template.isActive }).subscribe({
      next: () => {
        this.loadTemplates();
      },
      error: (err) => {
        this.error.set(err.error?.error || 'Failed to update template');
      }
    });
  }

  deleteTemplate(template: TemplateDto) {
    if (!confirm(`"${template.name}" şablonunu silmek istediğinize emin misiniz?`)) {
      return;
    }

    this.api.delete(template.id).subscribe({
      next: () => {
        this.loadTemplates();
      },
      error: (err) => {
        this.error.set(err.error?.error || 'Failed to delete template');
      }
    });
  }

  // Create modal
  openCreateModal() {
    this.newTemplate.set({
      name: '',
      platform: this.platforms()[0] || 'Instagram',
      format: this.formats()[0] || 'Post',
      priority: 100,
      isActive: true
    });
    this.showCreateModal.set(true);
  }

  closeCreateModal() {
    this.showCreateModal.set(false);
  }

  createTemplate() {
    const data = this.newTemplate();
    if (!data.name.trim()) {
      this.error.set('Şablon adı gerekli');
      return;
    }

    this.creating.set(true);
    this.api.create(data).subscribe({
      next: (template) => {
        this.creating.set(false);
        this.showCreateModal.set(false);
        // Go to designer
        this.router.navigate(['/templates', template.id, 'designer']);
      },
      error: (err) => {
        this.creating.set(false);
        this.error.set(err.error?.error || 'Failed to create template');
      }
    });
  }

  updateNewTemplate(field: keyof CreateTemplateRequest, value: any) {
    this.newTemplate.update(t => ({ ...t, [field]: value }));
  }

  getPlatformIcon(platform: string): string {
    switch (platform) {
      case 'Instagram': return '📷';
      case 'X': return '𝕏';
      case 'TikTok': return '🎵';
      case 'YouTube': return '▶️';
      default: return '📄';
    }
  }

  getFormatBadgeClass(format: string): string {
    switch (format) {
      case 'Post': return 'badge-post';
      case 'Reels': return 'badge-reels';
      case 'Shorts': return 'badge-shorts';
      case 'Video': return 'badge-video';
      case 'Tweet': return 'badge-tweet';
      case 'QuoteTweet': return 'badge-quote';
      default: return '';
    }
  }
}

