import type { PersonIdentity } from "@/lib/people";
import type { LegalChecklistTemplate } from "../../legalChecklistTypes";
import type { ContractResponse } from "./operationalProjects";

export type PermitStatus =
  | "NotStarted"
  | "Preparing"
  | "Submitted"
  | "UnderReview"
  | "NeedMoreDocs"
  | "Issued"
  | "Rejected"
  | "Expired";

export const PERMIT_STATUSES: PermitStatus[] = [
  "NotStarted",
  "Preparing",
  "Submitted",
  "UnderReview",
  "NeedMoreDocs",
  "Issued",
  "Rejected",
  "Expired",
];

export interface PermitChecklistItemResponse {
  isRequired?: boolean;
  id: number;
  operationalProjectId: number;
  operationalProjectCode?: string | null;
  operationalProjectName?: string | null;
  designProjectId?: number | null;
  designProjectCode?: string | null;
  designProjectName?: string | null;
  permitTypeCode: string;
  permitTypeLabel?: string | null;
  issuingAgency?: string | null;
  ownerUserId?: number | null;
  ownerName?: string | null;
  workPackageId?: number | null;
  workTaskId?: number | null;
  approverUserId?: number | null;
  approverName?: string | null;
  progressPercent: number;
  workTaskStatus?: string | null;
  workTaskRowVersion?: string | null;
  targetDeadline?: string | null;
  submittedAt?: string | null;
  issuedAt?: string | null;
  expiresAt?: string | null;
  submittedFilePath?: string | null;
  issuedFilePath?: string | null;
  status: PermitStatus;
  note?: string | null;
  isOverdue: boolean;
  isDueSoon: boolean;
  isExpiringSoon: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface PermitChecklistRiskSummary {
  overdue: number;
  dueSoon: number;
  expiringSoon: number;
  totalOpen: number;
}

export interface PermitChecklistListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: PermitChecklistItemResponse[];
  risk: PermitChecklistRiskSummary;
}

export interface PermitChecklistHistoryResponse {
  id: number;
  fromStatus?: PermitStatus | null;
  toStatus: PermitStatus;
  action: string;
  snapshotJson: string;
  changedAt: string;
  changedByUserId: number;
  changedByName: string;
}

export interface PermitChecklistListParams {
  operationalProjectId?: number;
  designProjectId?: number;
  ownerUserId?: number;
  approverUserId?: number;
  status?: string;
  permitTypeCode?: string;
  dueSoon?: boolean;
  expiringSoon?: boolean;
  overdue?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CreatePermitChecklistItemRequest {
  operationalProjectId: number;
  designProjectId?: number | null;
  permitTypeCode: string;
  status?: PermitStatus;
  issuingAgency?: string | null;
  ownerUserId?: number | null;
  targetDeadline?: string | null;
  submittedAt?: string | null;
  issuedAt?: string | null;
  expiresAt?: string | null;
  note?: string | null;
}

export interface LegalProjectMemberOption extends PersonIdentity {
  userId: number;
  name: string;
  email: string;
  roleGroupId: number;
  roleGroupCode: string;
  canLeadLegal: boolean;
}

export interface LegalActivationPreview {
  existingLegalLeadUserId?: number | null;
  existingPlannedStart?: string | null;
  existingPlannedEnd?: string | null;
  templates: LegalChecklistTemplate[];
  appliedTemplate?: LegalChecklistTemplate | null;
  templateAppliedAt?: string | null;
  templateAppliedByUserId?: number | null;
  workPackageRowVersion?: string | null;
  contractId: number;
  operationalProjectId: number;
  projectCode: string;
  projectName: string;
  projectManagerUserId: number;
  eligible: boolean;
  alreadyActivated: boolean;
  existingWorkPackageId?: number | null;
  reasonCode?: string | null;
  message?: string | null;
  permitTypes: Array<{
    code: string;
    name: string;
    alreadyExists: boolean;
    requiresSetup: boolean;
  }>;
  projectMembers: LegalProjectMemberOption[];
}

export interface LegalPermitSetupItemRequest {
  permitTypeCode: string;
  issuingAgency?: string | null;
  assigneeUserId: number;
  approverUserId: number;
  plannedStart: string;
  plannedEnd: string;
  predecessorPermitTypeCodes: string[];
}

export interface ActivateContractWithLegalRequest {
  templateId?: number;
  templateRowVersion?: string;
  workPackageRowVersion?: string;
  rowVersion: string;
  legalLeadUserId: number;
  plannedStart: string;
  plannedEnd: string;
  permits: LegalPermitSetupItemRequest[];
}

export interface LegalActivationResult {
  contract: ContractResponse;
  workPackageId: number;
  permitIds: number[];
  reusedExistingPackage: boolean;
}

export interface UpdatePermitChecklistItemRequest {
  status?: PermitStatus;
  issuingAgency?: string | null;
  clearIssuingAgency?: boolean;
  ownerUserId?: number | null;
  clearOwner?: boolean;
  targetDeadline?: string | null;
  clearTargetDeadline?: boolean;
  submittedAt?: string | null;
  clearSubmittedAt?: boolean;
  issuedAt?: string | null;
  clearIssuedAt?: boolean;
  expiresAt?: string | null;
  clearExpiresAt?: boolean;
  note?: string | null;
  clearNote?: boolean;
}
