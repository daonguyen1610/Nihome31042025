import type { ContractDirection, ContractType } from "./operationalProjects";
import type { ContractStatus } from "./contracts";

export interface ProjectBoqLineResponse {
  id: number;
  itemCode: string;
  description: string;
  unit: string;
  approvedQuantity: number;
  budgetUnitPrice: number;
  amount: number;
}

export interface ProjectBoqRevisionResponse {
  id: number;
  operationalProjectId: number;
  upstreamContractId?: number | null;
  upstreamContractNumber?: string | null;
  revisionNumber: number;
  currency: string;
  status: string;
  sourceTenderEstimateRevisionId?: number | null;
  sourceContractAppendixId?: number | null;
  costTotal: number;
  preparedByUserId: number;
  preparedByName?: string | null;
  submittedAt?: string | null;
  approvedAt?: string | null;
  rejectedAt?: string | null;
  decisionReason?: string | null;
  isFinal: boolean;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  lines: ProjectBoqLineResponse[];
}

export interface ProjectBoqRevisionListItemResponse {
  id: number;
  operationalProjectId: number;
  upstreamContractId?: number | null;
  upstreamContractNumber?: string | null;
  revisionNumber: number;
  currency: string;
  status: string;
  costTotal: number;
  lineCount: number;
  itemCodes: string[];
  preparedByUserId: number;
  preparedByName?: string | null;
  submittedAt?: string | null;
  approvedAt?: string | null;
  rejectedAt?: string | null;
  isFinal: boolean;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
}

export interface ProjectBoqRevisionListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: ProjectBoqRevisionListItemResponse[];
}

export interface ProjectBoqRevisionListParams {
  search?: string;
  status?: string;
  sortBy?: "revision" | "status" | "total" | "preparedBy" | "createdAt" | "updatedAt";
  sortDirection?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export interface MaterialRequestLineResponse {
  id: number;
  projectBoqLineId: number;
  itemCode: string;
  description: string;
  unit: string;
  requestedQuantity: number;
  receivedQuantity: number;
  boqApprovedQuantity: number;
  boqRemainingQuantity: number;
}

export interface MaterialRequestResponse {
  id: number;
  operationalProjectId: number;
  code: string;
  status: string;
  siteRequesterUserId: number;
  siteRequesterName?: string | null;
  responsibleSiteUserId: number;
  responsibleSiteUserName?: string | null;
  assignedProcurementUserId: number;
  assignedProcurementUserName?: string | null;
  requiredAt: string;
  note?: string | null;
  submittedAt?: string | null;
  submittedByUserId?: number | null;
  submittedByName?: string | null;
  approvedAt?: string | null;
  approvedByUserId?: number | null;
  approvedByName?: string | null;
  rejectedAt?: string | null;
  rejectedByUserId?: number | null;
  rejectedByName?: string | null;
  fulfilledAt?: string | null;
  cancelledAt?: string | null;
  decisionReason?: string | null;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  lines: MaterialRequestLineResponse[];
}

export interface MaterialRequestDetailResponse extends MaterialRequestResponse {
  operationalProjectCode: string;
  operationalProjectName: string;
  customerId: number;
  customerName: string;
  contracts: Array<{
    id: number;
    contractNumber: string;
    direction: ContractDirection;
    type: ContractType;
    vendorId?: number | null;
    vendorName?: string | null;
    status: ContractStatus;
  }>;
}

export interface MaterialRequestBoqLineOptionResponse {
  id: number;
  itemCode: string;
  description: string;
  unit: string;
  approvedQuantity: number;
  remainingQuantity: number;
}

export interface MaterialRequestBoqContextResponse {
  revisionId: number;
  revisionNumber: number;
  lines: MaterialRequestBoqLineOptionResponse[];
}

export interface MaterialRequestListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: MaterialRequestResponse[];
  currentApprovedBoq?: MaterialRequestBoqContextResponse | null;
}

export interface MaterialRequestListParams {
  search?: string;
  status?: string;
  siteRequesterUserId?: number;
  responsibleSiteUserId?: number;
  assignedProcurementUserId?: number;
  requiredFrom?: string;
  requiredTo?: string;
  sortBy?: "code" | "status" | "requiredAt" | "updatedAt";
  sortDirection?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export interface ProcurementContractLineResponse {
  id: number;
  contractId: number;
  contractNumber: string;
  projectBoqLineId: number;
  itemCode: string;
  procurementOwnerUserId: number;
  procurementOwnerName?: string | null;
  quantity: number;
  budgetUnitPrice: number;
  negotiatedUnitPrice: number;
  rowVersion: string;
}

export interface WarehouseReceiptLineResponse {
  id: number;
  materialRequestLineId: number;
  contractLineId?: number | null;
  materialRequestCode: string;
  itemCode: string;
  description: string;
  unit: string;
  requestedQuantity: number;
  netReceivedQuantity: number;
  contractNumber?: string | null;
  vendorName?: string | null;
  receivedQuantity: number;
}

export interface WarehouseReceiptResponse {
  id: number;
  operationalProjectId: number;
  code: string;
  status: string;
  reversalOfReceiptId?: number | null;
  reversalTransactionId?: number | null;
  receivedByUserId: number;
  receivedByName?: string | null;
  inspectedAt: string;
  postedAt?: string | null;
  postedByUserId?: number | null;
  postedByName?: string | null;
  reversalReason?: string | null;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  lines: WarehouseReceiptLineResponse[];
}

export interface WarehouseIssueLineResponse {
  id: number;
  projectBoqLineId: number;
  itemCode: string;
  description: string;
  unit: string;
  boqApprovedQuantity: number;
  stockOnHand: number;
  issuedQuantity: number;
}

export interface WarehouseIssueResponse {
  id: number;
  operationalProjectId: number;
  code: string;
  status: string;
  reversalOfIssueId?: number | null;
  reversalTransactionId?: number | null;
  responsibleSiteUserId: number;
  responsibleSiteUserName?: string | null;
  issuedByUserId: number;
  issuedByName?: string | null;
  issuedAt: string;
  postedAt?: string | null;
  postedByUserId?: number | null;
  postedByName?: string | null;
  workItemCode?: string | null;
  reversalReason?: string | null;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  lines: WarehouseIssueLineResponse[];
}

export interface WarehouseBusinessContext {
  operationalProjectCode: string;
  operationalProjectName: string;
  customerId: number;
  customerName: string;
  contracts: MaterialRequestDetailResponse["contracts"];
}

export interface WarehouseReceiptDetailResponse
  extends WarehouseReceiptResponse, WarehouseBusinessContext {}

export interface WarehouseIssueDetailResponse
  extends WarehouseIssueResponse, WarehouseBusinessContext {}

export interface WarehouseTransactionListItemResponse {
  id: number;
  operationalProjectId: number;
  type: "Receipt" | "Issue";
  code: string;
  status: string;
  occurredAt: string;
  postedAt?: string | null;
  actorUserId: number;
  actorName?: string | null;
  responsibleUserId?: number | null;
  responsibleUserName?: string | null;
  workItemCode?: string | null;
  reversalOfId?: number | null;
  reversalReason?: string | null;
  lineCount: number;
  totalQuantity: number;
  itemCodes: string[];
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
}

export interface WarehouseStockItemResponse {
  projectBoqLineId: number;
  itemCode: string;
  description: string;
  unit: string;
  boqApprovedQuantity: number;
  receivedQuantity: number;
  issuedQuantity: number;
  onHandQuantity: number;
}

export interface WarehouseTransactionListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: WarehouseTransactionListItemResponse[];
  stock: WarehouseStockItemResponse[];
}

export interface WarehouseTransactionListParams {
  search?: string;
  status?: string;
  type?: "receipt" | "issue";
  responsibleUserId?: number;
  occurredFrom?: string;
  occurredTo?: string;
  sortBy?: "occurredAt" | "code" | "status" | "type" | "updatedAt";
  sortDirection?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export type MaterialAlertType = "OverBoq" | "Shortage";

export type MaterialAlertStatus = "Open" | "Acknowledged" | "Resolved";

export type MaterialAlertSeverity = "Warning" | "Critical";

export interface MaterialAlertEventResponse {
  id: number;
  type: string;
  fromStatus?: string | null;
  toStatus: MaterialAlertStatus;
  reason?: string | null;
  changedByUserId: number;
  changedByName?: string | null;
  changedAt: string;
}

export interface MaterialAlertResponse {
  id: number;
  operationalProjectId: number;
  operationalProjectCode: string;
  operationalProjectName: string;
  customerId: number;
  customerName: string;
  projectBoqLineId?: number | null;
  code: string;
  type: MaterialAlertType;
  status: MaterialAlertStatus;
  severity: MaterialAlertSeverity;
  itemCode: string;
  description: string;
  unit: string;
  boqAllowance: number;
  requiredQuantity: number;
  receivedQuantity: number;
  issuedQuantity: number;
  onHandQuantity: number;
  varianceQuantity: number;
  sourceEntityType: string;
  sourceEntityId: number;
  assignedToUserId: number;
  assignedToUserName?: string | null;
  detectedAt: string;
  acknowledgedAt?: string | null;
  acknowledgedByUserId?: number | null;
  acknowledgedByName?: string | null;
  acknowledgementNote?: string | null;
  resolvedAt?: string | null;
  lastEvaluatedAt: string;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  contracts: MaterialRequestDetailResponse["contracts"];
  events: MaterialAlertEventResponse[];
}

export interface MaterialAlertListResponse {
  total: number;
  page: number;
  pageSize: number;
  statusCounts: Partial<Record<MaterialAlertStatus, number>>;
  items: MaterialAlertResponse[];
}

export interface MaterialAlertListParams {
  search?: string;
  type?: MaterialAlertType;
  status?: MaterialAlertStatus;
  severity?: MaterialAlertSeverity;
  assignedToUserId?: number;
  sortBy?: "detectedAt" | "updatedAt" | "severity" | "variance" | "itemCode";
  sortDirection?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export interface WarehouseReceiptUpsertRequest {
  inspectedAt: string;
  receivedByUserId: number;
  lines: Array<{
    materialRequestLineId: number;
    contractLineId?: number | null;
    receivedQuantity: number;
  }>;
  rowVersion?: string;
}

export interface WarehouseIssueUpsertRequest {
  issuedAt: string;
  responsibleSiteUserId: number;
  issuedByUserId: number;
  workItemCode?: string | null;
  lines: Array<{ projectBoqLineId: number; issuedQuantity: number }>;
  rowVersion?: string;
}

export interface VendorRatingResponse {
  id: number;
  operationalProjectId: number;
  contractId: number;
  contractNumber: string;
  vendorId: number;
  vendorName: string;
  versionNumber: number;
  status: string;
  procurementOwnerUserId: number;
  procurementOwnerName?: string | null;
  qualityScore: number;
  scheduleScore: number;
  costScore: number;
  hseScore: number;
  overallScore: number;
  comments?: string | null;
  preparedByUserId: number;
  submittedAt?: string | null;
  approvedAt?: string | null;
  decisionReason?: string | null;
  supersedesVendorRatingId?: number | null;
  rowVersion: string;
}

export interface ProcurementWorkspaceResponse {
  boqRevisions: ProjectBoqRevisionResponse[];
  materialRequests: MaterialRequestResponse[];
  contractLines: ProcurementContractLineResponse[];
  receipts: WarehouseReceiptResponse[];
  issues: WarehouseIssueResponse[];
  vendorRatings: VendorRatingResponse[];
}

export interface ProjectBoqRevisionRequest {
  upstreamContractId: number;
  currency: string;
  sourceTenderEstimateRevisionId?: number | null;
  sourceContractAppendixId?: number | null;
  lines: Array<{
    itemCode: string;
    description: string;
    unit: string;
    approvedQuantity: number;
    budgetUnitPrice: number;
  }>;
  rowVersion?: string;
}

export interface MaterialRequestUpsertRequest {
  responsibleSiteUserId: number;
  assignedProcurementUserId: number;
  requiredAt: string;
  note?: string | null;
  lines: Array<{ projectBoqLineId: number; requestedQuantity: number }>;
  rowVersion?: string;
}

export interface ProcurementContractLineRequest {
  contractId: number;
  projectBoqLineId: number;
  procurementOwnerUserId: number;
  quantity: number;
  negotiatedUnitPrice: number;
  rowVersion?: string;
}

export interface VendorRatingUpsertRequest {
  contractId: number;
  qualityScore: number;
  scheduleScore: number;
  costScore: number;
  hseScore: number;
  comments?: string | null;
  rowVersion?: string;
}
