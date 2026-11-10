import type { OpportunityStage } from "./opportunities";
import type { QuoteStatus, QuoteMethod } from "./quotes";
import type { ContractStatus } from "./contracts";
import type { PersonIdentity } from "@/lib/people";
import type { VendorResponse } from "./vendors";

export type OperationalProjectStatus = "Planning" | "Active" | "OnHold" | "Completed" | "Cancelled";

export const OPERATIONAL_PROJECT_STATUSES: OperationalProjectStatus[] = [
  "Planning",
  "Active",
  "OnHold",
  "Completed",
  "Cancelled",
];

export type WorkPackageKind = "Project" | "Contract" | "Internal";

export type WorkPackagePurpose =
  | "General"
  | "ProcurementInputPricing"
  | "FinanceCommercialReview"
  | "FinanceTenderReview"
  | "TenderPlanning"
  | "LegalPermitting";

export type WorkPackageStatus = "Planned" | "Active" | "Completed" | "Cancelled";

export type WorkTaskStatus = "Planned" | "InProgress" | "Completed" | "Verified" | "Cancelled";

export interface DepartmentWorkTaskResponse {
  id: number;
  workPackageId: number;
  parentTaskId?: number | null;
  hasSubtasks: boolean;
  wbsCode: string;
  title: string;
  description?: string | null;
  resources?: string | null;
  departmentRoleGroupId: number;
  departmentCode: string;
  departmentLabelKey: string;
  assigneeUserId: number;
  assigneeName: string;
  approverUserId?: number | null;
  approverName?: string | null;
  kpiDefinitionId?: number | null;
  projectScheduleNodeId?: string | null;
  kpiDefinitionCode?: string | null;
  status: WorkTaskStatus;
  progressPercent: number;
  plannedStart: string;
  plannedEnd: string;
  actualStart?: string | null;
  completedAt?: string | null;
  verifiedAt?: string | null;
  verifiedByUserId?: number | null;
  ccUserIds: number[];
  predecessorTaskIds: number[];
  successorTaskIds: number[];
  canUpdateProgress: boolean;
  canVerify: boolean;
  rowVersion: string;
}

export interface DepartmentWorkTaskAttachmentResponse {
  id: number;
  workTaskId: number;
  originalFileName: string;
  fileSize: number;
  createdAt: string;
}

export interface DepartmentWorkPackageResponse {
  id: number;
  code: string;
  kind: WorkPackageKind;
  purpose: WorkPackagePurpose;
  name: string;
  description?: string | null;
  operationalProjectId?: number | null;
  operationalProjectCode?: string | null;
  operationalProjectName?: string | null;
  contractId?: number | null;
  contractNumber?: string | null;
  sourceOpportunityId?: number | null;
  sourceOpportunityName?: string | null;
  sourceQuoteId?: number | null;
  sourceQuoteCode?: string | null;
  sourceQuoteVersion?: number | null;
  sourceTenderId?: number | null;
  sourceTenderCode?: string | null;
  departmentRoleGroupId: number;
  departmentCode: string;
  departmentLabelKey: string;
  participantDepartments: {
    id: number;
    code: string;
    labelKey: string;
    canReceiveProjectPackage: boolean;
  }[];
  leadUserId: number;
  leadUserName: string;
  status: WorkPackageStatus;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  completedAt?: string | null;
  progressPercent: number;
  canManagePackage: boolean;
  canAssignTasks: boolean;
  rowVersion: string;
  tasks: DepartmentWorkTaskResponse[];
  inputPriceOffers: InputPriceOfferResponse[];
  financeReview?: FinanceReviewResponse | null;
  approvalEmails: {
    id: number;
    recordType: string;
    recordId: number;
    toEmail: string;
    ccEmails: string[];
    status: string;
    createdAt: string;
    sentAt: string | null;
  }[];
}

export type BusinessReviewStatus = "Draft" | "Submitted" | "Approved" | "Rejected";

export interface InputPriceLineResponse {
  id: number;
  itemCode: string;
  description: string;
  unit: string;
  quantity: number;
  unitPrice: number;
  vatPercent: number;
  amount: number;
  sortOrder: number;
}

export interface InputPriceOfferResponse {
  id: number;
  workPackageId: number;
  vendorId: number;
  vendorCode: string;
  vendorName: string;
  vendorType: string;
  supplierReference: string;
  currency: string;
  validUntil: string;
  status: BusinessReviewStatus;
  note?: string | null;
  decisionReason?: string | null;
  subtotal: number;
  vatAmount: number;
  grandTotal: number;
  createdByUserId: number;
  lastEditedByUserId: number;
  rowVersion: string;
  lines: InputPriceLineResponse[];
}

export interface FinanceReviewResponse {
  id: number;
  workPackageId: number;
  status: BusinessReviewStatus;
  reviewedQuoteVersion: number | null;
  contractConditionsReviewed: boolean;
  guaranteesReviewed: boolean;
  paymentTermsReviewed: boolean;
  financialDossierReviewed: boolean;
  findings?: string | null;
  decisionReason?: string | null;
  createdByUserId: number;
  lastEditedByUserId: number;
  reviewedByUserId?: number | null;
  reviewedByName?: string | null;
  rowVersion: string;
}

export interface DepartmentWorkPackageListResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  items: DepartmentWorkPackageResponse[];
}

export interface DepartmentWorkPackageOptionsResponse {
  departments: { id: number; code: string; labelKey: string; canReceiveProjectPackage: boolean }[];
  users: {
    userId: number;
    name: string;
    email: string;
    roleGroupId: number;
    canLead: boolean;
    roleCode: string;
    roleName: string;
    roleLabelKey?: string | null;
    departmentLabelKey?: string | null;
  }[];
  kpiDefinitions: { id: number; code: string; nameKey: string }[];
  vendors: { id: number; code: string; name: string; type: string }[];
}

export interface DepartmentWorkPackageQuery {
  kind?: WorkPackageKind;
  operationalProjectId?: number;
  contractId?: number;
  sourceOpportunityId?: number;
  sourceQuoteId?: number;
  sourceTenderId?: number;
  purpose?: WorkPackagePurpose;
  departmentRoleGroupId?: number;
  status?: WorkPackageStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateDepartmentWorkPackageRequest {
  kind: WorkPackageKind;
  purpose: WorkPackagePurpose;
  name: string;
  description?: string | null;
  operationalProjectId?: number | null;
  contractId?: number | null;
  sourceOpportunityId?: number | null;
  sourceQuoteId?: number | null;
  sourceTenderId?: number | null;
  departmentRoleGroupId: number;
  participantDepartmentRoleGroupIds: number[];
  leadUserId: number;
  plannedStart?: string | null;
  plannedEnd?: string | null;
}

export interface UpsertInputPriceOfferRequest {
  vendorId: number;
  supplierReference: string;
  currency: string;
  validUntil: string;
  note?: string | null;
  rowVersion?: string | null;
  lines: Array<{
    itemCode: string;
    description: string;
    unit: string;
    quantity: number;
    unitPrice: number;
    vatPercent: number;
    sortOrder: number;
  }>;
}

export interface UpsertFinanceReviewRequest {
  contractConditionsReviewed: boolean;
  guaranteesReviewed: boolean;
  paymentTermsReviewed: boolean;
  financialDossierReviewed: boolean;
  findings?: string | null;
  rowVersion?: string | null;
}

export interface UpdateDepartmentWorkPackageRequest {
  name: string;
  description?: string | null;
  contractId?: number | null;
  status: WorkPackageStatus;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  rowVersion: string;
}

export interface UpsertDepartmentWorkTaskRequest {
  parentTaskId?: number | null;
  wbsCode: string;
  title: string;
  description?: string | null;
  resources?: string | null;
  departmentRoleGroupId?: number | null;
  assigneeUserId: number;
  approverUserId?: number | null;
  kpiDefinitionId?: number | null;
  projectScheduleNodeId?: string | null;
  plannedStart: string;
  plannedEnd: string;
  ccUserIds: number[];
  predecessorTaskIds: number[];
  rowVersion?: string;
}

export type DeletionImpactAction = "Delete" | "Unlink" | "Block";

export interface DeletionImpactLinkResponse {
  label: string;
  url: string;
}

export interface DeletionImpactItemResponse {
  key: string;
  action: DeletionImpactAction;
  count: number;
  examples: string[];
  resolutionLinks?: DeletionImpactLinkResponse[];
  resolutionUrl?: string | null;
}

export interface DeletionImpactResponse {
  resourceType: string;
  resourceId: number;
  resourceLabel: string;
  requiredConfirmation: string;
  planToken: string;
  canDelete: boolean;
  totalAffected: number;
  items: DeletionImpactItemResponse[];
}

export interface ConfirmDeletionRequest {
  planToken: string;
  confirmation: string;
  rowVersion?: string;
}

export type HardDeleteOperationStatus =
  "Preparing" | "Ready" | "Processing" | "Completed" | "ManualActionRequired" | "Failed";

export interface HardDeleteOperationResult {
  operationId: string;
  status: HardDeleteOperationStatus;
  isComplete: boolean;
  requiresManualAction: boolean;
  errorCode?: string | null;
  errorMessage?: string | null;
}

export interface OperationalProjectListItemResponse {
  id: number;
  code: string;
  name: string;
  customerId: number;
  customerName: string;
  projectManagerUserId?: number | null;
  projectManagerName?: string | null;
  status: OperationalProjectStatus;
  startDate?: string | null;
  endDate?: string | null;
  opportunityCount: number;
  quoteCount: number;
  contractCount: number;
  updatedAt: string;
}

export interface OperationalProjectResponse extends OperationalProjectListItemResponse {
  designProjects: Array<{
    id: number;
    projectCode: string;
    name: string;
    contractId?: number | null;
    status: string;
  }>;
  note?: string | null;
  designProjectId?: number | null;
  designProjectCode?: string | null;
  rowVersion: string;
  createdAt: string;
  latestCodeChange?: {
    id: number;
    oldCode: string;
    newCode: string;
    reason: string;
    status: "Pending" | "Processing" | "Succeeded" | "Failed" | "NotRequired";
    attemptCount: number;
    error?: string | null;
    changedAt: string;
    renamedAt?: string | null;
  } | null;
  contractSummary: {
    activeContractCount: number;
    upstreamContractCount: number;
    upstreamCurrentValue: number;
    downstreamContractCount: number;
    downstreamCurrentValue: number;
    scheduledPaymentAmount: number;
    paidPaymentAmount: number;
    paymentProgressPercent: number;
  };
  opportunities: Array<{
    id: number;
    name: string;
    stage: OpportunityStage;
    estimatedValue: number;
    winProbability: number;
    expectedCloseDate?: string | null;
    customerName?: string | null;
    ownerName?: string | null;
    lostReasonCode?: string | null;
  }>;
  quotes: Array<{
    id: number;
    code: string;
    status: QuoteStatus;
    method: QuoteMethod;
    version: number;
    areaSqm?: number | null;
    unitPricePerSqm?: number | null;
    packageDescription?: string | null;
    subtotal: number;
    discountPercent: number;
    vatPercent: number;
    grandTotal: number;
    validUntil: string;
    isExpired: boolean;
    note?: string | null;
    customerName?: string | null;
    ownerName?: string | null;
    submittedAt?: string | null;
    approvedAt?: string | null;
    sentAt?: string | null;
    createdAt: string;
    items: Array<{
      id: number;
      itemCode?: string | null;
      name: string;
      unit: string;
      quantity: number;
      unitPrice: number;
      amount: number;
    }>;
  }>;
  contracts: Array<{
    id: number;
    contractNumber: string;
    direction: ContractDirection;
    type: ContractType;
    vendorId?: number | null;
    vendorCode?: string | null;
    vendorName?: string | null;
    status: ContractStatus;
    value: number;
    approvedVoTotal: number;
    currentValue: number;
    paymentMilestoneCount: number;
    paidMilestoneCount: number;
    scheduledPaymentAmount: number;
    paidPaymentAmount: number;
    paymentProgressPercent: number;
    signedDate?: string | null;
    startDate?: string | null;
    endDate?: string | null;
    scopeOfWork?: string | null;
    note?: string | null;
    customerName?: string | null;
    ownerName?: string | null;
    createdAt: string;
  }>;
}

export interface ProjectScheduleItemResponse {
  nodeId: string;
  parentNodeId?: string | null;
  wbsCode: string;
  title: string;
  plannedStart: string;
  plannedEnd: string;
  sortOrder: number;
}

export interface ProjectScheduleRevisionResponse {
  id: number;
  version: number;
  status: "Draft" | "PendingApproval" | "Published" | "Rejected";
  changeReason?: string | null;
  approverUserId?: number | null;
  approverName?: string | null;
  submittedAt?: string | null;
  reviewedAt?: string | null;
  reviewedByUserId?: number | null;
  reviewComment?: string | null;
  publishedAt?: string | null;
  rowVersion: string;
  items: ProjectScheduleItemResponse[];
}

export interface ProjectScheduleResponse {
  operationalProjectId: number;
  hasSignedUpstreamContract: boolean;
  publishedRevisionId?: number | null;
  approverOptions: Array<{ userId: number; name: string; email?: string | null }>;
  revisions: ProjectScheduleRevisionResponse[];
}

export interface SaveProjectScheduleItemRequest {
  nodeId?: string | null;
  parentNodeId?: string | null;
  wbsCode: string;
  title: string;
  plannedStart: string;
  plannedEnd: string;
  sortOrder: number;
}

export interface SaveProjectScheduleRevisionRequest {
  changeReason: string;
  items: SaveProjectScheduleItemRequest[];
}

export type ConstructionWorkflowStatus = "Draft" | "PendingCmReview" | "CmApproved" | "CmRejected";

export type ConstructionResourceType = "Material" | "Equipment" | "Labour";

export type ConstructionQsDirection = "Upstream" | "Downstream";

export type ConstructionQsAdjustmentKind = "Original" | "SupersedingCorrection" | "Reversal";

export type ConstructionQsRecordState = "Active" | "Superseded" | "Reversed";

export interface ConstructionProcurementResponse {
  id: number;
  operationalProjectId: number;
  code: string;
  type: "Equipment" | "Labour";
  wbsCode: string;
  needByDate: string;
  quantity: number;
  unit: string;
  reason: string;
  contractId: number | null;
  contractNumber: string | null;
  contractValid: boolean;
  suppliedAt: string | null;
  suppliedByUserId: number | null;
  supplyDocumentId: number | null;
  rowVersion: string;
  history: {
    id: number;
    previousContractId: number | null;
    contractId: number;
    supplyDocumentId: number | null;
    actorUserId: number;
    createdAt: string;
  }[];
}

export interface ConstructionResourceRequestResponse {
  id: number;
  code: string;
  type: ConstructionResourceType;
  wbsCode: string;
  needByDate: string;
  quantity: number;
  unit: string;
  reason: string;
  projectBoqLineId?: number | null;
  responsibleSiteUserId?: number | null;
  assignedProcurementUserId?: number | null;
  status: ConstructionWorkflowStatus;
  createdByUserId: number;
  lastEditedByUserId: number;
  cmDecisionReason?: string | null;
  handoffEntityType?: string | null;
  handoffEntityId?: number | null;
  handoffStatus: "NotReady" | "ReadyForProcurementAndFinance" | "MaterialRequestCreated";
  procurementContractId?: number | null;
  suppliedAt?: string | null;
  suppliedByUserId?: number | null;
  supplyDocumentId?: number | null;
  rowVersion: string;
}

export interface ConstructionResourceRequestListResponse {
  items: ConstructionResourceRequestResponse[];
}

export interface ConstructionResourceReferenceDataResponse {
  boqLines: Array<{
    id: number;
    itemCode: string;
    description: string;
    unit: string;
    approvedQuantity: number;
    remainingQuantity: number;
  }>;
  siteUsers: Array<{
    userId: number;
    userName: string;
    roleCode: string;
    roleName: string;
    roleLabelKey?: string | null;
    departmentId?: number | null;
    departmentLabelKey?: string | null;
  }>;
  procurementUsers: Array<{
    userId: number;
    userName: string;
    roleCode: string;
    roleName: string;
    roleLabelKey?: string | null;
    departmentId?: number | null;
    departmentLabelKey?: string | null;
  }>;
}

export interface CreateConstructionResourceRequest {
  type: ConstructionResourceType;
  wbsCode: string;
  needByDate: string;
  quantity: number;
  unit: string;
  reason: string;
  projectBoqLineId?: number | null;
  responsibleSiteUserId?: number | null;
  assignedProcurementUserId?: number | null;
}

export interface UpdateConstructionResourceRequest extends CreateConstructionResourceRequest {
  rowVersion: string;
}

export interface QaQcTemplateResponse {
  id: number;
  code: string;
  nameVi: string;
  name: string;
  nameZh: string;
  nameJa: string;
  version: number;
  schemaJson: string;
  isPublished: boolean;
  rowVersion: string;
}

export interface QaQcDossierResponse {
  id: number;
  code: string;
  templateId: number;
  templateVersion: number;
  templateSnapshotJson: string;
  ifcReleaseId?: number | null;
  checklistResultsJson: string;
  wbsCode: string;
  title: string;
  evidenceDocumentIds: number[];
  evidence: QaQcEvidence[];
  status: ConstructionWorkflowStatus;
  createdByUserId: number;
  lastEditedByUserId: number;
  cmDecisionReason?: string | null;
  rowVersion: string;
}

export type QaQcEvidenceType =
  | "General"
  | "AcceptanceRecord"
  | "Sample"
  | "LaboratoryResult"
  | "MaterialCertificate"
  | "SiteDiary";

export interface QaQcEvidence {
  projectDocumentId: number;
  evidenceType: QaQcEvidenceType;
}

export interface CreateQaQcDossierRequest {
  templateId: number;
  wbsCode: string;
  title: string;
  ifcReleaseId?: number | null;
  checklistResultsJson: string;
  evidenceDocumentIds?: number[];
  evidence?: QaQcEvidence[];
}

export interface UpdateQaQcDossierRequest extends CreateQaQcDossierRequest {
  rowVersion: string;
}

export interface ConstructionQsBatchResponse {
  id: number;
  code: string;
  direction: ConstructionQsDirection;
  contractId: number;
  wbsCode: string;
  periodFrom: string;
  periodTo: string;
  projectBoqRevisionId?: number | null;
  contractAppendixId?: number | null;
  contractPaymentMilestoneId?: number | null;
  certifiedAmount: number;
  description: string;
  supersedesBatchId?: number | null;
  reversesBatchId?: number | null;
  adjustmentKind: ConstructionQsAdjustmentKind;
  recordState: ConstructionQsRecordState;
  lines: ConstructionQsBatchLineResponse[];
  evidenceDocumentIds: number[];
  status: ConstructionWorkflowStatus;
  createdByUserId: number;
  lastEditedByUserId: number;
  cmDecisionReason?: string | null;
  rowVersion: string;
}

export interface CreateConstructionQsBatchRequest {
  direction: ConstructionQsDirection;
  contractId: number;
  wbsCode: string;
  periodFrom: string;
  periodTo: string;
  projectBoqRevisionId?: number | null;
  contractAppendixId?: number | null;
  contractPaymentMilestoneId?: number | null;
  certifiedAmount: number;
  description: string;
  supersedesBatchId?: number | null;
  reversesBatchId?: number | null;
  lines: ConstructionQsBatchLineRequest[];
  evidenceDocumentIds: number[];
}

export interface UpdateConstructionQsBatchRequest extends CreateConstructionQsBatchRequest {
  rowVersion: string;
}

export interface ConstructionQsBatchLineRequest {
  itemCode: string;
  description: string;
  unit: string;
  quantity: number;
  unitPrice: number;
}

export interface ConstructionQsBatchLineResponse extends ConstructionQsBatchLineRequest {
  id: number;
  amount: number;
  sortOrder: number;
}

export interface ConstructionQsReferenceDataResponse {
  contracts: Array<{
    id: number;
    contractNumber: string;
    direction: ConstructionQsDirection;
    status: string;
    paymentMilestones: Array<{
      id: number;
      order: number;
      name: string;
      amount: number;
      status: string;
    }>;
    approvedAppendices: Array<{ id: number; voNumber: number; title: string }>;
  }>;
  approvedBoqRevisions: Array<{ id: number; revisionNumber: number; currency: string }>;
}

export interface ConstructionPreparationResponse {
  id: number;
  operationalProjectId: number;
  version: number;
  status: "Draft" | "Published" | "ReadyToStart" | "InConstruction" | "Superseded";
  wbsBaselineReference: string;
  scheduleBaselineReference: string;
  sCurveBaselineReference: string;
  bchOrganizationReference: string;
  resourcePlanReference: string;
  budgetBaselineReference: string;
  wbsBaselineDocumentId?: number | null;
  scheduleBaselineDocumentId?: number | null;
  sCurveBaselineDocumentId?: number | null;
  bchOrganizationDocumentId?: number | null;
  resourcePlanDocumentId?: number | null;
  budgetBaselineDocumentId?: number | null;
  budgetBaselineLocked: boolean;
  siteHandoverRequired: boolean;
  siteHandoverComplete: boolean;
  methodStatementRequired: boolean;
  methodStatementApproved: boolean;
  hsePlanRequired: boolean;
  hsePlanApproved: boolean;
  siteHandoverDocumentId?: number | null;
  methodStatementDocumentId?: number | null;
  hsePlanDocumentId?: number | null;
  missingConditions: string[];
  readinessSnapshotJson?: string | null;
  rowVersion: string;
}

export interface ConstructionReadinessResponse {
  isReady: boolean;
  isStarted: boolean;
  blockers: Array<{ code: string; message: string }>;
  evaluatedAt: string;
  rowVersion?: string;
}

export interface UpdateConstructionPreparationRequest {
  wbsBaselineReference: string;
  scheduleBaselineReference: string;
  sCurveBaselineReference: string;
  bchOrganizationReference: string;
  resourcePlanReference: string;
  budgetBaselineReference: string;
  wbsBaselineDocumentId?: number | null;
  scheduleBaselineDocumentId?: number | null;
  sCurveBaselineDocumentId?: number | null;
  bchOrganizationDocumentId?: number | null;
  resourcePlanDocumentId?: number | null;
  budgetBaselineDocumentId?: number | null;
  budgetBaselineLocked: boolean;
  siteHandoverRequired: boolean;
  siteHandoverComplete: boolean;
  methodStatementRequired: boolean;
  methodStatementApproved: boolean;
  hsePlanRequired: boolean;
  hsePlanApproved: boolean;
  siteHandoverDocumentId?: number | null;
  methodStatementDocumentId?: number | null;
  hsePlanDocumentId?: number | null;
  rowVersion?: string;
}

export interface CreateQaQcTemplateRequest {
  code: string;
  nameVi: string;
  name: string;
  nameZh: string;
  nameJa: string;
  schemaJson: string;
}

export interface OperationalProjectTimelineItem {
  id: number;
  contractId: number;
  contractNumber: string;
  order: number;
  name: string;
  percentValue: number;
  amount: number;
  plannedDate?: string | null;
  actualDate?: string | null;
  status: PaymentMilestoneStatus;
  source: "ContractPaymentMilestone";
  note?: string | null;
  updatedAt: string;
}

export type KpiTargetDirection = "HigherIsBetter" | "LowerIsBetter";

export type KpiPeriodStatus = "Open" | "Locked";

export type KpiScoreStatus = "Available" | "MissingData" | "MissingConfiguration";

export interface KpiDefinitionResponse {
  id: number;
  code: string;
  roleCode: string;
  nameKey: string;
  sourceModule: string;
  metricCode: string;
  weight: number;
  requiresTarget: boolean;
  targetValue?: number | null;
  minimumAcceptableScore?: number | null;
  targetDirection: KpiTargetDirection;
  version: number;
  isActive: boolean;
  rowVersion: string;
}

export interface KpiUserOptionResponse {
  roleCode: string;
  roleName: string;
  roleLabelKey?: string | null;
  departmentId?: number | null;
  departmentLabelKey?: string | null;
  userId: number;
  userName: string;
  positionCode: string;
}

export interface KpiScoreResponse {
  id: number;
  definitionId: number;
  code: string;
  nameKey: string;
  sourceModule: string;
  weight: number;
  targetValue?: number | null;
  minimumAcceptableScore?: number | null;
  rawValue?: number | null;
  numerator?: number | null;
  denominator?: number | null;
  score?: number | null;
  weightedScore?: number | null;
  status: KpiScoreStatus;
  evidenceJson: string;
  definitionVersion: number;
  calculatedAt: string;
}

export interface KpiDashboardResponse {
  periodId: number;
  year: number;
  month: number;
  timeZoneId: string;
  periodStatus: KpiPeriodStatus;
  periodRowVersion: string;
  lockedAt?: string | null;
  lockedByName?: string | null;
  lockNote?: string | null;
  userId: number;
  userName: string;
  roleCode: string;
  totalScore?: number | null;
  availableWeight: number;
  isComplete: boolean;
  lastCalculatedAt?: string | null;
  scores: KpiScoreResponse[];
}

export interface OperationalProjectListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: OperationalProjectListItemResponse[];
}

export interface OperationalProjectListParams {
  customerId?: number;
  projectManagerUserId?: number;
  status?: OperationalProjectStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

export type HseViolationStatus =
  "Draft" | "Reported" | "Confirmed" | "Remediated" | "Closed" | "Rejected" | "Cancelled";

export type HseViolationSeverity = "Low" | "Medium" | "High" | "Critical";

export interface HseViolationEventResponse {
  id: number;
  type: "Created" | "Transition" | "Correction";
  fromStatus?: HseViolationStatus | null;
  toStatus: HseViolationStatus;
  reason?: string | null;
  createdByUserId: number;
  createdByName?: string | null;
  createdAt: string;
}

export interface HseViolationResponse {
  id: number;
  operationalProjectId: number;
  operationalProjectCode: string;
  operationalProjectName: string;
  code: string;
  offlineClientId: string;
  occurredAt: string;
  location: string;
  category: string;
  severity: HseViolationSeverity;
  description: string;
  regulatoryReference?: string | null;
  evidenceDocuments: string[];
  responsibleSiteUserId: number;
  responsibleSiteUserName?: string | null;
  remediationOwnerUserId?: number | null;
  remediationOwnerUserName?: string | null;
  remediationDeadline?: string | null;
  remediationNote?: string | null;
  penaltyReference?: string | null;
  penaltyAmount?: number | null;
  status: HseViolationStatus;
  reportedAt?: string | null;
  reportedByUserId?: number | null;
  reportedByName?: string | null;
  confirmedAt?: string | null;
  confirmedByUserId?: number | null;
  confirmedByName?: string | null;
  closedAt?: string | null;
  closedByUserId?: number | null;
  closedByName?: string | null;
  decisionReason?: string | null;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  events: HseViolationEventResponse[];
}

export interface HseViolationListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: HseViolationResponse[];
  statusCounts: Partial<Record<HseViolationStatus, number>>;
}

export interface HseViolationListParams {
  status?: HseViolationStatus;
  severity?: HseViolationSeverity;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface HseViolationFieldsRequest {
  offlineClientId: string;
  occurredAt: string;
  location: string;
  category: string;
  severity: HseViolationSeverity;
  description: string;
  regulatoryReference?: string | null;
  evidenceDocuments: string[];
  responsibleSiteUserId: number;
  remediationOwnerUserId?: number | null;
  remediationDeadline?: string | null;
  remediationNote?: string | null;
  penaltyReference?: string | null;
  penaltyAmount?: number | null;
}

export interface UpdateHseViolationRequest extends HseViolationFieldsRequest {
  rowVersion: string;
}

export interface TransitionHseViolationRequest {
  reason?: string | null;
  remediationNote?: string | null;
  remediationOwnerUserId?: number | null;
  remediationDeadline?: string | null;
  rowVersion: string;
}

export interface CorrectHseViolationRequest extends UpdateHseViolationRequest {
  reason: string;
}

export interface CreateOperationalProjectRequest {
  code?: string | null;
  name: string;
  customerId: number;
  projectManagerUserId?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  note?: string | null;
}

export interface UpdateOperationalProjectRequest {
  name: string;
  customerId: number;
  projectManagerUserId?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  note?: string | null;
  status: OperationalProjectStatus;
  rowVersion?: string;
}

export interface RenameOperationalProjectRequest {
  newCode: string;
  reason: string;
  rowVersion: string;
}

export interface OperationalProjectCodeSuggestionResponse {
  code: string;
}

export interface ReopenOperationalProjectRequest {
  rowVersion: string;
  reason: string;
}

export interface ProjectDocumentCategoryResponse {
  value: string;
  folderPath: string;
  translationKey: string;
}

export type ProjectDocumentSyncStatus =
  "Pending" | "Processing" | "Synced" | "Failed" | "Deleted" | "Conflict";

export type ProjectDocumentSourceType =
  "ManualUpload" | "ExistingManagedFile" | "GoogleDriveImport";

export interface ProjectDocumentResponse {
  id: number;
  operationalProjectId: number;
  category: string;
  sourceModule: string;
  sourceType: ProjectDocumentSourceType;
  sourceEntityType?: string | null;
  sourceSlot?: string | null;
  sourceRecordId?: number | null;
  customerId?: number | null;
  contractId?: number | null;
  originalFileName: string;
  contentType: string;
  size: number;
  sha256: string;
  origin: string;
  generation: number;
  desiredOperation: string;
  syncStatus: ProjectDocumentSyncStatus;
  syncAttemptCount: number;
  maxSyncAttempts: number;
  syncError?: string | null;
  nextSyncAttemptAt?: string | null;
  driveWebViewLink?: string | null;
  driveModifiedAt?: string | null;
  isDownloadable: boolean;
  unsupportedReason?: string | null;
  conflictState: "None" | "PendingConfirmation";
  conflictWithDocumentId?: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface ContractSourceLeadResponse {
  id: number;
  name: string;
  convertedAt: string;
}

export interface ContractProgressSource {
  id: number;
  code: string;
  name: string;
  status: string;
}

export interface ContractExecutionProgressResponse {
  calculatedAt: string;
  work: {
    packageCount: number;
    cancelledPackageCount: number;
    taskCount: number;
    completedTaskCount: number;
    verifiedTaskCount: number;
    cancelledTaskCount: number;
    sources: ContractProgressSource[];
  } | null;
  qs: {
    dossierCount: number;
    activeApprovedCount: number;
    pendingReviewCount: number;
    sources: ContractProgressSource[];
  } | null;
  payment: {
    milestoneCount: number;
    paidCount: number;
    requestedCount: number;
    scheduledAmount: number;
    paidScheduledAmount: number;
  };
}

export interface ContractResponse {
  sourceContractId?: number | null;
  continuationKind?: string | null;
  continuationReason?: string | null;

  id: number;
  contractNumber: string;
  customerId: number;
  customerName?: string;
  direction: ContractDirection;
  type: ContractType;
  vendorId?: number | null;
  vendorCode?: string | null;
  vendorName?: string | null;
  opportunityId?: number | null;
  opportunityTitle?: string | null;
  sourceTenderId?: number | null;
  sourceTenderCode?: string | null;
  sourceTenderName?: string | null;
  quoteId?: number | null;
  operationalProjectId?: number | null;
  operationalProjectCode?: string | null;
  operationalProjectName?: string | null;
  /** Derived from design_projects; present when a design project is attached. */
  designProjectId?: number | null;
  designProjectCode?: string | null;
  designProjectName?: string | null;
  designProjectCurrentStage?: string | null;
  quoteCode?: string | null;
  ownerUserId?: number | null;
  ownerName?: string | null;
  status: ContractStatus;
  signedDate?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  value: number;
  /** Σ of Approved VO deltas. Server-computed. */
  approvedVoTotal: number;
  /** value + approvedVoTotal. Server-computed. */
  currentValue: number;
  nextPaymentDueDate?: string | null;
  nextPaymentAmount?: number | null;
  outstandingScheduledAmount: number;
  overduePaymentMilestoneCount: number;
  /** At least one attachment of kind SignedScan exists. */
  hasSignedScan: boolean;
  attachmentCount: number;
  appendixCount: number;
  scopeOfWork?: string | null;
  note?: string | null;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  paymentMilestones: ContractPaymentMilestoneResponse[];
}

export type PaymentMilestoneStatus = "Pending" | "Requested" | "Paid";

export const PAYMENT_MILESTONE_STATUSES: PaymentMilestoneStatus[] = [
  "Pending",
  "Requested",
  "Paid",
];

export interface ContractPaymentMilestoneResponse {
  id: number;
  order: number;
  name: string;
  percentValue: number;
  amount: number;
  dueDate?: string | null;
  actualPaymentDate?: string | null;
  responsibleAccountantUserId?: number | null;
  responsibleAccountantName?: string | null;
  requestedAt?: string | null;
  status: PaymentMilestoneStatus;
  note?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ContractPaymentMilestoneRequest {
  id?: number | null;
  order: number;
  name: string;
  percentValue: number;
  dueDate?: string | null;
  actualPaymentDate?: string | null;
  responsibleAccountantUserId?: number | null;
  requestedAt?: string | null;
  status: PaymentMilestoneStatus;
  note?: string | null;
}

export interface UpdateContractPaymentScheduleRequest {
  rowVersion: string;
  paymentMilestones: ContractPaymentMilestoneRequest[];
}

export interface ContractListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: ContractResponse[];
  totalCurrentValue: number;
  overdueContractCount: number;
  dueSoonContractCount: number;
}

export interface ContractFilterOptionsResponse {
  owners: (PersonIdentity & { id: number; name: string })[];
  customers: { id: number; name: string }[];
  projects: { id: number; code: string; name: string; customerId: number }[];
}

export interface ContractListParams {
  status?: ContractStatus;
  direction?: ContractDirection;
  type?: ContractType;
  vendorId?: number;
  ownerUserId?: number;
  customerId?: number;
  operationalProjectId?: number;
  search?: string;
  signedFrom?: string;
  signedTo?: string;
  endFrom?: string;
  endTo?: string;
  valueMin?: number;
  valueMax?: number;
  page?: number;
  pageSize?: number;
  sortBy?: "signedDate" | "endDate" | "value" | "contractNumber" | "updatedAt";
  sortDirection?: "asc" | "desc";
}

export interface UpsertContractRequest {
  sourceContractId?: number | null;
  continuationKind?: string | null;
  continuationReason?: string | null;
  sourceContractRowVersion?: string;

  rowVersion?: string;
  contractNumber?: string | null;
  customerId: number;
  direction: ContractDirection;
  type: Exclude<ContractType, "Unclassified">;
  vendorId?: number | null;
  opportunityId?: number | null;
  quoteId?: number | null;
  operationalProjectId?: number | null;
  ownerUserId?: number | null;
  status: ContractStatus;
  signedDate?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  value: number;
  scopeOfWork?: string | null;
  note?: string | null;
  /**
   * null = leave the existing schedule untouched (useful for status-only
   * patches); empty array = wipe; non-empty array = replace. Percentages
   * must sum to 100.
   */
  paymentMilestones?: ContractPaymentMilestoneRequest[] | null;
}

export type ContractDirection = "Upstream" | "Downstream";

export type ContractType =
  | "Unclassified"
  | "Design"
  | "Permit"
  | "Construction"
  | "DesignAndPermit"
  | "DesignAndBuild"
  | "PermitAndConstruction"
  | "DesignPermitAndConstruction"
  | "Supply"
  | "Subcontract";

export interface ContractClassificationOptions {
  directions: ContractDirection[];
  types: Exclude<ContractType, "Unclassified">[];
  allowedTypes: Record<ContractDirection, Exclude<ContractType, "Unclassified">[]>;
  vendors: Pick<VendorResponse, "id" | "vendorCode" | "companyName" | "vendorType">[];
}
