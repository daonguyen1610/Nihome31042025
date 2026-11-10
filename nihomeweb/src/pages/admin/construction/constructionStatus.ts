import type {
  AcceptanceStatus,
  AsBuiltStatus,
  ConstructionTaskStatus,
  ConstructionWorkflowStatus,
  HandoverStatus,
  HseViolationSeverity,
  HseViolationStatus,
  PunchSeverity,
  PunchStatus,
  SiteDiaryStatus,
} from "@/services/adminApi";

/** Semantic tone for a construction status. Colour is never the only signal. */
export type StatusTone = "muted" | "info" | "warning" | "success" | "indigo" | "danger";

export const STATUS_TONE_CLASS: Record<StatusTone, string> = {
  muted: "status-pill chip-muted",
  info: "status-pill chip-info",
  warning: "status-pill chip-warning",
  success: "status-pill chip-success",
  indigo: "status-pill chip-indigo",
  danger: "status-pill chip-danger",
};

export const statusToneClass = (tone: StatusTone): string => STATUS_TONE_CLASS[tone];

/** Tone colours without the pill shape, for bars, tiles and markers. */
export const STATUS_SURFACE_CLASS: Record<StatusTone, string> = {
  muted: "chip-muted",
  info: "chip-info",
  warning: "chip-warning",
  success: "chip-success",
  indigo: "chip-indigo",
  danger: "chip-danger",
};

export const REVIEW_STATUS_TONE: Record<ConstructionWorkflowStatus, StatusTone> = {
  Draft: "muted",
  PendingCmReview: "info",
  CmApproved: "success",
  CmRejected: "danger",
};

export const ACCEPTANCE_STATUS_TONE: Record<AcceptanceStatus, StatusTone> = {
  Draft: "muted",
  Submitted: "info",
  Approved: "success",
  Rejected: "danger",
  Cancelled: "muted",
};

export const PUNCH_STATUS_TONE: Record<PunchStatus, StatusTone> = {
  Open: "muted",
  InProgress: "info",
  Fixed: "warning",
  Verified: "success",
  Cancelled: "muted",
};

export const PUNCH_SEVERITY_TONE: Record<PunchSeverity, StatusTone> = {
  Low: "success",
  Medium: "info",
  High: "warning",
  Critical: "danger",
};

export const HSE_STATUS_TONE: Record<HseViolationStatus, StatusTone> = {
  Draft: "muted",
  Reported: "info",
  Confirmed: "warning",
  Remediated: "indigo",
  Closed: "success",
  Rejected: "danger",
  Cancelled: "muted",
};

export const HSE_SEVERITY_TONE: Record<HseViolationSeverity, StatusTone> = {
  Low: "success",
  Medium: "info",
  High: "warning",
  Critical: "danger",
};

export const OVERDUE_TONE: StatusTone = "warning";

export const ASBUILT_STATUS_TONE: Record<AsBuiltStatus, StatusTone> = {
  Draft: "muted",
  Submitted: "info",
  Approved: "success",
  Archived: "indigo",
  Cancelled: "muted",
};

export const TASK_STATUS_TONE: Record<ConstructionTaskStatus, StatusTone> = {
  Planned: "muted",
  InProgress: "info",
  Completed: "success",
  Cancelled: "danger",
  OnHold: "warning",
  WaitingForDepartment: "indigo",
};

export const HANDOVER_STATUS_TONE: Record<HandoverStatus, StatusTone> = {
  Draft: "muted",
  ReadyForHandover: "info",
  HandedOver: "success",
  Reopened: "warning",
  Cancelled: "muted",
};

export const DIARY_STATUS_TONE: Record<SiteDiaryStatus, StatusTone> = {
  Draft: "muted",
  Submitted: "info",
  Confirmed: "success",
};
