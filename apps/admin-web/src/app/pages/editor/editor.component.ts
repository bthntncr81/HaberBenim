import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EditorialApiService } from '../../services/editorial-api.service';
import { PublishApiService } from '../../services/publish-api.service';
import { BreakingApiService } from '../../services/breaking-api.service';
import { AuthService } from '../../services/auth.service';
import { EditorialItem, SaveDraftRequest, CorrectionRequest } from '../../shared/editorial.models';
import { ChannelPublishLog } from '../../shared/publish.models';

type TabType = 'x' | 'web' | 'mobile' | 'push' | 'meta';

@Component({
  selector: 'app-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './editor.component.html',
  styleUrl: './editor.component.scss'
})
export class EditorComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private editorialApi = inject(EditorialApiService);
  private publishApi = inject(PublishApiService);
  private breakingApi = inject(BreakingApiService);
  private authService = inject(AuthService);

  // Data
  item = signal<EditorialItem | null>(null);
  contentId = signal<string>('');
  publishLogs = signal<ChannelPublishLog[]>([]);

  // UI State
  isLoading = signal(true);
  isSaving = signal(false);
  isApproving = signal(false);
  isRejecting = signal(false);
  isScheduling = signal(false);
  isEnqueueing = signal(false);
  isCorrecting = signal(false);
  isRetracting = signal(false);
  isMarkingBreaking = signal(false);
  activeTab = signal<TabType>('x');

  // Modals
  showRejectModal = signal(false);
  showScheduleModal = signal(false);
  showLogsModal = signal(false);
  showCorrectionModal = signal(false);
  showRetractModal = signal(false);
  showBreakingModal = signal(false);
  rejectReason = '';
  scheduleDateTime = '';
  correctionNote = '';
  retractReason = '';
  
  // Breaking news fields
  breakingNote = '';
  breakingPriority = 100;
  breakingPushRequired = true;

  // Toast
  toastMessage = signal('');
  toastType = signal<'success' | 'error'>('success');
  showToast = signal(false);

  // Draft form fields
  draft = {
    xText: '',
    webTitle: '',
    webBody: '',
    mobileSummary: '',
    pushTitle: '',
    pushBody: '',
    hashtagsCsv: '',
    mentionsCsv: '',
    editorialNote: '',
    // Channel toggles
    publishToWeb: true,
    publishToMobile: true,
    publishToX: true
  };

  get canApproveRejectSchedule(): boolean {
    return this.authService.hasAnyRole(['Admin', 'Editor']);
  }

  get canEnqueuePublish(): boolean {
    const itemVal = this.item();
    if (!itemVal) return false;
    return this.authService.hasAnyRole(['Admin', 'Editor']) &&
      (itemVal.status === 'ReadyToPublish' || itemVal.status === 'Scheduled' || itemVal.status === 'AutoReady');
  }

  get isPublished(): boolean {
    return this.item()?.status === 'Published';
  }

  get canCorrect(): boolean {
    return this.isPublished && this.authService.hasAnyRole(['Admin', 'Editor']);
  }

  get canRetract(): boolean {
    return this.isPublished && this.authService.hasAnyRole(['Admin', 'Editor']);
  }

  get canMarkBreaking(): boolean {
    return this.authService.hasAnyRole(['Admin', 'Editor']);
  }

  get isBreaking(): boolean {
    return !!(this.item() as any)?.isBreaking;
  }

  get currentVersionNo(): number {
    const revisions = this.item()?.revisions || [];
    if (revisions.length === 0) return 0;
    return Math.max(...revisions.map(r => r.versionNo));
  }

  get lastActionType(): string {
    const revisions = this.item()?.revisions || [];
    if (revisions.length === 0) return 'None';
    const sorted = [...revisions].sort((a, b) => b.versionNo - a.versionNo);
    return sorted[0].actionType;
  }

  get xCharCount(): number {
    return this.draft.xText?.length || 0;
  }

  get xCharWarning(): boolean {
    return this.xCharCount > 280;
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.contentId.set(id);
      this.loadItem(id);
    }
  }

  loadItem(id: string): void {
    this.isLoading.set(true);
    this.editorialApi.getItem(id).subscribe({
      next: (item) => {
        this.item.set(item);
        this.populateDraft(item);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load item', err);
        this.isLoading.set(false);
        this.showToastMessage('Failed to load content', 'error');
      }
    });
  }

  populateDraft(item: EditorialItem): void {
    if (item.draft) {
      this.draft = {
        xText: item.draft.xText || '',
        webTitle: item.draft.webTitle || '',
        webBody: item.draft.webBody || '',
        mobileSummary: item.draft.mobileSummary || '',
        pushTitle: item.draft.pushTitle || '',
        pushBody: item.draft.pushBody || '',
        hashtagsCsv: item.draft.hashtagsCsv || '',
        mentionsCsv: item.draft.mentionsCsv || '',
        editorialNote: item.editorialNote || '',
        // Channel toggles
        publishToWeb: item.draft.publishToWeb ?? true,
        publishToMobile: item.draft.publishToMobile ?? true,
        publishToX: item.draft.publishToX ?? true
      };
    } else {
      // Use defaults from content item
      this.draft = {
        xText: item.title.substring(0, 280),
        webTitle: item.title,
        webBody: item.bodyText,
        mobileSummary: (item.summary || item.bodyText).substring(0, 200),
        pushTitle: item.title.substring(0, 100),
        pushBody: (item.summary || item.bodyText).substring(0, 200),
        hashtagsCsv: '',
        mentionsCsv: '',
        editorialNote: item.editorialNote || '',
        // All channels enabled by default
        publishToWeb: true,
        publishToMobile: true,
        publishToX: true
      };
    }
  }

  setActiveTab(tab: TabType): void {
    this.activeTab.set(tab);
  }

  saveDraft(): void {
    if (this.isSaving()) return;

    this.isSaving.set(true);

    const payload: SaveDraftRequest = {
      xText: this.draft.xText,
      webTitle: this.draft.webTitle,
      webBody: this.draft.webBody,
      mobileSummary: this.draft.mobileSummary,
      pushTitle: this.draft.pushTitle,
      pushBody: this.draft.pushBody,
      hashtagsCsv: this.draft.hashtagsCsv,
      mentionsCsv: this.draft.mentionsCsv,
      editorialNote: this.draft.editorialNote,
      // Channel toggles
      publishToWeb: this.draft.publishToWeb,
      publishToMobile: this.draft.publishToMobile,
      publishToX: this.draft.publishToX
    };

    this.editorialApi.saveDraft(this.contentId(), payload).subscribe({
      next: (response) => {
        this.isSaving.set(false);
        this.showToastMessage(`Draft saved (v${response.latestVersionNo})`, 'success');
        this.loadItem(this.contentId());
      },
      error: (err) => {
        this.isSaving.set(false);
        this.showToastMessage('Failed to save draft', 'error');
      }
    });
  }

  approve(): void {
    if (this.isApproving() || !this.canApproveRejectSchedule) return;

    this.isApproving.set(true);
    this.editorialApi.approve(this.contentId()).subscribe({
      next: () => {
        this.isApproving.set(false);
        this.showToastMessage('Content approved & queued!', 'success');
        this.loadItem(this.contentId());
      },
      error: (err) => {
        this.isApproving.set(false);
        this.showToastMessage('Failed to approve', 'error');
      }
    });
  }

  openRejectModal(): void {
    this.rejectReason = '';
    this.showRejectModal.set(true);
  }

  closeRejectModal(): void {
    this.showRejectModal.set(false);
  }

  confirmReject(): void {
    if (!this.rejectReason.trim() || this.isRejecting()) return;

    this.isRejecting.set(true);
    this.editorialApi.reject(this.contentId(), this.rejectReason).subscribe({
      next: () => {
        this.isRejecting.set(false);
        this.showRejectModal.set(false);
        this.showToastMessage('Content rejected', 'success');
        this.loadItem(this.contentId());
      },
      error: (err) => {
        this.isRejecting.set(false);
        this.showToastMessage('Failed to reject', 'error');
      }
    });
  }

  openScheduleModal(): void {
    // Default to 1 hour from now
    const defaultTime = new Date(Date.now() + 60 * 60 * 1000);
    this.scheduleDateTime = defaultTime.toISOString().slice(0, 16);
    this.showScheduleModal.set(true);
  }

  closeScheduleModal(): void {
    this.showScheduleModal.set(false);
  }

  confirmSchedule(): void {
    if (!this.scheduleDateTime || this.isScheduling()) return;

    const scheduledUtc = new Date(this.scheduleDateTime).toISOString();

    this.isScheduling.set(true);
    this.editorialApi.schedule(this.contentId(), scheduledUtc).subscribe({
      next: () => {
        this.isScheduling.set(false);
        this.showScheduleModal.set(false);
        this.showToastMessage('Content scheduled & queued!', 'success');
        this.loadItem(this.contentId());
      },
      error: (err) => {
        this.isScheduling.set(false);
        this.showToastMessage('Failed to schedule', 'error');
      }
    });
  }

  enqueuePublish(): void {
    if (this.isEnqueueing()) return;

    this.isEnqueueing.set(true);
    this.publishApi.enqueue(this.contentId()).subscribe({
      next: (response) => {
        this.isEnqueueing.set(false);
        if (response.alreadyQueued) {
          this.showToastMessage('Already queued for publishing', 'success');
        } else {
          this.showToastMessage('Queued for publishing!', 'success');
        }
        this.loadItem(this.contentId());
      },
      error: (err) => {
        this.isEnqueueing.set(false);
        this.showToastMessage('Failed to enqueue', 'error');
      }
    });
  }

  viewPublishLogs(): void {
    this.publishLogs.set([]);
    this.showLogsModal.set(true);

    this.publishApi.getLogs(this.contentId()).subscribe({
      next: (logs) => {
        this.publishLogs.set(logs);
      },
      error: (err) => {
        console.error('Failed to load logs', err);
      }
    });
  }

  closeLogsModal(): void {
    this.showLogsModal.set(false);
  }

  // Correction Mode (Sprint 7)
  openCorrectionModal(): void {
    this.correctionNote = '';
    this.showCorrectionModal.set(true);
  }

  closeCorrectionModal(): void {
    this.showCorrectionModal.set(false);
  }

  submitCorrection(): void {
    if (this.isCorrecting()) return;

    this.isCorrecting.set(true);

    const payload: CorrectionRequest = {
      xText: this.draft.xText,
      webTitle: this.draft.webTitle,
      webBody: this.draft.webBody,
      mobileSummary: this.draft.mobileSummary,
      pushTitle: this.draft.pushTitle,
      pushBody: this.draft.pushBody,
      hashtagsCsv: this.draft.hashtagsCsv,
      mentionsCsv: this.draft.mentionsCsv,
      editorialNote: this.draft.editorialNote,
      publishToWeb: this.draft.publishToWeb,
      publishToMobile: this.draft.publishToMobile,
      publishToX: this.draft.publishToX,
      correctionNote: this.correctionNote || undefined
    };

    this.editorialApi.correct(this.contentId(), payload).subscribe({
      next: (response) => {
        this.isCorrecting.set(false);
        this.showCorrectionModal.set(false);
        
        if (response.ok) {
          this.showToastMessage(
            `Correction saved (v${response.versionNo})${response.jobId ? ' & republish queued' : ''}`,
            'success'
          );
          this.loadItem(this.contentId());
        } else {
          this.showToastMessage(response.error || 'Correction failed', 'error');
        }
      },
      error: (err) => {
        this.isCorrecting.set(false);
        this.showToastMessage('Failed to submit correction', 'error');
      }
    });
  }

  // Retract Mode (Sprint 8)
  openRetractModal(): void {
    this.retractReason = '';
    this.showRetractModal.set(true);
  }

  closeRetractModal(): void {
    this.showRetractModal.set(false);
  }

  submitRetract(): void {
    if (!this.retractReason.trim() || this.isRetracting()) return;

    this.isRetracting.set(true);

    this.editorialApi.retract(this.contentId(), this.retractReason).subscribe({
      next: (response) => {
        this.isRetracting.set(false);
        this.showRetractModal.set(false);
        
        if (response.ok) {
          this.showToastMessage(
            `Content retracted (v${response.versionNo})`,
            'success'
          );
          this.loadItem(this.contentId());
        } else {
          this.showToastMessage(response.error || 'Retraction failed', 'error');
        }
      },
      error: (err) => {
        this.isRetracting.set(false);
        this.showToastMessage('Failed to retract content', 'error');
      }
    });
  }

  // Mark Breaking (Sprint 8)
  openBreakingModal(): void {
    this.breakingNote = '';
    this.breakingPriority = 100;
    this.breakingPushRequired = true;
    this.showBreakingModal.set(true);
  }

  closeBreakingModal(): void {
    this.showBreakingModal.set(false);
  }

  submitMarkBreaking(): void {
    if (this.isMarkingBreaking()) return;

    this.isMarkingBreaking.set(true);

    this.breakingApi.mark(this.contentId(), {
      note: this.breakingNote || undefined,
      priority: this.breakingPriority,
      pushRequired: this.breakingPushRequired
    }).subscribe({
      next: (response) => {
        this.isMarkingBreaking.set(false);
        this.showBreakingModal.set(false);
        
        if (response.ok) {
          this.showToastMessage(
            `Marked as breaking (v${response.versionNo})${response.jobId ? '. Job: ' + response.jobId : ''}`,
            'success'
          );
          this.loadItem(this.contentId());
        } else {
          this.showToastMessage(response.error || 'Failed to mark as breaking', 'error');
        }
      },
      error: (err) => {
        this.isMarkingBreaking.set(false);
        const errorMsg = err.error?.error || 'Failed to mark as breaking';
        this.showToastMessage(errorMsg, 'error');
      }
    });
  }

  goToPublishing(): void {
    this.router.navigate(['/publishing'], {
      queryParams: { contentId: this.contentId() }
    });
  }

  goBack(): void {
    this.router.navigate(['/approval']);
  }

  formatDate(dateStr: string | null): string {
    if (!dateStr) return '-';
    return new Date(dateStr).toLocaleString();
  }

  getStatusClass(status: string): string {
    return status?.toLowerCase().replace(/\s+/g, '') || '';
  }

  getActionIcon(actionType: string): string {
    switch (actionType) {
      case 'DraftSaved': return '💾';
      case 'Approved': return '✅';
      case 'Rejected': return '❌';
      case 'Scheduled': return '📅';
      case 'Correction': return '🔧';
      case 'Retracted': return '🚫';
      case 'Breaking': return '🔴';
      case 'Published': return '🚀';
      default: return '📝';
    }
  }

  getChannelIcon(channel: string): string {
    switch (channel) {
      case 'Web': return '🌐';
      case 'Mobile': return '📱';
      case 'X': return '𝕏';
      default: return '📤';
    }
  }

  getSkippedReason(log: ChannelPublishLog): string | null {
    if (log.status !== 'Skipped') return null;
    if (log.responseJson) {
      try {
        const data = JSON.parse(log.responseJson);
        return data.reason || data.Reason || 'Skipped';
      } catch {
        return 'Skipped';
      }
    }
    return 'Skipped';
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }
}
