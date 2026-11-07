export interface PaymentAttachmentRequest {
  fileName: string;
  filePath: string;
}

export interface PaymentAttachmentResponse extends PaymentAttachmentRequest {
  id: number;
}

export interface PaymentRequestEventResponse {
  id: number;
  fromStatus?: string | null;
  toStatus: string;
  reason?: string | null;
  changedByUserId: number;
  changedByName?: string | null;
  changedAt: string;
}

export interface PaymentReferencesResponse {
  contracts: Array<{
    id: number;
    contractNumber: string;
    vendorId: number;
    vendorName: string;
    paymentMilestones: Array<{ id: number; order: number; name: string }>;
  }>;
  vendors: Array<{ id: number; vendorCode: string; companyName: string }>;
  accountants: Array<{
    id: number;
    fullName: string | null;
    roleCode: string;
    roleName: string;
    roleLabelKey?: string | null;
    departmentId?: number | null;
    departmentLabelKey?: string | null;
  }>;
  qsBatches: Array<{ id: number; contractId: number; code: string; certifiedAmount: number }>;
}

export interface PaymentRequestResponse {
  id: number;
  code: string;
  contractId: number;
  contractNumber: string;
  vendorId: number;
  vendorName: string;
  contractPaymentMilestoneId?: number | null;
  constructionQsBatchId?: number | null;
  constructionQsCode?: string | null;
  supplierInvoiceNumber: string;
  invoiceDate: string;
  invoiceAmount: number;
  currency: string;
  status: string;
  revisionNumber: number;
  revisions: PaymentRequestRevisionResponse[];
  receivedAt: string;
  validatedAt?: string | null;
  assignedAccountantUserId: number;
  assignedAccountantName?: string | null;
  submittedAt?: string | null;
  submittedByUserId?: number | null;
  approvedAt?: string | null;
  approvedByUserId?: number | null;
  paidAt?: string | null;
  paidByUserId?: number | null;
  rejectedAt?: string | null;
  cancelledAt?: string | null;
  decisionReason?: string | null;
  rowVersion: string;
  attachments: PaymentAttachmentResponse[];
  events: PaymentRequestEventResponse[];
}

export interface PaymentRequestRevisionResponse {
  revisionNumber: number;
  status: string;
  constructionQsBatchId?: number | null;
  supplierInvoiceNumber: string;
  invoiceAmount: number;
  currency: string;
  capturedAt: string;
  revisionReason: string;
  attachments: PaymentAttachmentResponse[];
}

export interface PaymentRequestUpsertRequest {
  contractId: number;
  vendorId: number;
  contractPaymentMilestoneId?: number | null;
  constructionQsBatchId?: number | null;
  supplierInvoiceNumber: string;
  invoiceDate: string;
  invoiceAmount: number;
  currency: string;
  receivedAt: string;
  assignedAccountantUserId: number;
  attachments: PaymentAttachmentRequest[];
  rowVersion?: string;
}

export interface AccountingPeriodResponse {
  id: number;
  year: number;
  month: number;
  periodStartUtc: string;
  periodEndUtc: string;
  status: string;
  closingAt?: string | null;
  closedAt?: string | null;
  closeReason?: string | null;
  rowVersion: string;
}

export interface AccountingCorrectionResponse {
  id: number;
  code: string;
  operationalProjectId: number;
  projectName: string;
  accountingPeriodId: number;
  periodLabel: string;
  sourceEntityType: string;
  sourceEntityId: number;
  reasonCode: string;
  originalValue: number;
  correctedValue: number;
  currency: string;
  note?: string | null;
  responsibleAccountantUserId: number;
  responsibleAccountantName?: string | null;
  status: string;
  recordedByUserId: number;
  submittedAt?: string | null;
  approvedAt?: string | null;
  rejectedAt?: string | null;
  decisionReason?: string | null;
  reversalOfCorrectionId?: number | null;
  reversedAt?: string | null;
  rowVersion: string;
}

export interface AccountingCorrectionUpsertRequest {
  operationalProjectId: number;
  accountingPeriodId: number;
  sourceEntityType: string;
  sourceEntityId: number;
  reasonCode: string;
  originalValue: number;
  correctedValue: number;
  currency: string;
  note?: string | null;
  responsibleAccountantUserId: number;
  rowVersion?: string;
}

export type AcceptanceStatus = "Draft" | "Submitted" | "Approved" | "Rejected" | "Cancelled";

export interface AcceptanceRecordResponse {
  id: number;
  designProjectId: number;
  designProjectName: string;
  acceptanceCode: string;
  title: string;
  description?: string | null;
  constructionTaskId?: number | null;
  constructionTaskName?: string | null;
  acceptanceDate: string;
  location?: string | null;
  participants?: string | null;
  findings?: string | null;
  resolutionNote?: string | null;
  documents?: string | null;
  status: AcceptanceStatus;
  isOverdue: boolean;
  revisionCount: number;
  submittedAt?: string | null;
  submittedByUserId?: number | null;
  submittedByName?: string | null;
  approvedAt?: string | null;
  approvedByUserId?: number | null;
  approvedByName?: string | null;
  rejectedAt?: string | null;
  rejectedByUserId?: number | null;
  rejectedByName?: string | null;
  createdAt: string;
  createdByUserId?: number | null;
  updatedAt: string;
  updatedByUserId?: number | null;
}

export interface AcceptanceRecordListResponse {
  items: AcceptanceRecordResponse[];
  total: number;
  page: number;
  pageSize: number;
  statusCounts: Partial<Record<AcceptanceStatus, number>>;
  overdueCount: number;
}

export interface AcceptanceRecordListParams {
  designProjectId?: number;
  status?: string;
  constructionTaskId?: number;
  responsibleUserId?: number;
  acceptanceFrom?: string;
  acceptanceTo?: string;
  search?: string;
  overdueOnly?: boolean;
  openOnly?: boolean;
  sortBy?: "date" | "code" | "title" | "project" | "status" | "updatedAt";
  sortDirection?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export interface CreateAcceptanceRecordRequest {
  designProjectId: number;
  title: string;
  description?: string | null;
  constructionTaskId?: number | null;
  acceptanceDate: string;
  location?: string | null;
  participants?: string | null;
  findings?: string | null;
  documents?: string | null;
}

export interface UpdateAcceptanceRecordRequest {
  title: string;
  description?: string | null;
  constructionTaskId?: number | null;
  acceptanceDate: string;
  location?: string | null;
  participants?: string | null;
  findings?: string | null;
  documents?: string | null;
}

export interface TransitionAcceptanceStatusRequest {
  status: AcceptanceStatus;
  resolutionNote?: string | null;
}

export interface BulkDeleteAcceptanceRecordsRequest {
  ids: number[];
}

export interface AcceptanceRecordBulkDeleteResponse {
  deletedIds: number[];
  skippedIds: number[];
}
