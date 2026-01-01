import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../services/auth.service';
import { SourceApiService } from '../../services/source-api.service';
import {
    Categories,
    DefaultBehaviors,
    FullTextExtractModes,
    SourceDetail,
    SourceListItem,
    SourceTypes,
    UpsertSourceRequest,
    XSourceState
} from '../../shared/source.models';

interface SourceWithState extends SourceListItem {
  xState?: XSourceState | null;
  isLoadingState?: boolean;
}

@Component({
  selector: 'app-sources',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './sources.component.html',
  styleUrl: './sources.component.scss'
})
export class SourcesComponent implements OnInit {
  private sourceApi = inject(SourceApiService);
  private authService = inject(AuthService);

  // Role check
  isAdmin = computed(() => this.authService.hasRole('Admin'));

  // Data
  sources = signal<SourceWithState[]>([]);
  total = signal(0);

  // Filters
  typeFilter = '';
  categoryFilter = '';
  activeFilter = '';
  searchQuery = '';

  // Pagination
  page = signal(1);
  pageSize = signal(20);

  // UI State
  isLoading = signal(false);
  error = signal<string | null>(null);

  // Modal state
  showAddModal = signal(false);
  showEditModal = signal(false);
  isSubmitting = signal(false);
  editingSource = signal<SourceDetail | null>(null);
  
  // Form data
  formData: UpsertSourceRequest = this.getEmptyFormData();
  formErrors = signal<Record<string, string>>({});

  // Deleting & toggling
  deletingId = signal<string | null>(null);
  togglingId = signal<string | null>(null);

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  // Options
  sourceTypes = SourceTypes;
  defaultBehaviors = DefaultBehaviors;
  categories = Categories;
  fullTextExtractModes = FullTextExtractModes;

  get totalPages(): number {
    return Math.ceil(this.total() / this.pageSize());
  }

  ngOnInit(): void {
    this.loadSources();
  }

  loadSources(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.sourceApi.list({
      type: this.typeFilter || undefined,
      category: this.categoryFilter || undefined,
      isActive: this.activeFilter ? this.activeFilter === 'active' : undefined,
      q: this.searchQuery || undefined,
      page: this.page(),
      pageSize: this.pageSize()
    }).subscribe({
      next: (response) => {
        const sourcesWithState: SourceWithState[] = response.items.map(s => ({
          ...s,
          xState: null,
          isLoadingState: false
        }));
        this.sources.set(sourcesWithState);
        this.total.set(response.total);
        this.isLoading.set(false);

        // Load X state for X-type sources
        sourcesWithState
          .filter(s => s.type === 'X')
          .forEach(s => this.loadXState(s));
      },
      error: (err) => {
        console.error('Failed to load sources', err);
        this.error.set('Failed to load sources');
        this.isLoading.set(false);
      }
    });
  }

  loadXState(source: SourceWithState): void {
    source.isLoadingState = true;
    this.updateSource(source);

    this.sourceApi.getXState(source.id).subscribe({
      next: (state) => {
        source.xState = state;
        source.isLoadingState = false;
        this.updateSource(source);
      },
      error: () => {
        source.isLoadingState = false;
        this.updateSource(source);
      }
    });
  }

  private updateSource(source: SourceWithState): void {
    const sources = this.sources();
    const index = sources.findIndex(s => s.id === source.id);
    if (index >= 0) {
      sources[index] = { ...source };
      this.sources.set([...sources]);
    }
  }

  onSearch(): void {
    this.page.set(1);
    this.loadSources();
  }

  onClearFilters(): void {
    this.typeFilter = '';
    this.categoryFilter = '';
    this.activeFilter = '';
    this.searchQuery = '';
    this.page.set(1);
    this.loadSources();
  }

  onPageChange(newPage: number): void {
    if (newPage < 1 || newPage > this.totalPages) return;
    this.page.set(newPage);
    this.loadSources();
  }

  toggleActive(source: SourceListItem): void {
    if (this.togglingId()) return;
    
    this.togglingId.set(source.id);
    
    this.sourceApi.toggleActive(source.id, !source.isActive).subscribe({
      next: (updated) => {
        this.togglingId.set(null);
        const sources = this.sources();
        const index = sources.findIndex(s => s.id === source.id);
        if (index >= 0) {
          sources[index] = { ...sources[index], ...updated };
          this.sources.set([...sources]);
        }
        this.showToastMessage(
          `Source ${updated.isActive ? 'activated' : 'deactivated'}`,
          'success'
        );
      },
      error: () => {
        this.togglingId.set(null);
        this.showToastMessage('Failed to toggle source status', 'error');
      }
    });
  }

  openAddModal(): void {
    this.formData = this.getEmptyFormData();
    this.formErrors.set({});
    this.editingSource.set(null);
    this.showAddModal.set(true);
  }

  openEditModal(source: SourceListItem): void {
    // Load full details
    this.sourceApi.get(source.id).subscribe({
      next: (detail) => {
        this.editingSource.set(detail);
        this.formData = {
          name: detail.name,
          type: detail.type,
          identifier: detail.identifier,
          url: detail.url,
          description: detail.description,
          category: detail.category,
          trustLevel: detail.trustLevel,
          priority: detail.priority,
          isActive: detail.isActive,
          defaultBehavior: detail.defaultBehavior,
          fullTextFetchEnabled: detail.fullTextFetchEnabled,
          fullTextExtractMode: detail.fullTextExtractMode
        };
        this.showEditModal.set(true);
      },
      error: () => {
        this.showToastMessage('Failed to load source details', 'error');
      }
    });
  }

  closeModal(): void {
    this.showAddModal.set(false);
    this.showEditModal.set(false);
    this.editingSource.set(null);
    this.formErrors.set({});
  }

  validateForm(): boolean {
    const errors: Record<string, string> = {};

    // Name validation
    if (!this.formData.name?.trim()) {
      errors['name'] = 'Name is required';
    } else if (this.formData.name.length < 2) {
      errors['name'] = 'Name must be at least 2 characters';
    } else if (this.formData.name.length > 120) {
      errors['name'] = 'Name cannot exceed 120 characters';
    }

    // Type validation
    if (!this.formData.type) {
      errors['type'] = 'Type is required';
    }

    // URL validation for RSS
    if (this.formData.type === 'RSS') {
      if (!this.formData.url?.trim()) {
        errors['url'] = 'URL is required for RSS sources';
      } else {
        try {
          const url = new URL(this.formData.url);
          if (url.protocol !== 'http:' && url.protocol !== 'https:') {
            errors['url'] = 'URL must be HTTP or HTTPS';
          }
        } catch {
          errors['url'] = 'Please enter a valid URL';
        }
      }
    }

    // Identifier validation for X
    if (this.formData.type === 'X') {
      if (!this.formData.identifier?.trim()) {
        errors['identifier'] = 'Username is required for X sources';
      } else {
        const cleaned = this.formData.identifier.replace('@', '').trim();
        if (cleaned.includes(' ') || cleaned.includes('@')) {
          errors['identifier'] = 'Username cannot contain spaces or @';
        }
      }
    }

    // Trust level validation
    if (this.formData.trustLevel < 0 || this.formData.trustLevel > 100) {
      errors['trustLevel'] = 'Trust level must be between 0 and 100';
    }

    // Priority validation
    if (this.formData.priority < 0 || this.formData.priority > 1000) {
      errors['priority'] = 'Priority must be between 0 and 1000';
    }

    this.formErrors.set(errors);
    return Object.keys(errors).length === 0;
  }

  get isFormValid(): boolean {
    // Quick validation without setting errors
    if (!this.formData.name?.trim() || this.formData.name.length < 2) return false;
    if (!this.formData.type) return false;
    if (this.formData.type === 'RSS' && !this.formData.url?.trim()) return false;
    if (this.formData.type === 'X' && !this.formData.identifier?.trim()) return false;
    return true;
  }

  submitForm(): void {
    if (this.isSubmitting()) return;
    
    if (!this.validateForm()) return;

    this.isSubmitting.set(true);

    // Clean identifier for X type
    if (this.formData.type === 'X' && this.formData.identifier) {
      this.formData.identifier = this.formData.identifier.replace('@', '').trim();
    }

    const request: UpsertSourceRequest = { ...this.formData };

    if (this.editingSource()) {
      // Update
      this.sourceApi.update(this.editingSource()!.id, request).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeModal();
          this.showToastMessage('Source updated successfully', 'success');
          this.loadSources();
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.showToastMessage(err.error?.error || 'Failed to update source', 'error');
        }
      });
    } else {
      // Create
      this.sourceApi.create(request).subscribe({
        next: () => {
          this.isSubmitting.set(false);
          this.closeModal();
          this.showToastMessage('Source created successfully', 'success');
          this.loadSources();
        },
        error: (err) => {
          this.isSubmitting.set(false);
          this.showToastMessage(err.error?.error || 'Failed to create source', 'error');
        }
      });
    }
  }

  deleteSource(source: SourceListItem): void {
    if (this.deletingId()) return;

    if (!confirm(`Are you sure you want to delete "${source.name}"?`)) {
      return;
    }

    this.deletingId.set(source.id);

    this.sourceApi.delete(source.id).subscribe({
      next: () => {
        this.deletingId.set(null);
        this.showToastMessage('Source deleted', 'success');
        this.loadSources();
      },
      error: () => {
        this.deletingId.set(null);
        this.showToastMessage('Failed to delete source', 'error');
      }
    });
  }

  getTypeIcon(type: string): string {
    switch (type) {
      case 'RSS': return '📡';
      case 'X': return '𝕏';
      case 'Manual': return '✍️';
      case 'GoogleNews': return '📰';
      default: return '📰';
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

  private getEmptyFormData(): UpsertSourceRequest {
    return {
      name: '',
      type: 'RSS',
      identifier: null,
      url: null,
      description: null,
      category: 'Gundem',
      trustLevel: 50,
      priority: 100,
      isActive: true,
      defaultBehavior: 'Editorial',
      fullTextFetchEnabled: false,
      fullTextExtractMode: 'Auto'
    };
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}
