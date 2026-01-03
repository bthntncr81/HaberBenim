import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { EditorialApiService } from '../../services/editorial-api.service';
import { EditorialMediaApiService } from '../../services/editorial-media-api.service';
import { PublishApiService } from '../../services/publish-api.service';
import { BreakingApiService } from '../../services/breaking-api.service';
import { AiVideoApiService, AiVideoJob, AiVideoGenerateRequest } from '../../services/ai-video-api.service';
import { SourceTemplateApiService, ApplyTemplateResponse, ResolvedTextSpec } from '../../services/source-template-api.service';
import { ReadyQueueApiService, RenderJobDto } from '../../services/ready-queue-api.service';
import { AuthService } from '../../services/auth.service';
import { EditorialItem, SaveDraftRequest, CorrectionRequest } from '../../shared/editorial.models';
import { ChannelPublishLog } from '../../shared/publish.models';
import { MediaAsset, MediaOriginLabels, MediaOriginColors, GenerateImageRequest } from '../../shared/media.models';

type TabType = 'x' | 'web' | 'mobile' | 'push' | 'meta';

@Component({
  selector: 'app-editor',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './editor.component.html',
  styleUrl: './editor.component.scss'
})
export class EditorComponent implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private editorialApi = inject(EditorialApiService);
  private mediaApi = inject(EditorialMediaApiService);
  private publishApi = inject(PublishApiService);
  private breakingApi = inject(BreakingApiService);
  private aiVideoApi = inject(AiVideoApiService);
  private sourceTemplateApi = inject(SourceTemplateApiService);
  private readyQueueApi = inject(ReadyQueueApiService);
  private authService = inject(AuthService);

  // Data
  item = signal<EditorialItem | null>(null);
  
  // Template Application
  templatePlatforms = ['Instagram', 'X', 'TikTok', 'YouTube'];
  isApplyingTemplate = signal<string | null>(null); // platform being applied
  templateResults = signal<Record<string, ApplyTemplateResponse>>({});
  showTemplateResultModal = signal(false);
  activeTemplateResult = signal<{ platform: string; result: ApplyTemplateResponse } | null>(null);
  
  // Render Final
  renderJobs = signal<RenderJobDto[]>([]);
  isRenderingFinal = signal<string | null>(null); // platform being rendered
  isLoadingRenderJobs = signal(false);
  renderPollingInterval: any = null;
  contentId = signal<string>('');
  publishLogs = signal<ChannelPublishLog[]>([]);
  
  // Media
  mediaAssets = signal<MediaAsset[]>([]);
  isLoadingMedia = signal(false);
  isRefreshingMedia = signal(false);
  isGeneratingMedia = signal(false);
  isDeletingMedia = signal<string | null>(null); // asset id being deleted
  isSettingPrimary = signal<string | null>(null); // asset id being set as primary

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
  showAIGenerateModal = signal(false);
  rejectReason = '';
  scheduleDateTime = '';
  correctionNote = '';
  retractReason = '';
  
  // Breaking news fields
  breakingNote = '';
  breakingPriority = 100;
  breakingPushRequired = true;

  // AI Generate fields (Image)
  aiPromptOverride = '';
  aiStylePreset = 'news-illustration';
  aiForce = false;

  // AI Video fields
  aiVideoJob = signal<AiVideoJob | null>(null);
  isLoadingVideoStatus = signal(false);
  isGeneratingVideo = signal(false);
  isCancellingVideo = signal(false);
  showAIVideoModal = signal(false);
  
  // AI Video form
  videoGenerateAiVideo = false;
  videoMode: 'AutoPrompt' | 'CustomPrompt' = 'AutoPrompt';
  videoPromptOverride = '';
  videoPromptPreview = '';
  videoModel = 'sora-2';
  videoSeconds = '8';
  videoSize = '1280x720';
  videoForce = false;
  
  // AI Video polling interval
  private videoPollingInterval: any = null;

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
    publishToX: true,
    publishToInstagram: true,
    // Instagram
    instagramCaptionOverride: '',
    // AI Video
    generateAiVideo: false,
    aiVideoMode: 'AutoPrompt' as 'AutoPrompt' | 'CustomPrompt',
    aiVideoPromptOverride: ''
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

  // Media helpers
  get primaryMedia(): MediaAsset | null {
    return this.mediaAssets().find(a => a.isPrimary) || this.mediaAssets()[0] || null;
  }

  get hasMedia(): boolean {
    return this.mediaAssets().length > 0;
  }

  getOriginLabel(origin: string): string {
    return MediaOriginLabels[origin] || origin;
  }

  getOriginColor(origin: string): string {
    return MediaOriginColors[origin] || '#6b7280';
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.contentId.set(id);
      this.loadItem(id);
      this.loadMedia(id);
      this.loadVideoStatus(id);
      this.loadRenderJobs(id);
    }
  }

  ngOnDestroy(): void {
    this.stopVideoPolling();
    this.stopRenderPolling();
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

  loadMedia(id: string): void {
    this.isLoadingMedia.set(true);
    this.mediaApi.list(id).subscribe({
      next: (assets) => {
        this.mediaAssets.set(assets);
        this.isLoadingMedia.set(false);
      },
      error: (err) => {
        console.error('Failed to load media', err);
        this.isLoadingMedia.set(false);
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
        publishToX: item.draft.publishToX ?? true,
        publishToInstagram: item.draft.publishToInstagram ?? true,
        // Instagram
        instagramCaptionOverride: item.draft.instagramCaptionOverride || '',
        // AI Video
        generateAiVideo: (item.draft as any).generateAiVideo ?? false,
        aiVideoMode: (item.draft as any).aiVideoMode || 'AutoPrompt',
        aiVideoPromptOverride: (item.draft as any).aiVideoPromptOverride || ''
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
        publishToX: true,
        publishToInstagram: true,
        instagramCaptionOverride: '',
        // AI Video defaults
        generateAiVideo: false,
        aiVideoMode: 'AutoPrompt',
        aiVideoPromptOverride: ''
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
      publishToX: this.draft.publishToX,
      publishToInstagram: this.draft.publishToInstagram,
      // Instagram
      instagramCaptionOverride: this.draft.instagramCaptionOverride || null
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
      publishToInstagram: this.draft.publishToInstagram,
      instagramCaptionOverride: this.draft.instagramCaptionOverride || null,
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

  // Media Actions
  refreshMediaFromSource(): void {
    if (this.isRefreshingMedia()) return;

    this.isRefreshingMedia.set(true);
    this.mediaApi.refreshFromSource(this.contentId()).subscribe({
      next: (response) => {
        this.isRefreshingMedia.set(false);
        if (response.success) {
          this.showToastMessage('Kaynak görseli başarıyla çekildi', 'success');
          this.loadMedia(this.contentId());
        } else {
          this.showToastMessage(response.message || 'Kaynak görseli bulunamadı', 'error');
        }
      },
      error: (err) => {
        this.isRefreshingMedia.set(false);
        this.showToastMessage('Görsel çekilemedi', 'error');
      }
    });
  }

  openAIGenerateModal(): void {
    this.aiPromptOverride = '';
    this.aiStylePreset = 'news-illustration';
    this.aiForce = false;
    this.showAIGenerateModal.set(true);
  }

  closeAIGenerateModal(): void {
    this.showAIGenerateModal.set(false);
  }

  submitAIGenerate(): void {
    if (this.isGeneratingMedia()) return;

    this.isGeneratingMedia.set(true);

    const request: GenerateImageRequest = {
      force: this.aiForce
    };
    if (this.aiPromptOverride.trim()) {
      request.promptOverride = this.aiPromptOverride.trim();
    }
    if (this.aiStylePreset) {
      request.stylePreset = this.aiStylePreset;
    }

    this.mediaApi.generate(this.contentId(), request).subscribe({
      next: (result) => {
        this.isGeneratingMedia.set(false);
        this.showAIGenerateModal.set(false);
        
        if (result.success) {
          this.showToastMessage(result.message || 'AI görsel başarıyla oluşturuldu', 'success');
          this.loadMedia(this.contentId());
        } else {
          this.showToastMessage(result.error || result.message || 'AI görsel oluşturulamadı', 'error');
        }
      },
      error: (err) => {
        this.isGeneratingMedia.set(false);
        const errorMsg = err.error?.message || err.error?.error || 'AI görsel oluşturulamadı';
        this.showToastMessage(errorMsg, 'error');
      }
    });
  }

  setPrimaryMedia(assetId: string): void {
    if (this.isSettingPrimary()) return;

    this.isSettingPrimary.set(assetId);
    this.mediaApi.setPrimary(this.contentId(), assetId).subscribe({
      next: () => {
        this.isSettingPrimary.set(null);
        this.showToastMessage('Ana görsel güncellendi', 'success');
        this.loadMedia(this.contentId());
      },
      error: (err) => {
        this.isSettingPrimary.set(null);
        this.showToastMessage('Ana görsel güncellenemedi', 'error');
      }
    });
  }

  deleteMedia(assetId: string): void {
    if (this.isDeletingMedia()) return;

    if (!confirm('Bu görseli silmek istediğinizden emin misiniz?')) {
      return;
    }

    this.isDeletingMedia.set(assetId);
    this.mediaApi.delete(this.contentId(), assetId).subscribe({
      next: () => {
        this.isDeletingMedia.set(null);
        this.showToastMessage('Görsel silindi', 'success');
        this.loadMedia(this.contentId());
      },
      error: (err) => {
        this.isDeletingMedia.set(null);
        this.showToastMessage('Görsel silinemedi', 'error');
      }
    });
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
  }

  // AI Video Methods
  loadVideoStatus(id: string): void {
    this.isLoadingVideoStatus.set(true);
    this.aiVideoApi.getStatus(id).subscribe({
      next: (response) => {
        this.isLoadingVideoStatus.set(false);
        if (response.job) {
          this.aiVideoJob.set(response.job);
          // Start polling if in progress
          if (response.job.status === 'Queued' || response.job.status === 'InProgress') {
            this.startVideoPolling();
          }
        }
      },
      error: (err) => {
        this.isLoadingVideoStatus.set(false);
        console.error('Failed to load video status', err);
      }
    });
  }

  startVideoPolling(): void {
    if (this.videoPollingInterval) return;
    
    this.videoPollingInterval = setInterval(() => {
      const job = this.aiVideoJob();
      if (!job || (job.status !== 'Queued' && job.status !== 'InProgress')) {
        this.stopVideoPolling();
        return;
      }

      this.aiVideoApi.getStatus(this.contentId()).subscribe({
        next: (response) => {
          if (response.job) {
            this.aiVideoJob.set(response.job);
            if (response.job.status === 'Completed') {
              this.stopVideoPolling();
              this.showToastMessage('AI video başarıyla oluşturuldu!', 'success');
            } else if (response.job.status === 'Failed') {
              this.stopVideoPolling();
              this.showToastMessage('AI video oluşturulamadı: ' + (response.job.error || 'Bilinmeyen hata'), 'error');
            }
          }
        },
        error: () => {
          // Ignore polling errors
        }
      });
    }, 5000); // Poll every 5 seconds
  }

  stopVideoPolling(): void {
    if (this.videoPollingInterval) {
      clearInterval(this.videoPollingInterval);
      this.videoPollingInterval = null;
    }
  }

  openAIVideoModal(): void {
    this.videoMode = 'AutoPrompt';
    this.videoPromptOverride = '';
    this.videoPromptPreview = '';
    this.videoModel = 'sora-2';
    this.videoSeconds = '8';
    this.videoSize = '1280x720';
    this.videoForce = false;
    this.showAIVideoModal.set(true);
    
    // Load prompt preview
    this.loadVideoPromptPreview();
  }

  closeAIVideoModal(): void {
    this.showAIVideoModal.set(false);
  }

  loadVideoPromptPreview(): void {
    const customPrompt = this.videoMode === 'CustomPrompt' ? this.videoPromptOverride : undefined;
    this.aiVideoApi.getPromptPreview(this.contentId(), customPrompt).subscribe({
      next: (response) => {
        if (response.preview) {
          this.videoPromptPreview = response.preview.prompt;
        }
      },
      error: (err) => {
        console.error('Failed to load prompt preview', err);
      }
    });
  }

  onVideoModeChange(): void {
    if (this.videoMode === 'AutoPrompt') {
      this.loadVideoPromptPreview();
    } else {
      this.videoPromptPreview = '';
    }
  }

  generateAiVideo(): void {
    if (this.isGeneratingVideo()) return;

    this.isGeneratingVideo.set(true);

    const request: AiVideoGenerateRequest = {
      force: this.videoForce,
      mode: this.videoMode,
      model: this.videoModel,
      seconds: this.videoSeconds,
      size: this.videoSize
    };

    if (this.videoMode === 'CustomPrompt' && this.videoPromptOverride.trim()) {
      request.promptOverride = this.videoPromptOverride.trim();
    }

    this.aiVideoApi.generate(this.contentId(), request).subscribe({
      next: (response) => {
        this.isGeneratingVideo.set(false);
        this.closeAIVideoModal();
        
        if (response.success && response.job) {
          this.aiVideoJob.set(response.job);
          this.showToastMessage(response.message || 'Video üretimi başladı', 'success');
          
          // Start polling if job is in progress
          if (response.job.status === 'Queued' || response.job.status === 'InProgress') {
            this.startVideoPolling();
          }
        } else {
          this.showToastMessage(response.message || 'Video üretimi başlatılamadı', 'error');
        }
      },
      error: (err) => {
        this.isGeneratingVideo.set(false);
        const errorMsg = err.error?.message || 'AI video oluşturulamadı';
        this.showToastMessage(errorMsg, 'error');
      }
    });
  }

  retryVideoGeneration(): void {
    this.videoForce = true;
    this.generateAiVideo();
  }

  cancelVideoGeneration(): void {
    if (this.isCancellingVideo()) return;

    this.isCancellingVideo.set(true);
    this.aiVideoApi.cancel(this.contentId()).subscribe({
      next: (response) => {
        this.isCancellingVideo.set(false);
        if (response.success) {
          this.stopVideoPolling();
          this.aiVideoJob.set(null);
          this.showToastMessage('Video üretimi iptal edildi', 'success');
        } else {
          this.showToastMessage(response.message || 'İptal edilemedi', 'error');
        }
      },
      error: (err) => {
        this.isCancellingVideo.set(false);
        this.showToastMessage('İptal edilemedi', 'error');
      }
    });
  }

  getVideoStatusLabel(status: string): string {
    switch (status) {
      case 'Queued': return 'Sırada';
      case 'InProgress': return 'Üretiliyor';
      case 'Completed': return 'Tamamlandı';
      case 'Failed': return 'Başarısız';
      case 'Cancelled': return 'İptal Edildi';
      default: return status;
    }
  }

  getVideoStatusClass(status: string): string {
    switch (status) {
      case 'Queued': return 'status-queued';
      case 'InProgress': return 'status-progress';
      case 'Completed': return 'status-completed';
      case 'Failed': return 'status-failed';
      case 'Cancelled': return 'status-cancelled';
      default: return '';
    }
  }

  private showToastMessage(message: string, type: 'success' | 'error'): void {
    this.toastMessage.set(message);
    this.toastType.set(type);
    this.showToast.set(true);
    setTimeout(() => this.showToast.set(false), 4000);
  }

  // Template Application Methods
  applyTemplate(platform: string): void {
    if (this.isApplyingTemplate()) return;

    this.isApplyingTemplate.set(platform);
    this.sourceTemplateApi.applyTemplate(this.contentId(), { platform }).subscribe({
      next: (response) => {
        this.isApplyingTemplate.set(null);
        
        // Store result
        const results = { ...this.templateResults() };
        results[platform] = response;
        this.templateResults.set(results);

        if (response.success && response.selectedTemplateId) {
          this.showToastMessage(
            `Template "${response.selectedTemplateName}" applied for ${platform}`,
            'success'
          );
          // Show result modal
          this.activeTemplateResult.set({ platform, result: response });
          this.showTemplateResultModal.set(true);
        } else if (response.skipReason) {
          this.showToastMessage(
            `Skipped for ${platform}: ${response.skipReason}`,
            'error'
          );
        } else {
          this.showToastMessage(
            response.error || `No template matched for ${platform}`,
            'error'
          );
        }
      },
      error: (err) => {
        this.isApplyingTemplate.set(null);
        this.showToastMessage(
          err.error?.error || `Failed to apply template for ${platform}`,
          'error'
        );
      }
    });
  }

  closeTemplateResultModal(): void {
    this.showTemplateResultModal.set(false);
    this.activeTemplateResult.set(null);
  }

  getTemplateResult(platform: string): ApplyTemplateResponse | null {
    return this.templateResults()[platform] || null;
  }

  hasTemplateResult(platform: string): boolean {
    const result = this.templateResults()[platform];
    return result?.success === true && !!result.selectedTemplateId;
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

  // Render Final Methods
  loadRenderJobs(contentId: string): void {
    this.isLoadingRenderJobs.set(true);
    this.readyQueueApi.getItem(contentId).subscribe({
      next: (item) => {
        this.renderJobs.set(item.renderJobs || []);
        this.isLoadingRenderJobs.set(false);
        
        // Start polling if any jobs are in progress
        if (item.renderJobs?.some(j => j.status === 'Queued' || j.status === 'Rendering')) {
          this.startRenderPolling();
        }
      },
      error: () => {
        this.renderJobs.set([]);
        this.isLoadingRenderJobs.set(false);
      }
    });
  }

  startRenderPolling(): void {
    if (this.renderPollingInterval) return;
    
    this.renderPollingInterval = setInterval(() => {
      const jobs = this.renderJobs();
      if (!jobs.some(j => j.status === 'Queued' || j.status === 'Rendering')) {
        this.stopRenderPolling();
        return;
      }

      this.readyQueueApi.getItem(this.contentId()).subscribe({
        next: (item) => {
          this.renderJobs.set(item.renderJobs || []);
          
          // Check if any job just completed
          const completed = item.renderJobs?.find(j => j.status === 'Completed');
          if (completed && !jobs.find(j => j.id === completed.id && j.status === 'Completed')) {
            this.showToastMessage(`Render completed for ${completed.platform}`, 'success');
          }
        },
        error: () => {}
      });
    }, 5000);
  }

  stopRenderPolling(): void {
    if (this.renderPollingInterval) {
      clearInterval(this.renderPollingInterval);
      this.renderPollingInterval = null;
    }
  }

  renderFinal(platform: string): void {
    if (this.isRenderingFinal()) return;

    this.isRenderingFinal.set(platform);
    this.readyQueueApi.createRenderJobs(this.contentId(), {
      platforms: [platform],
      force: false
    }).subscribe({
      next: (response) => {
        this.isRenderingFinal.set(null);
        if (response.success && response.createdJobs.length > 0) {
          this.showToastMessage(`Render job created for ${platform}`, 'success');
          this.loadRenderJobs(this.contentId());
        } else if (response.createdJobs.length === 0) {
          this.showToastMessage(response.message || 'No template found or already rendered', 'error');
        }
      },
      error: (err) => {
        this.isRenderingFinal.set(null);
        this.showToastMessage(err.error?.error || `Failed to create render job for ${platform}`, 'error');
      }
    });
  }

  renderAllPlatforms(): void {
    if (this.isRenderingFinal()) return;

    this.isRenderingFinal.set('all');
    this.readyQueueApi.createRenderJobs(this.contentId(), {
      platforms: this.templatePlatforms,
      force: false
    }).subscribe({
      next: (response) => {
        this.isRenderingFinal.set(null);
        if (response.success) {
          this.showToastMessage(response.message, 'success');
          this.loadRenderJobs(this.contentId());
        }
      },
      error: (err) => {
        this.isRenderingFinal.set(null);
        this.showToastMessage(err.error?.error || 'Failed to create render jobs', 'error');
      }
    });
  }

  getRenderJob(platform: string): RenderJobDto | null {
    return this.renderJobs().find(j => j.platform === platform) || null;
  }

  getRenderStatusLabel(status: string): string {
    switch (status) {
      case 'Queued': return 'In Queue';
      case 'Rendering': return 'Rendering...';
      case 'Completed': return 'Ready';
      case 'Failed': return 'Failed';
      default: return status;
    }
  }

  getRenderStatusClass(status: string): string {
    switch (status) {
      case 'Queued': return 'status-queued';
      case 'Rendering': return 'status-rendering';
      case 'Completed': return 'status-completed';
      case 'Failed': return 'status-failed';
      default: return '';
    }
  }

  parseResolvedText(json: string | null): Record<string, string> {
    if (!json) return {};
    try {
      return JSON.parse(json);
    } catch {
      return {};
    }
  }
}
