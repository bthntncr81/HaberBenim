import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RulesApiService } from '../../services/rules-api.service';
import { FeedApiService } from '../../services/feed-api.service';
import { 
  Rule, 
  CreateRuleRequest, 
  UpdateRuleRequest, 
  DecisionTypeOptions, 
  DecisionType 
} from '../../shared/rule.models';
import { Source } from '../../shared/feed.models';

@Component({
  selector: 'app-rules',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './rules.component.html',
  styleUrl: './rules.component.scss'
})
export class RulesComponent implements OnInit {
  private rulesApi = inject(RulesApiService);
  private feedApi = inject(FeedApiService);

  // Data
  rules = signal<Rule[]>([]);
  sources = signal<Source[]>([]);
  
  // UI State
  isLoading = signal(false);
  isSaving = signal(false);
  showModal = signal(false);
  isEditMode = signal(false);
  editingRuleId = signal<string | null>(null);
  
  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  // Form
  formData = {
    name: '',
    isEnabled: true,
    priority: 0,
    decisionType: 'RequireApproval' as DecisionType,
    minTrustLevel: null as number | null,
    keywordsIncludeCsv: '',
    keywordsExcludeCsv: '',
    sourceIdsCsv: '',
    groupIdsCsv: ''
  };

  // Selected sources for multi-select
  selectedSourceIds: string[] = [];

  // Options
  decisionTypeOptions = DecisionTypeOptions;
  trustLevelOptions = [
    { value: null, label: 'Any' },
    { value: 1, label: '1 - Low' },
    { value: 2, label: '2 - Medium' },
    { value: 3, label: '3 - High' }
  ];

  ngOnInit(): void {
    this.loadRules();
    this.loadSources();
  }

  loadRules(): void {
    this.isLoading.set(true);
    this.rulesApi.listRules().subscribe({
      next: (rules) => {
        this.rules.set(rules);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load rules', err);
        this.isLoading.set(false);
        this.showToastMessage('Failed to load rules', 'error');
      }
    });
  }

  loadSources(): void {
    this.feedApi.getSources().subscribe({
      next: (sources) => this.sources.set(sources),
      error: (err) => console.error('Failed to load sources', err)
    });
  }

  openNewRuleModal(): void {
    this.resetForm();
    this.isEditMode.set(false);
    this.editingRuleId.set(null);
    this.showModal.set(true);
  }

  openEditRuleModal(rule: Rule): void {
    this.formData = {
      name: rule.name,
      isEnabled: rule.isEnabled,
      priority: rule.priority,
      decisionType: rule.decisionType,
      minTrustLevel: rule.minTrustLevel,
      keywordsIncludeCsv: rule.keywordsIncludeCsv || '',
      keywordsExcludeCsv: rule.keywordsExcludeCsv || '',
      sourceIdsCsv: rule.sourceIdsCsv || '',
      groupIdsCsv: rule.groupIdsCsv || ''
    };
    this.selectedSourceIds = rule.sourceIdsCsv 
      ? rule.sourceIdsCsv.split(',').map(s => s.trim()).filter(s => s) 
      : [];
    this.isEditMode.set(true);
    this.editingRuleId.set(rule.id);
    this.showModal.set(true);
  }

  closeModal(): void {
    this.showModal.set(false);
    this.resetForm();
  }

  resetForm(): void {
    this.formData = {
      name: '',
      isEnabled: true,
      priority: 0,
      decisionType: 'RequireApproval',
      minTrustLevel: null,
      keywordsIncludeCsv: '',
      keywordsExcludeCsv: '',
      sourceIdsCsv: '',
      groupIdsCsv: ''
    };
    this.selectedSourceIds = [];
  }

  onSourceSelectionChange(sourceId: string, event: Event): void {
    const checked = (event.target as HTMLInputElement).checked;
    if (checked) {
      if (!this.selectedSourceIds.includes(sourceId)) {
        this.selectedSourceIds.push(sourceId);
      }
    } else {
      this.selectedSourceIds = this.selectedSourceIds.filter(id => id !== sourceId);
    }
    this.formData.sourceIdsCsv = this.selectedSourceIds.join(',');
  }

  isSourceSelected(sourceId: string): boolean {
    return this.selectedSourceIds.includes(sourceId);
  }

  saveRule(): void {
    if (!this.formData.name.trim()) {
      this.showToastMessage('Name is required', 'error');
      return;
    }

    this.isSaving.set(true);

    const payload: CreateRuleRequest | UpdateRuleRequest = {
      name: this.formData.name.trim(),
      isEnabled: this.formData.isEnabled,
      priority: this.formData.priority,
      decisionType: this.formData.decisionType,
      minTrustLevel: this.formData.minTrustLevel,
      keywordsIncludeCsv: this.formData.keywordsIncludeCsv.trim() || null,
      keywordsExcludeCsv: this.formData.keywordsExcludeCsv.trim() || null,
      sourceIdsCsv: this.selectedSourceIds.length > 0 ? this.selectedSourceIds.join(',') : null,
      groupIdsCsv: this.formData.groupIdsCsv.trim() || null
    };

    if (this.isEditMode() && this.editingRuleId()) {
      this.rulesApi.updateRule(this.editingRuleId()!, payload).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.closeModal();
          this.loadRules();
          this.showToastMessage('Rule updated successfully', 'success');
        },
        error: (err) => {
          this.isSaving.set(false);
          this.showToastMessage(err.error?.error || 'Failed to update rule', 'error');
        }
      });
    } else {
      this.rulesApi.createRule(payload).subscribe({
        next: () => {
          this.isSaving.set(false);
          this.closeModal();
          this.loadRules();
          this.showToastMessage('Rule created successfully', 'success');
        },
        error: (err) => {
          this.isSaving.set(false);
          this.showToastMessage(err.error?.error || 'Failed to create rule', 'error');
        }
      });
    }
  }

  deleteRule(rule: Rule): void {
    if (!confirm(`Are you sure you want to delete "${rule.name}"?`)) {
      return;
    }

    this.rulesApi.deleteRule(rule.id).subscribe({
      next: () => {
        this.loadRules();
        this.showToastMessage('Rule deleted successfully', 'success');
      },
      error: (err) => {
        this.showToastMessage(err.error?.error || 'Failed to delete rule', 'error');
      }
    });
  }

  toggleRuleEnabled(rule: Rule): void {
    const payload: UpdateRuleRequest = {
      name: rule.name,
      isEnabled: !rule.isEnabled,
      priority: rule.priority,
      decisionType: rule.decisionType,
      minTrustLevel: rule.minTrustLevel,
      keywordsIncludeCsv: rule.keywordsIncludeCsv,
      keywordsExcludeCsv: rule.keywordsExcludeCsv,
      sourceIdsCsv: rule.sourceIdsCsv,
      groupIdsCsv: rule.groupIdsCsv
    };

    this.rulesApi.updateRule(rule.id, payload).subscribe({
      next: () => {
        this.loadRules();
        this.showToastMessage(`Rule ${!rule.isEnabled ? 'enabled' : 'disabled'}`, 'success');
      },
      error: (err) => {
        this.showToastMessage('Failed to update rule', 'error');
      }
    });
  }

  getDecisionTypeLabel(type: string): string {
    const opt = this.decisionTypeOptions.find(o => o.value === type);
    return opt?.label || type;
  }

  truncate(text: string | null, length: number): string {
    if (!text) return '-';
    return text.length > length ? text.substring(0, length) + '...' : text;
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}
