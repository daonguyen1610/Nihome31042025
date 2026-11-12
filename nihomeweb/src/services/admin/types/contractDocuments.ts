import type { ContractStatus } from "./contracts";
import type { OpportunityStage } from "./opportunities";
import type { DesignProjectResponse } from "./designProjects";
import type { PersonIdentity } from "@/lib/people";

export type ContractAppendixStatus = "Draft" | "Submitted" | "Approved" | "Rejected";

export const CONTRACT_APPENDIX_STATUSES: ContractAppendixStatus[] = [
  "Draft",
  "Submitted",
  "Approved",
  "Rejected",
];

export interface ContractAppendixFileInfo {
  filePath: string;
  originalFileName?: string | null;
  fileSize?: number | null;
  contentType?: string | null;
}

export interface ContractAppendixResponse {
  id: number;
  contractId: number;
  voNumber: number;
  title: string;
  reason: string;
  valueDelta: number;
  filePath?: string | null;
  originalFileName?: string | null;
  fileSize?: number | null;
  contentType?: string | null;
  files?: ContractAppendixFileInfo[];
  status: ContractAppendixStatus;
  submittedAt?: string | null;
  submittedByUserId?: number | null;
  submittedByName?: string | null;
  decidedAt?: string | null;
  decidedByUserId?: number | null;
  decidedByName?: string | null;
  decisionNote?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface UpsertContractAppendixRequest {
  title: string;
  reason: string;
  valueDelta: number;
  files?: ContractAppendixFileInfo[];
  filePath?: string | null;
  originalFileName?: string | null;
  fileSize?: number | null;
  contentType?: string | null;
}

export type ContractAttachmentKind = "SignedScan" | "Supporting";

export const CONTRACT_ATTACHMENT_KINDS: ContractAttachmentKind[] = ["SignedScan", "Supporting"];

export interface ContractAttachmentResponse {
  id: number;
  contractId: number;
  kind: ContractAttachmentKind;
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
  label?: string | null;
  createdAt: string;
  uploadedByUserId?: number | null;
  uploadedByName?: string | null;
}

export interface ContractTimelineEvent {
  id: number;
  occurredAt: string;
  action: string;
  message?: string | null;
  userId?: number | null;
  userName?: string | null;
}

export interface AppendixUploadResponse {
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
}

export type OpportunityActivityType = "Call" | "Email" | "Meeting" | "Note" | "StageChange";

export interface OpportunityActivityResponse {
  id: number;
  type: OpportunityActivityType;
  occurredAt: string;
  content: string;
  createdByUserId: number;
  createdByName?: string;
  createdAt: string;
}

export interface OpportunityContractLinkResponse {
  id: number;
  contractNumber: string;
  quoteId?: number | null;
  status: ContractStatus;
  signedDate?: string | null;
}

export interface OpportunityResponse {
  sourceContractId?: number | null;
  continuationKind?: string | null;
  continuationReason?: string | null;

  id: number;
  name: string;
  customerId: number;
  customerName?: string;
  operationalProjectId?: number | null;
  designProjectId?: number | null;
  designProjectCode?: string | null;
  ownerUserId?: number;
  ownerName?: string;
  estimatedValue: number;
  currency: string;
  winProbability: number;
  expectedCloseDate?: string;
  stage: OpportunityStage;
  lostReasonCode?: string;
  lostNote?: string;
  closedAt?: string;
  wonQuoteId?: number;
  wonTenderId?: number;
  note?: string;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  activities: OpportunityActivityResponse[];
  contracts: OpportunityContractLinkResponse[];
}

export interface OpportunityDesignStartResponse {
  created: boolean;
  designProject: DesignProjectResponse;
}

export interface OpportunityOwnerOption extends PersonIdentity {
  id: number;
  fullName: string;
}

export interface OpportunityHistoryItem {
  id: number;
  createdAt: string;
  action: string;
  message: string;
  actorName?: string | null;
  status: string;
}

export interface OpportunityListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: OpportunityResponse[];
}

export interface OpportunityPipelineColumn {
  stage: OpportunityStage;
  count: number;
  totalValue: number;
  items: OpportunityResponse[];
}

export interface OpportunityPipelineResponse {
  columns: OpportunityPipelineColumn[];
}

export interface CreateOpportunityRequest {
  sourceContractId?: number | null;
  continuationKind?: string | null;
  continuationReason?: string | null;
  sourceContractRowVersion?: string;

  name: string;
  customerId: number;
  operationalProjectId?: number | null;
  ownerUserId?: number | null;
  estimatedValue: number;
  winProbability: number;
  expectedCloseDate?: string | null;
  stage?: OpportunityStage;
  note?: string;
}

export interface UpdateOpportunityRequest {
  rowVersion?: string;
  name: string;
  customerId: number;
  operationalProjectId?: number | null;
  ownerUserId?: number | null;
  estimatedValue: number;
  winProbability: number;
  expectedCloseDate?: string | null;
  note?: string;
}

export interface ChangeOpportunityStageRequest {
  rowVersion?: string;
  targetStage: OpportunityStage;
  wonQuoteId?: number;
  wonTenderId?: number;
  lostReasonCode?: string;
  lostNote?: string;
}

export interface AddOpportunityActivityRequest {
  type: OpportunityActivityType;
  content: string;
  occurredAt?: string;
}

export interface OpportunityListParams {
  stage?: OpportunityStage;
  customerId?: number;
  ownerUserId?: number;
  expectedCloseFrom?: string;
  expectedCloseTo?: string;
  minValue?: number;
  maxValue?: number;
  search?: string;
  page?: number;
  pageSize?: number;
  quoteEligibleOnly?: boolean;
}

export interface OpportunityPipelineParams {
  ownerUserId?: number;
  customerId?: number;
  expectedCloseFrom?: string;
  expectedCloseTo?: string;
  minValue?: number;
  maxValue?: number;
}
