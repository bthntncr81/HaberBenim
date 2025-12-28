// Decision types
export const DecisionTypes = {
  AutoPublish: 'AutoPublish',
  RequireApproval: 'RequireApproval',
  Block: 'Block',
  Schedule: 'Schedule'
} as const;

export type DecisionType = typeof DecisionTypes[keyof typeof DecisionTypes];

export const DecisionTypeOptions: { value: DecisionType; label: string }[] = [
  { value: 'AutoPublish', label: 'Auto Publish' },
  { value: 'RequireApproval', label: 'Require Approval' },
  { value: 'Block', label: 'Block' },
  { value: 'Schedule', label: 'Schedule' }
];

// Content status types
export const ContentStatuses = {
  New: 'New',
  PendingApproval: 'PendingApproval',
  Blocked: 'Blocked',
  Scheduled: 'Scheduled',
  AutoReady: 'AutoReady',
  Published: 'Published',
  Archived: 'Archived'
} as const;

export type ContentStatus = typeof ContentStatuses[keyof typeof ContentStatuses];

export const ContentStatusOptions: { value: ContentStatus; label: string }[] = [
  { value: 'New', label: 'New' },
  { value: 'PendingApproval', label: 'Pending Approval' },
  { value: 'Blocked', label: 'Blocked' },
  { value: 'Scheduled', label: 'Scheduled' },
  { value: 'AutoReady', label: 'Auto Ready' },
  { value: 'Published', label: 'Published' },
  { value: 'Archived', label: 'Archived' }
];

// Rule interfaces
export interface Rule {
  id: string;
  name: string;
  isEnabled: boolean;
  priority: number;
  decisionType: DecisionType;
  minTrustLevel: number | null;
  keywordsIncludeCsv: string | null;
  keywordsExcludeCsv: string | null;
  sourceIdsCsv: string | null;
  groupIdsCsv: string | null;
  createdAtUtc: string;
  createdByUserId: string | null;
  createdByUserEmail: string | null;
}

export interface CreateRuleRequest {
  name: string;
  isEnabled: boolean;
  priority: number;
  decisionType: DecisionType;
  minTrustLevel?: number | null;
  keywordsIncludeCsv?: string | null;
  keywordsExcludeCsv?: string | null;
  sourceIdsCsv?: string | null;
  groupIdsCsv?: string | null;
}

export interface UpdateRuleRequest {
  name: string;
  isEnabled: boolean;
  priority: number;
  decisionType: DecisionType;
  minTrustLevel?: number | null;
  keywordsIncludeCsv?: string | null;
  keywordsExcludeCsv?: string | null;
  sourceIdsCsv?: string | null;
  groupIdsCsv?: string | null;
}

export interface RecomputeRequest {
  fromUtc?: string;
  toUtc?: string;
  sourceId?: string;
  status?: string;
}

export interface RecomputeResult {
  processed: number;
  changed: number;
  byDecisionTypeCounts: { [key: string]: number };
}

export interface RuleDecisionResult {
  decisionType: DecisionType;
  status: ContentStatus;
  ruleId: string | null;
  ruleName: string | null;
  reason: string;
  scheduledAtUtc: string | null;
}

