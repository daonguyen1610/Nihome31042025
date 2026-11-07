import type { PersonIdentity } from "@/lib/people";
import type { CsvImportError } from "./quotes";

export type TenderStatus = "Preparing" | "Submitted" | "Won" | "Lost" | "Cancelled";

export const TENDER_STATUSES: TenderStatus[] = [
  "Preparing",
  "Submitted",
  "Won",
  "Lost",
  "Cancelled",
];

export type TenderChecklistItemStatus =
  "NotStarted" | "Preparing" | "Done" | "Submitted" | "NotApplicable";

export interface TenderAssigneeOptionResponse extends PersonIdentity {
  id: number;
  fullName: string;
}

export interface TenderChecklistItemResponse {
  id: number;
  templateCode?: string | null;
  title: string;
  status: TenderChecklistItemStatus;
  isRequired: boolean;
  notApplicableReason?: string | null;
  documentKind: string;
  ownerUserId?: number | null;
  ownerName?: string | null;
  internalDeadline?: string | null;
  filePath?: string | null;
  originalFileName?: string | null;
  capabilityDocumentId?: number | null;
  sortOrder: number;
  completedAt?: string | null;
  completedByUserId?: number | null;
  verifiedAt?: string | null;
  verifiedByUserId?: number | null;
  workTaskId?: number | null;
  workPackageId?: number | null;
  currentEvidenceId?: number | null;
  evidenceRevisions: TenderChecklistEvidenceResponse[];
}

export interface TenderChecklistEvidenceResponse {
  id: number;
  revision: number;
  source: "DirectUpload" | "CapabilityLibrary" | "Legacy";
  originalFileName: string;
  fileSize: number;
  contentType: string;
  sha256?: string | null;
  capabilityDocumentId?: number | null;
  capabilityDocumentVersion?: number | null;
  createdByUserId: number;
  createdAt: string;
}

export interface TenderSubmissionResponse {
  id: number;
  sequence: number;
  submittedAt: string;
  submittedByUserId: number;
  submittedByName?: string | null;
  approvedEstimateRevisionId?: number | null;
  financeReviewWorkPackageId?: number | null;
  submissionChannel: string;
  receiptReference?: string | null;
  legacyUnknown: boolean;
  itemCount: number;
  items: TenderSubmissionItemResponse[];
}

export interface TenderSubmissionItemResponse {
  checklistItemId: number;
  templateCode?: string | null;
  title: string;
  isRequired: boolean;
  status: TenderChecklistItemStatus;
  filePath?: string | null;
  originalFileName?: string | null;
  capabilityDocumentId?: number | null;
  evidenceId?: number | null;
  capabilityDocumentVersion?: number | null;
  sha256?: string | null;
  notApplicableReason?: string | null;
}

export interface TenderResponse {
  id: number;
  code: string;
  name: string;
  customerId: number;
  customerName: string;
  operationalProjectId?: number | null;
  operationalProjectCode?: string | null;
  operationalProjectName?: string | null;
  openingDate?: string | null;
  invitationReceivedAt?: string | null;
  invitationReference?: string | null;
  invitationSource: string;
  invitationReceivedByUserId: number;
  invitationReceivedByName?: string | null;
  invitationPortalUrl?: string | null;
  submissionDeadline: string;
  preparerUserId?: number | null;
  preparerName?: string | null;
  infoSource?: string | null;
  status: TenderStatus;
  note?: string | null;
  wonOpportunityId?: number | null;
  wonOpportunityName?: string | null;
  lostReasonCode?: string | null;
  lostReasonLabel?: string | null;
  lostNote?: string | null;
  closedAt?: string | null;
  submittedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  checklistItems: TenderChecklistItemResponse[];
  submissions: TenderSubmissionResponse[];
  invitationDocuments: TenderInvitationDocumentResponse[];
  checklistCompletionPercent: number;
  isDeadlineImminent: boolean;
  milestones: TenderMilestoneResponse[];
}

export interface TenderInvitationDocumentResponse {
  id: number;
  originalFileName?: string | null;
  fileSize?: number | null;
  contentType?: string | null;
  externalUrl?: string | null;
  createdByUserId: number;
  createdAt: string;
}

export interface TenderMilestoneResponse {
  kind: "PlanningReady" | "DossierReady" | "InternalReviewReady" | "ReadyToSubmit";
  achievedAt: string;
  achievedByUserId: number;
}

export interface TenderReadinessBlockerResponse {
  code: string;
  area: string;
  message: string;
  actionPath: string;
}

export interface TenderReadinessResponse {
  tenderId: number;
  currentStep: string;
  canSubmit: boolean;
  blockers: TenderReadinessBlockerResponse[];
  milestones: TenderMilestoneResponse[];
}

export interface TenderListItemResponse {
  id: number;
  code: string;
  name: string;
  customerId: number;
  customerName: string;
  openingDate?: string | null;
  invitationReceivedAt: string;
  invitationReference?: string | null;
  invitationSource: string;
  invitationReceivedByUserId?: number | null;
  invitationPortalUrl?: string | null;
  submissionDeadline: string;
  preparerUserId?: number | null;
  preparerName?: string | null;
  status: TenderStatus;
  checklistCompletionPercent: number;
  isDeadlineImminent: boolean;
  updatedAt: string;
}

export interface TenderListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: TenderListItemResponse[];
}

export interface CreateTenderRequest {
  name: string;
  customerId: number;
  operationalProjectId?: number | null;
  openingDate?: string | null;
  invitationReceivedAt: string;
  invitationReference?: string | null;
  invitationSource: string;
  invitationReceivedByUserId?: number | null;
  invitationPortalUrl?: string | null;
  submissionDeadline: string;
  preparerUserId?: number | null;
  infoSource?: string | null;
  note?: string | null;
}

export interface UpdateTenderRequest {
  name: string;
  operationalProjectId?: number | null;
  openingDate?: string | null;
  invitationReceivedAt?: string | null;
  invitationReference?: string | null;
  invitationSource?: string | null;
  invitationReceivedByUserId?: number | null;
  invitationPortalUrl?: string | null;
  submissionDeadline: string;
  preparerUserId?: number | null;
  infoSource?: string | null;
  note?: string | null;
}

export interface TenderStatisticsParams {
  fromDate?: string;
  toDate?: string;
  customerId?: number;
  preparerUserId?: number;
}

export interface TenderStatisticsResponse {
  submittedCount: number;
  wonCount: number;
  lostCount: number;
  pendingCount: number;
  cancelledCount: number;
  winRate: number | null;
  missingSubmissionCount: number;
  calculatedAt: string;
}

export interface TenderListParams extends TenderStatisticsParams {
  submittedOnly?: boolean;
  status?: string;
  openingMonth?: number;
  openingYear?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface UpdateTenderChecklistItemRequest {
  status?: TenderChecklistItemStatus;
  ownerUserId?: number | null;
  clearOwner?: boolean;
  internalDeadline?: string | null;
  clearInternalDeadline?: boolean;
  isRequired?: boolean;
  notApplicableReason?: string | null;
  workTaskId?: number | null;
  clearWorkTask?: boolean;
}

export interface CreateTenderChecklistItemRequest {
  title: string;
  isRequired: boolean;
  documentKind?: string;
  ownerUserId?: number | null;
  workTaskId?: number | null;
  internalDeadline?: string | null;
}

export interface SubmitTenderRequest {
  submissionChannel: string;
  receiptReference?: string | null;
  note?: string | null;
  rowVersion: string;
}

export interface AttachTenderChecklistFromLibraryItem {
  checklistItemId: number;
  capabilityDocumentId: number;
}

export interface AttachTenderChecklistFromLibraryRequest {
  items: AttachTenderChecklistFromLibraryItem[];
}

export interface MarkTenderWonRequest {
  opportunityId?: number | null;
  createOpportunity?: boolean;
  note?: string | null;
}

export interface MarkTenderLostRequest {
  reasonCode: string;
  note?: string | null;
}

export interface TenderTimelineEvent {
  id: number;
  occurredAt: string;
  action: string;
  message?: string | null;
  userId?: number | null;
  userName?: string | null;
}

export type TenderEstimateStatus = "Draft" | "Submitted" | "Approved" | "Rejected";

export interface TenderEstimateLineResponse {
  id: number;
  itemCode: string;
  description: string;
  unit: string;
  quantity: number;
  unitCost: number;
  bidUnitPrice: number;
  costAmount: number;
  bidAmount: number;
  note?: string | null;
  sortOrder: number;
}

export interface TenderEstimateRevisionResponse {
  id: number;
  tenderId: number;
  versionNumber: number;
  status: TenderEstimateStatus;
  currency: string;
  vatPercent: number;
  costSubtotal: number;
  bidSubtotal: number;
  vatAmount: number;
  grandBidTotal: number;
  sourceFileName: string;
  sourceSha256: string;
  importedByUserId: number;
  importedAt: string;
  submittedByUserId?: number | null;
  submittedAt?: string | null;
  approvedByUserId?: number | null;
  approvedAt?: string | null;
  rejectedByUserId?: number | null;
  rejectedAt?: string | null;
  note?: string | null;
  lines: TenderEstimateLineResponse[];
}

export interface TenderEstimateImportResponse {
  revision?: TenderEstimateRevisionResponse | null;
  errors: CsvImportError[];
}

export interface TransitionTenderRequest {
  status: TenderStatus;
  opportunityId?: number | null;
  reasonCode?: string | null;
  note?: string | null;
}
