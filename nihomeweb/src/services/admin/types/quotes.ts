export type QuoteMethod = "UnitCost" | "Boq";

export const QUOTE_METHODS: QuoteMethod[] = ["UnitCost", "Boq"];

export type QuoteStatus =
  | "Draft"
  | "PendingApproval"
  | "Approved"
  | "SentToCustomer"
  | "CustomerApproved"
  | "Rejected"
  | "Expired"
  | "Cancelled";

export const QUOTE_STATUSES: QuoteStatus[] = [
  "Draft",
  "PendingApproval",
  "Approved",
  "SentToCustomer",
  "CustomerApproved",
  "Rejected",
  "Expired",
  "Cancelled",
];

export interface QuoteItemInput {
  itemCode?: string | null;
  name: string;
  unit: string;
  quantity: number | string;
  unitPrice: number | string;
  sortOrder?: number;
}

export interface QuoteItemResponse {
  id: number;
  itemCode?: string;
  name: string;
  unit: string;
  quantity: string;
  unitPrice: string;
  amount: string;
  sortOrder: number;
}

export interface QuoteApprovalLogResponse {
  id: number;
  action: string;
  fromStatus?: string;
  toStatus: string;
  byUserId?: number;
  byUserName?: string;
  note?: string;
  createdAt: string;
}

export interface QuoteVersionResponse {
  version: number;
  method: QuoteMethod;
  currency: string;
  areaSqm?: number;
  unitPricePerSqm?: number;
  packageDescription?: string;
  materialRateRevisionId?: number | null;
  materialRateCatalogId?: number | null;
  materialRateCatalogCode?: string | null;
  materialRateCatalogName?: string | null;
  materialRateRevisionVersion?: number | null;
  pricingEffectiveDate?: string | null;
  catalogUnitPricePerSqm?: number | null;
  rateSource: "Catalog" | "Override" | "CatalogReference";
  rateOverrideReason?: string | null;
  rateOverrideByUserId?: number | null;
  rateOverrideAt?: string | null;
  inputPriceOfferId?: number | null;
  inputPriceVendorName?: string | null;
  inputPriceSupplierReference?: string | null;
  inputPriceSnapshotJson?: string | null;
  subtotal: number;
  discountPercent: number;
  vatPercent: number;
  grandTotal: number;
  items: QuoteItemResponse[];
  capturedAt: string;
  isCurrent: boolean;
}

export interface QuoteResponse {
  id: number;
  code: string;
  opportunityId: number;
  operationalProjectId?: number | null;
  opportunityName?: string;
  customerId?: number;
  customerName?: string;
  ownerUserId?: number;
  ownerName?: string;
  method: QuoteMethod;
  version: number;
  currency: string;
  areaSqm?: number;
  unitPricePerSqm?: number;
  packageDescription?: string;
  materialRateRevisionId?: number | null;
  materialRateCatalogId?: number | null;
  materialRateCatalogCode?: string | null;
  materialRateCatalogName?: string | null;
  materialRateRevisionVersion?: number | null;
  projectBoqRevisionId?: number | null;
  projectBoqRevisionNumber?: number | null;
  pricingEffectiveDate?: string | null;
  catalogUnitPricePerSqm?: number | null;
  rateSource: "Catalog" | "Override" | "CatalogReference";
  rateOverrideReason?: string | null;
  rateOverrideByUserId?: number | null;
  rateOverrideAt?: string | null;
  inputPriceOfferId?: number | null;
  inputPriceVendorName?: string | null;
  inputPriceSupplierReference?: string | null;
  inputPriceSnapshotJson?: string | null;
  subtotal: number;
  discountPercent: number;
  vatPercent: number;
  grandTotal: number;
  grandTotalInWords: string;
  status: QuoteStatus;
  validUntil: string;
  isExpired: boolean;
  note?: string;
  submittedAt?: string;
  approvalApproverUserId?: number | null;
  approvalApproverName?: string | null;
  approvalEmailStatus?: "Pending" | "Sending" | "Sent" | "NeedsReview" | null;
  approvalEmailSentAt?: string | null;
  approvedAt?: string;
  sentAt?: string;
  closedAt?: string;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  items: QuoteItemResponse[];
  approvalLogs: QuoteApprovalLogResponse[];
}

export interface QuoteDocumentResponse {
  id: number;
  quoteId: number;
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
  label?: string;
  createdAt: string;
  uploadedByUserId?: number;
  uploadedByName?: string;
}

export interface QuoteListItemResponse {
  id: number;
  code: string;
  opportunityId: number;
  opportunityName?: string;
  customerName?: string;
  ownerUserId?: number;
  ownerName?: string;
  approvalApproverUserId?: number | null;
  version: number;
  method: QuoteMethod;
  currency: string;
  grandTotal: number;
  status: QuoteStatus;
  validUntil: string;
  isExpiringSoon: boolean;
  updatedAt: string;
  rowVersion: string;
}

export interface QuoteListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: QuoteListItemResponse[];
}

export interface QuoteVersionsResponse {
  quoteId: number;
  versions: QuoteVersionResponse[];
}

export interface CreateQuoteRequest {
  draftOnly?: boolean;
  opportunityId: number;
  ownerUserId?: number | null;
  inputPriceOfferId?: number | null;
  method?: QuoteMethod;
  areaSqm?: number | null;
  unitPricePerSqm?: number | null;
  materialRateCatalogId?: number | null;
  pricingEffectiveDate?: string | null;
  rateOverrideReason?: string | null;
  packageDescription?: string;
  items?: QuoteItemInput[];
  discountPercent: number;
  vatPercent: number;
  validUntil?: string | null;
  note?: string;
}

export interface UpdateQuoteRequest {
  rowVersion?: string;
  method?: QuoteMethod | null;
  ownerUserId?: number | null;
  inputPriceOfferId?: number | null;
  areaSqm?: number | null;
  unitPricePerSqm?: number | null;
  materialRateCatalogId?: number | null;
  pricingEffectiveDate?: string | null;
  rateOverrideReason?: string | null;
  packageDescription?: string;
  // BOQ quotes copy their lines from this approved project BOQ revision.
  projectBoqRevisionId?: number | null;
  items?: QuoteItemInput[];
  discountPercent: number;
  vatPercent: number;
  validUntil?: string | null;
  note?: string;
}

export interface QuoteProjectBoq {
  revisionId: number;
  revisionNumber: number;
  status: string;
  currency: string;
  total: number;
  lineCount: number;
  approvedAt?: string | null;
  upstreamContractNumber?: string | null;
  sourceFileName?: string | null;
}

export interface QuoteWorkflowRequest {
  rowVersion?: string;
  note?: string;
}

export interface QuoteApprovalCandidate {
  id: number;
  name: string;
  email: string;
  roleCode: string;
  roleName: string;
  roleLabelKey?: string | null;
  departmentId?: number | null;
  departmentLabelKey?: string | null;
}

export interface QuoteApprovalPreview {
  previewToken: string;
  approverUserId: number;
  approverName: string;
  toEmail: string;
  ccEmails: string[];
  subject: string;
  body: string;
}

export interface QuoteEmailPreviewResponse {
  toEmail: string;
  subject: string;
  body: string;
  canEdit: boolean;
  attachmentFileName: string;
  attachmentContentType: string;
}

export interface SendQuoteEmailRequest extends QuoteWorkflowRequest {
  toEmail: string;
  subject: string;
  body: string;
  languageCode: string;
}

export interface ExtendQuoteValidityRequest {
  rowVersion?: string;
  newValidUntil: string;
  note?: string;
}

export type MaterialRateRevisionStatus = "Draft" | "Approved" | "Rejected" | "Retired";

export type MaterialRateCatalogType = "InvestmentRate" | "Boq";

export interface MaterialRateCatalogResponse {
  id: number;
  catalogType: MaterialRateCatalogType;
  code: string;
  name: string;
  description?: string | null;
  currency: string;
  isActive: boolean;
  revisionCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface MaterialRateLineResponse {
  id: number;
  materialCode: string;
  materialName: string;
  unit: string;
  quantity: string;
  normPerSqm: string;
  unitRate: string;
  wastePercent: string;
  amountPerSqm: string;
  sortOrder: number;
}

export interface MaterialRateRevisionResponse {
  id: number;
  catalogId: number;
  catalogType: MaterialRateCatalogType;
  catalogCode: string;
  catalogName: string;
  currency: string;
  version: number;
  status: MaterialRateRevisionStatus;
  effectiveFrom: string;
  effectiveTo?: string | null;
  note?: string | null;
  decisionNote?: string | null;
  decidedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  importSourceFileName?: string | null;
  importSourceHash?: string | null;
  importedAt?: string | null;
  totalRatePerSqm: string;
  totalAmount: string;
  lines: MaterialRateLineResponse[];
}

export interface UpsertMaterialRateCatalogRequest {
  catalogType: MaterialRateCatalogType;
  code: string;
  name: string;
  description?: string | null;
  currency: string;
  isActive: boolean;
}

export interface CreateMaterialRateRevisionRequest {
  effectiveFrom: string;
  effectiveTo?: string | null;
  note?: string | null;
}

export interface UpsertInvestmentRateLineRequest {
  materialCode: string;
  materialName: string;
  unit: string;
  normPerSqm: string;
  unitRate: string;
  wastePercent: string;
}

export interface CsvImportError {
  row?: number | null;
  column?: number | null;
  message: string;
  messageKey?: string | null;
  messageArgs?: Record<string, string | number> | null;
}

export interface MaterialRateImportResponse {
  importedCount: number;
  errors: CsvImportError[];
}

export interface QuoteListParams {
  status?: QuoteStatus;
  opportunityId?: number;
  customerId?: number;
  ownerUserId?: number;
  createdFrom?: string;
  createdTo?: string;
  minValue?: number;
  maxValue?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CsvImportError {
  row?: number | null;
  column?: number | null;
  message: string;
}
