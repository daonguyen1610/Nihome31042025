import api, { withIdempotencyKey } from "@/lib/api";
import type { ContentItem, ServiceResponse } from "@/services/contentApi";

// ─── Request types ───────────────────────────────────────────

export interface UpsertActivityRequest {
  slug: string;
  date: string;
  imageUrl: string;
  gallery?: string[];
  category: string;
  categoryId?: number | null;
  author?: string;
  title: string;
  excerpt: string;
  content: ContentItem[];
  sortOrder?: number;
}

export interface UpsertNewsRequest {
  slug: string;
  date: string;
  imageUrl: string;
  gallery?: string[];
  category: string;
  newsCategoryId?: number | null;
  title: string;
  excerpt: string;
  content: ContentItem[];
  sortOrder?: number;
}

export interface UpsertProjectRequest {
  slug: string;
  imageUrl: string;
  gallery?: string[];
  name: string;
  client: string;
  location: string;
  scale: string;
  scope: string;
  status: string;
  year?: string;
  category?: string;
  categoryId?: number | null;
  description?: string;
  challenges?: string[];
  solutions?: string[];
  highlights?: { label: string; value: string }[];
  content?: ContentItem[];
  sortOrder?: number;
}

export interface UpsertLogoRequest {
  name: string;
  imageUrl: string;
  href?: string;
  kind: string;
  sortOrder?: number;
}

export interface UpsertProcessRequest {
  groupKey: string;
  code?: string;
  title: string;
  sortOrder?: number;
  images?: ProcessAssetInput[];
  files?: ProcessAssetInput[];
}

export interface ProcessAssetInput {
  displayName: string;
  url: string;
  originalFileName: string;
  contentType: string;
  sortOrder: number;
}

export interface ProcessAssetUploadResponse {
  displayName: string;
  url: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  sortOrder: number;
}

export interface UpsertSlideshowRequest {
  slug: string;
  imageUrl: string;
  title: string;
  subtitle?: string;
  linkUrl?: string;
  linkText?: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface UpsertAboutSectionRequest {
  slug: string;
  itemsJson?: string | null;
  eyebrow: string;
  titleA: string;
  titleB: string;
  paragraph1: string;
  paragraph2: string;
  imageUrl: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface AboutSectionAdminResponse {
  id: number;
  slug: string;
  itemsJson?: string | null;
  eyebrow: string;
  titleA: string;
  titleB: string;
  paragraph1: string;
  paragraph2: string;
  imageUrl: string;
  isActive: boolean;
  sortOrder: number;
}

export interface UpsertServiceAdminRequest {
  slug: string;
  title: string;
  shortTitle: string;
  tagline: string;
  intro: string;
  sections: { heading: string; body: string[] }[];
  highlights: string[];
  introBlocks: { text: string; imageUrl?: string }[];
  sortOrder: number;
}

export interface SlideshowAdminResponse {
  id: number;
  slug: string;
  imageUrl: string;
  title: string;
  subtitle?: string;
  linkUrl?: string;
  linkText?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface ActivityCategoryResponse {
  id: number;
  name: string;
  nameVi: string;
  nameEn: string;
  nameZh: string;
  nameJa: string;
  isActive: boolean;
  sortOrder: number;
}

export interface UpsertActivityCategoryRequest {
  name: string;
  nameVi?: string;
  nameEn?: string;
  nameZh?: string;
  nameJa?: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface UpsertProjectCategoryRequest {
  name: string;
  nameVi?: string;
  nameEn?: string;
  nameZh?: string;
  nameJa?: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface ProjectCategoryResponse {
  id: number;
  name: string;
  nameVi: string;
  nameEn: string;
  nameZh: string;
  nameJa: string;
  isActive: boolean;
  sortOrder: number;
}

export interface UpsertNewsCategoryRequest {
  name: string;
  nameVi?: string;
  nameEn?: string;
  nameZh?: string;
  nameJa?: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface NewsCategoryResponse {
  id: number;
  name: string;
  nameVi: string;
  nameEn: string;
  nameZh: string;
  nameJa: string;
  isActive: boolean;
  sortOrder: number;
}

export interface UpsertEmploymentTypeRequest {
  code: string;
  name: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface EmploymentTypeResponse {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
}

export interface UpsertJobPositionRequest {
  title: string;
  department: string;
  location: string;
  employmentType: string;
  experienceLevel: string;
  description?: string;
  requirements: string[];
  benefits: string[];
  isActive: boolean;
  sortOrder: number;
}

export interface JobPositionResponse {
  id: number;
  title: string;
  department: string;
  location: string;
  employmentType: string;
  experienceLevel: string;
  description?: string;
  requirements: string[];
  benefits: string[];
  isActive: boolean;
  sortOrder: number;
  applicationCount: number;
}

export interface UpsertRecruitmentDropdownOptionRequest {
  type: string;
  code: string;
  name: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface RecruitmentDropdownOptionResponse {
  id: number;
  type: string;
  code: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
}

export interface JobApplicationResponse {
  id: number;
  jobPositionId: number;
  positionTitle: string;
  candidateName: string;
  email: string;
  phone?: string;
  experienceYears?: number;
  coverLetter?: string;
  cvUrl?: string;
  status: string;
  appliedAt: string;
}

export interface SubmitJobApplicationRequest {
  jobPositionId: number;
  candidateName: string;
  email: string;
  phone?: string;
  experienceYears?: number;
  coverLetter?: string;
  cvUrl?: string;
}

export interface EmailTemplatesResponse {
  newApplicationEmailSubjectTemplate: string | null;
  newApplicationEmailBodyTemplate: string | null;
  notificationEmail: string | null;
  otpEmailSubjectTemplate: string | null;
  otpEmailBodyTemplate: string | null;
}

export interface UpdateEmailTemplatesRequest {
  newApplicationEmailSubjectTemplate?: string | null;
  newApplicationEmailBodyTemplate?: string | null;
  notificationEmail?: string | null;
  otpEmailSubjectTemplate?: string | null;
  otpEmailBodyTemplate?: string | null;
}

export interface OtpSettingsResponse {
  enableOtpForRegistration: boolean;
  enableOtpForForgotPassword: boolean;
}

export type UpdateOtpSettingsRequest = OtpSettingsResponse;

export interface MapEmbedResponse {
  mapEmbedUrl: string | null;
}

export interface UpdateMapEmbedRequest {
  mapEmbedUrl: string | null;
}

export interface ContactMessageResponse {
  id: number;
  name: string;
  email: string;
  phone?: string;
  subject: string;
  message: string;
  isReplied: boolean;
  replyContent?: string;
  repliedAt?: string;
  createdAt: string;
}

// -------- CRM Lead --------

export type LeadStatus =
  | "New"
  | "Contacted"
  | "Interested"
  | "NotInterested"
  | "Converted"
  | "Junk";

export type LeadActivityType = "Call" | "Email" | "Meeting" | "Note";

export interface LeadActivityResponse {
  id: number;
  type: LeadActivityType;
  content: string;
  createdByUserId: number;
  createdByName?: string;
  createdAt: string;
}

export interface LeadResponse {
  id: number;
  name: string;
  companyName?: string;
  phone?: string;
  email?: string;
  sourceCode: string;
  status: LeadStatus;
  ownerUserId?: number;
  ownerName?: string;
  note?: string;
  convertedAt?: string;
  convertedCustomerId?: number;
  convertedOpportunityId?: number;
  createdAt: string;
  updatedAt: string;
  activities: LeadActivityResponse[];
}

export interface LeadListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: LeadResponse[];
}

export interface CreateLeadRequest {
  name: string;
  companyName?: string;
  phone?: string;
  email?: string;
  sourceCode: string;
  ownerUserId?: number | null;
  note?: string;
}

export interface UpdateLeadRequest {
  name: string;
  companyName?: string;
  phone?: string;
  email?: string;
  sourceCode: string;
  status: LeadStatus;
  ownerUserId?: number | null;
  note?: string;
}

export interface ConvertLeadRequest {
  customerId?: number | null;
  opportunityId?: number | null;
  note?: string;
}

export interface CreateLeadActivityRequest {
  type: LeadActivityType;
  content: string;
}

export interface LeadListParams {
  status?: LeadStatus;
  sourceCode?: string;
  ownerUserId?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface MasterDataOption {
  id: number;
  category: string;
  code: string;
  name: string;
  labelKey?: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface MasterDataCategory {
  category: string;
  totalCount: number;
  activeCount: number;
}

export interface UpsertMasterDataOptionRequest {
  code: string;
  name: string;
  labelKey?: string | null;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
}

// -------- Workflow config (NIH-225) --------

export interface WorkflowStep {
  order: number;
  name: string;
  approverRoleCode: string;
  slaHours: number;
  requireAllApprovers: boolean;
  conditionExpression?: string | null;
}

export interface WorkflowConfig {
  id: number;
  module: string;
  action: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
  steps: WorkflowStep[];
  createdAt: string;
  updatedAt: string;
}

export interface UpsertWorkflowConfigRequest {
  module: string;
  action: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
  steps: WorkflowStep[];
}

// -------- CRM Customer --------

export type CustomerType = "Individual" | "Company";
export type CustomerRelationshipStatus = "Prospect" | "InProgress" | "Signed" | "Suspended";
export type CustomerActivityType = "Call" | "Email" | "Meeting" | "Note";

export interface CustomerContactResponse {
  id: number;
  fullName: string;
  position?: string;
  phone?: string;
  email?: string;
  isPrimary: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CustomerActivityResponse {
  id: number;
  type: CustomerActivityType;
  occurredAt: string;
  content: string;
  createdByUserId: number;
  createdByName?: string;
  createdAt: string;
}

export interface CustomerResponse {
  id: number;
  type: CustomerType;
  name: string;
  taxId?: string;
  address?: string;
  representativeName?: string;
  sourceCode: string;
  relationshipStatus: CustomerRelationshipStatus;
  ownerUserId?: number;
  ownerName?: string;
  note?: string;
  createdAt: string;
  updatedAt: string;
  contacts: CustomerContactResponse[];
  activities: CustomerActivityResponse[];
}

export interface CustomerListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: CustomerResponse[];
}

export interface UpsertCustomerContactRequest {
  id?: number;
  fullName: string;
  position?: string;
  phone?: string;
  email?: string;
  isPrimary?: boolean;
}

export interface CreateCustomerRequest {
  type: CustomerType;
  name: string;
  taxId?: string;
  address?: string;
  representativeName?: string;
  sourceCode: string;
  ownerUserId?: number | null;
  note?: string;
  primaryContact: UpsertCustomerContactRequest;
  duplicateOverrideReason?: string;
}

export interface UpdateCustomerRequest {
  type: CustomerType;
  name: string;
  taxId?: string;
  address?: string;
  representativeName?: string;
  sourceCode: string;
  relationshipStatus: CustomerRelationshipStatus;
  ownerUserId?: number | null;
  note?: string;
  duplicateOverrideReason?: string;
}

export interface CreateCustomerActivityRequest {
  type: CustomerActivityType;
  content: string;
  occurredAt?: string;
}

export interface CustomerDuplicateDetail {
  field: "TaxId" | "Phone";
  value: string;
  existingCustomerId: number;
  existingCustomerName: string;
  message: string;
}

export interface CustomerListParams {
  type?: CustomerType;
  status?: CustomerRelationshipStatus;
  ownerUserId?: number;
  sourceCode?: string;
  search?: string;
  createdFrom?: string;
  createdTo?: string;
  page?: number;
  pageSize?: number;
}

// ─── CRM Opportunity (NIH-83) ────────────────────────────────────────

export type OpportunityStage =
  | "Prospecting"
  | "Qualification"
  | "Proposal"
  | "Negotiation"
  | "Won"
  | "Lost";

export const OPPORTUNITY_STAGES: OpportunityStage[] = [
  "Prospecting",
  "Qualification",
  "Proposal",
  "Negotiation",
  "Won",
  "Lost",
];

// -------- CRM Contract (NIH-102) --------

export type ContractStatus =
  | "Draft"
  | "Signed"
  | "InProgress"
  | "OnHold"
  | "Completed"
  | "Cancelled";

export const CONTRACT_STATUSES: ContractStatus[] = [
  "Draft",
  "Signed",
  "InProgress",
  "OnHold",
  "Completed",
  "Cancelled",
];

export interface ContractResponse {
  id: number;
  contractNumber: string;
  customerId: number;
  customerName?: string;
  opportunityId?: number | null;
  opportunityTitle?: string | null;
  quoteId?: number | null;
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
  /** At least one attachment of kind SignedScan exists. */
  hasSignedScan: boolean;
  attachmentCount: number;
  appendixCount: number;
  scopeOfWork?: string | null;
  note?: string | null;
  createdAt: string;
  updatedAt: string;
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
  status: PaymentMilestoneStatus;
  note?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface ContractPaymentMilestoneRequest {
  order: number;
  name: string;
  percentValue: number;
  dueDate?: string | null;
  status: PaymentMilestoneStatus;
  note?: string | null;
}

export interface ContractListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: ContractResponse[];
}

export interface ContractListParams {
  status?: ContractStatus;
  ownerUserId?: number;
  customerId?: number;
  search?: string;
  signedFrom?: string;
  signedTo?: string;
  valueMin?: number;
  valueMax?: number;
  page?: number;
  pageSize?: number;
}

export interface UpsertContractRequest {
  contractNumber?: string | null;
  customerId: number;
  opportunityId?: number | null;
  quoteId?: number | null;
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

// -------- NIH-104: appendices (VO), attachments, timeline --------

export type ContractAppendixStatus =
  | "Draft"
  | "Submitted"
  | "Approved"
  | "Rejected";

export const CONTRACT_APPENDIX_STATUSES: ContractAppendixStatus[] = [
  "Draft",
  "Submitted",
  "Approved",
  "Rejected",
];

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
  filePath?: string | null;
  originalFileName?: string | null;
  fileSize?: number | null;
  contentType?: string | null;
}

export type ContractAttachmentKind = "SignedScan" | "Supporting";

export const CONTRACT_ATTACHMENT_KINDS: ContractAttachmentKind[] = [
  "SignedScan",
  "Supporting",
];

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

export type OpportunityActivityType =
  | "Call"
  | "Email"
  | "Meeting"
  | "Note"
  | "StageChange";

export interface OpportunityActivityResponse {
  id: number;
  type: OpportunityActivityType;
  occurredAt: string;
  content: string;
  createdByUserId: number;
  createdByName?: string;
  createdAt: string;
}

export interface OpportunityResponse {
  id: number;
  name: string;
  customerId: number;
  customerName?: string;
  ownerUserId?: number;
  ownerName?: string;
  estimatedValue: number;
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
  activities: OpportunityActivityResponse[];
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
  name: string;
  customerId: number;
  ownerUserId?: number | null;
  estimatedValue: number;
  winProbability: number;
  expectedCloseDate?: string | null;
  stage?: OpportunityStage;
  note?: string;
}

export interface UpdateOpportunityRequest {
  name: string;
  customerId: number;
  ownerUserId?: number | null;
  estimatedValue: number;
  winProbability: number;
  expectedCloseDate?: string | null;
  note?: string;
}

export interface ChangeOpportunityStageRequest {
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
}

export interface OpportunityPipelineParams {
  ownerUserId?: number;
  customerId?: number;
  expectedCloseFrom?: string;
  expectedCloseTo?: string;
  minValue?: number;
  maxValue?: number;
}

// ─── Quotes (NIH-84) ─────────────────────────────────────────

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
  quantity: number;
  unitPrice: number;
  sortOrder?: number;
}

export interface QuoteItemResponse {
  id: number;
  itemCode?: string;
  name: string;
  unit: string;
  quantity: number;
  unitPrice: number;
  amount: number;
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
  areaSqm?: number;
  unitPricePerSqm?: number;
  packageDescription?: string;
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
  opportunityName?: string;
  customerId?: number;
  customerName?: string;
  ownerUserId?: number;
  ownerName?: string;
  method: QuoteMethod;
  version: number;
  areaSqm?: number;
  unitPricePerSqm?: number;
  packageDescription?: string;
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
  approvedAt?: string;
  sentAt?: string;
  closedAt?: string;
  createdAt: string;
  updatedAt: string;
  items: QuoteItemResponse[];
  approvalLogs: QuoteApprovalLogResponse[];
}

export interface QuoteListItemResponse {
  id: number;
  code: string;
  opportunityId: number;
  opportunityName?: string;
  customerName?: string;
  ownerUserId?: number;
  ownerName?: string;
  version: number;
  method: QuoteMethod;
  grandTotal: number;
  status: QuoteStatus;
  validUntil: string;
  isExpiringSoon: boolean;
  updatedAt: string;
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
  opportunityId: number;
  ownerUserId?: number | null;
  method: QuoteMethod;
  areaSqm?: number | null;
  unitPricePerSqm?: number | null;
  packageDescription?: string;
  items?: QuoteItemInput[];
  discountPercent: number;
  vatPercent: number;
  validUntil?: string | null;
  note?: string;
}

export interface UpdateQuoteRequest {
  ownerUserId?: number | null;
  areaSqm?: number | null;
  unitPricePerSqm?: number | null;
  packageDescription?: string;
  items?: QuoteItemInput[];
  discountPercent: number;
  vatPercent: number;
  validUntil?: string | null;
  note?: string;
}

export interface QuoteWorkflowRequest {
  note?: string;
}

export interface ExtendQuoteValidityRequest {
  newValidUntil: string;
  note?: string;
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

// ─── Capability Documents (NIH-98) ────────────────────────────

/** Bucket returned by the backend for the expiry badge. */
export type CapabilityDocumentExpiryState = "none" | "expired" | "critical" | "warning" | "ok";

export const CAPABILITY_DOCUMENT_EXPIRY_STATES: CapabilityDocumentExpiryState[] = [
  "none",
  "expired",
  "critical",
  "warning",
  "ok",
];

export interface CapabilityDocumentVersionResponse {
  id: number;
  versionNumber: number;
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
  uploadedByUserId?: number | null;
  uploadedByName?: string | null;
  createdAt: string;
}

export interface CapabilityDocumentResponse {
  id: number;
  name: string;
  tagCode: string;
  tagLabel?: string | null;
  issuedDate?: string | null;
  expiryDate?: string | null;
  description?: string | null;
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
  currentVersion: number;
  expiryState: CapabilityDocumentExpiryState;
  uploadedByUserId?: number | null;
  uploadedByName?: string | null;
  createdAt: string;
  updatedAt: string;
  previousVersionCount: number;
}

export interface CapabilityDocumentDetailResponse extends CapabilityDocumentResponse {
  versions: CapabilityDocumentVersionResponse[];
}

export interface CapabilityDocumentListResponse {
  items: CapabilityDocumentResponse[];
  total: number;
  page: number;
  pageSize: number;
}

export interface UpsertCapabilityDocumentRequest {
  name: string;
  tagCode: string;
  issuedDate?: string | null;
  expiryDate?: string | null;
  description?: string | null;
  filePath?: string;
  originalFileName?: string;
  fileSize?: number;
  contentType?: string;
}

export interface ReplaceCapabilityDocumentFileRequest {
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
}

export interface CapabilityDocumentUploadResponse {
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
}

export interface CapabilityDocumentListParams {
  tagCode?: string;
  issuedYear?: number;
  search?: string;
  expiryState?: CapabilityDocumentExpiryState;
  page?: number;
  pageSize?: number;
}

// ─── Tenders (NIH-85 / NIH-95 / NIH-96) ────────────────────────

export type TenderStatus = "Preparing" | "Submitted" | "Won" | "Lost" | "Cancelled";

export const TENDER_STATUSES: TenderStatus[] = [
  "Preparing",
  "Submitted",
  "Won",
  "Lost",
  "Cancelled",
];

export type TenderChecklistItemStatus = "NotStarted" | "Preparing" | "Done" | "Submitted";

export interface TenderChecklistItemResponse {
  id: number;
  templateCode?: string | null;
  title: string;
  status: TenderChecklistItemStatus;
  ownerUserId?: number | null;
  ownerName?: string | null;
  internalDeadline?: string | null;
  filePath?: string | null;
  originalFileName?: string | null;
  sortOrder: number;
}

export interface TenderResponse {
  id: number;
  code: string;
  name: string;
  customerId: number;
  customerName: string;
  openingDate?: string | null;
  submissionDeadline: string;
  preparerUserId?: number | null;
  preparerName?: string | null;
  infoSource?: string | null;
  status: TenderStatus;
  note?: string | null;
  wonOpportunityId?: number | null;
  lostReasonCode?: string | null;
  lostNote?: string | null;
  closedAt?: string | null;
  createdAt: string;
  updatedAt: string;
  checklistItems: TenderChecklistItemResponse[];
  checklistCompletionPercent: number;
  isDeadlineImminent: boolean;
}

export interface TenderListItemResponse {
  id: number;
  code: string;
  name: string;
  customerId: number;
  customerName: string;
  openingDate?: string | null;
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
  openingDate?: string | null;
  submissionDeadline: string;
  preparerUserId?: number | null;
  infoSource?: string | null;
  note?: string | null;
}

export interface UpdateTenderRequest {
  name: string;
  openingDate?: string | null;
  submissionDeadline: string;
  preparerUserId?: number | null;
  infoSource?: string | null;
  note?: string | null;
}

export interface TenderListParams {
  status?: string;
  customerId?: number;
  preparerUserId?: number;
  openingMonth?: number;
  openingYear?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}

// ─── NIH-97 tender detail-page workflow ────────────────────────

/**
 * Inline-edit for a single checklist row. `clearOwner` / `clearInternalDeadline`
 * flip explicit nulls (so a plain absent field == "no change"). Send one
 * field at a time when the FE mutates via optimistic-UI.
 */
export interface UpdateTenderChecklistItemRequest {
  status?: TenderChecklistItemStatus;
  ownerUserId?: number | null;
  clearOwner?: boolean;
  internalDeadline?: string | null;
  clearInternalDeadline?: boolean;
}

export interface AttachTenderChecklistFromLibraryItem {
  checklistItemId: number;
  capabilityDocumentId: number;
}

export interface AttachTenderChecklistFromLibraryRequest {
  items: AttachTenderChecklistFromLibraryItem[];
}

export interface MarkTenderWonRequest {
  opportunityId: number;
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

/**
 * RBAC role code. Historically restricted to the three system codes
 * (`SUPER_ADMIN` / `ADMIN` / `USER`); now any code from the `roles` table
 * (custom business roles included) is valid. Typed as `string` to allow
 * arbitrary catalog values while keeping autocomplete on the system codes.
 */
export type UserRole = (string & {}) | "SUPER_ADMIN" | "ADMIN" | "USER";

export interface UserListItemResponse {
  id: number;
  phoneNumber: string;
  fullName?: string;
  email?: string;
  /** Canonical RBAC role code. Falls back to the legacy enum for legacy users. */
  role: UserRole;
  /** RBAC role id. Null only for legacy users not yet backfilled. */
  roleId?: number | null;
  /** Human-readable role name from the `roles` table. Null for legacy users. */
  roleName?: string | null;
  isActive: boolean;
  avatarUrl?: string;
}

export interface UserDetailResponse extends UserListItemResponse {
  refreshTokenCount: number;
}

export interface UserListResponse {
  items: UserListItemResponse[];
  total: number;
}

export interface CreateUserRequest {
  phoneNumber: string;
  fullName: string;
  email: string;
  password: string;
  role: UserRole;
}

export interface UpdateUserRequest {
  fullName?: string;
  email?: string;
  role?: UserRole;
  isActive?: boolean;
}

export interface RoleMetadataResponse {
  role: UserRole;
  labelKey: string;
  descriptionKey: string;
  userCount: number;
  isSystemRole: boolean;
}

export interface PermissionMatrixRowResponse {
  moduleKey: string;
  accessByRole: Record<UserRole, boolean>;
}

export interface RoleCatalogResponse {
  roles: RoleMetadataResponse[];
  permissionMatrix: PermissionMatrixRowResponse[];
}

export interface AuditLogItem {
  id: number;
  auditId: string;
  createdAt: string;
  action: string;
  resourceType: string;
  resourceId: string | null;
  message: string;
  actorUserId: number | null;
  actorPhone: string | null;
  actorRole: string | null;
  actorType: string;
  sourceSystem: string;
  targetSystem: string | null;
  channel: string;
  ipAddress: string | null;
  userAgent: string | null;
  status: string;
  failureReason: string | null;
  correlationId: string | null;
  requestId: string | null;
  oldValueJson: string | null;
  newValueJson: string | null;
  metadataJson: string | null;
}

export interface AuditLogPage {
  page: number;
  pageSize: number;
  total: number;
  actions: string[];
  items: AuditLogItem[];
}

export interface AuditConfigDto {
  retentionMinutes: number;
}

export interface ListAuditLogParams {
  page?: number;
  pageSize?: number;
  from?: string;
  to?: string;
  action?: string;
  actorPhone?: string;
  ip?: string;
  status?: string;
  resourceType?: string;
  resourceId?: string;
  correlationId?: string;
  search?: string;
}

export interface DeleteAuditRangeParams {
  before?: string;
  action?: string;
}

// ─── Admin API ───────────────────────────────────────────────
//
// Upload folder convention — ALWAYS pass `folder` to uploadImage / uploadVideo:
//   projects/<slug>   activities/<slug>   news/<slug>
//   slideshow         logos               services        about
// Omitting `folder` uploads to the flat root and breaks the organised structure.

export const adminApi = {
  uploadImage: (file: File, previousImageUrl?: string, folder?: string) => {
    const formData = new FormData();
    formData.append("file", file);
    if (previousImageUrl) formData.append("previousImageUrl", previousImageUrl);
    if (folder) formData.append("folder", folder);
    return api.post<{ imageUrl: string }>("/system/upload-image", formData);
  },

  uploadVideo: (file: File, previousImageUrl?: string, folder?: string) => {
    const formData = new FormData();
    formData.append("file", file);
    if (previousImageUrl) formData.append("previousImageUrl", previousImageUrl);
    if (folder) formData.append("folder", folder);
    return api.post<{ mediaUrl: string }>("/system/upload-video", formData);
  },

  uploadDocument: (file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return api.post<{ cvUrl: string }>("/system/upload-cv", formData);
  },

  // Activities / Posts
  createActivity: (data: UpsertActivityRequest) =>
    api.post("/activities", data),
  updateActivity: (id: number, data: UpsertActivityRequest) =>
    api.put(`/activities/${id}`, data),
  deleteActivity: (id: number) =>
    api.delete(`/activities/${id}`),

  // Activity categories
  getActivityCategories: (includeInactive = false) =>
    api.get<ActivityCategoryResponse[]>(`/activity-categories?includeInactive=${includeInactive}`),
  createActivityCategory: (data: UpsertActivityCategoryRequest) =>
    api.post<ActivityCategoryResponse>("/activity-categories", data),
  updateActivityCategory: (id: number, data: UpsertActivityCategoryRequest) =>
    api.put<ActivityCategoryResponse>(`/activity-categories/${id}`, data),
  deleteActivityCategory: (id: number) =>
    api.delete(`/activity-categories/${id}`),

  // News categories
  getNewsCategories: (includeInactive = false) =>
    api.get<NewsCategoryResponse[]>(`/news-categories?includeInactive=${includeInactive}`),
  createNewsCategory: (data: UpsertNewsCategoryRequest) =>
    api.post<NewsCategoryResponse>("/news-categories", data),
  updateNewsCategory: (id: number, data: UpsertNewsCategoryRequest) =>
    api.put<NewsCategoryResponse>(`/news-categories/${id}`, data),
  deleteNewsCategory: (id: number) =>
    api.delete(`/news-categories/${id}`),

  // Project categories
  getProjectCategories: (includeInactive = false) =>
    api.get<ProjectCategoryResponse[]>(`/project-categories?includeInactive=${includeInactive}`),
  createProjectCategory: (data: UpsertProjectCategoryRequest) =>
    api.post<ProjectCategoryResponse>("/project-categories", data),
  updateProjectCategory: (id: number, data: UpsertProjectCategoryRequest) =>
    api.put<ProjectCategoryResponse>(`/project-categories/${id}`, data),
  deleteProjectCategory: (id: number) =>
    api.delete(`/project-categories/${id}`),

  // Employment types
  getEmploymentTypes: (includeInactive = false) =>
    api.get<EmploymentTypeResponse[]>(`/employment-types?includeInactive=${includeInactive}`),
  createEmploymentType: (data: UpsertEmploymentTypeRequest) =>
    api.post<EmploymentTypeResponse>("/employment-types", data),
  updateEmploymentType: (id: number, data: UpsertEmploymentTypeRequest) =>
    api.put<EmploymentTypeResponse>(`/employment-types/${id}`, data),
  deleteEmploymentType: (id: number) =>
    api.delete(`/employment-types/${id}`),

  // Recruitment dropdown options (experience-level, benefit)
  getRecruitmentDropdownOptions: (type: string, includeInactive = false) =>
    api.get<RecruitmentDropdownOptionResponse[]>(`/recruitment-dropdown-options?type=${encodeURIComponent(type)}&includeInactive=${includeInactive}`),
  createRecruitmentDropdownOption: (data: UpsertRecruitmentDropdownOptionRequest) =>
    api.post<RecruitmentDropdownOptionResponse>("/recruitment-dropdown-options", data),
  updateRecruitmentDropdownOption: (id: number, data: UpsertRecruitmentDropdownOptionRequest) =>
    api.put<RecruitmentDropdownOptionResponse>(`/recruitment-dropdown-options/${id}`, data),
  deleteRecruitmentDropdownOption: (id: number) =>
    api.delete(`/recruitment-dropdown-options/${id}`),

  // News
  createNews: (data: UpsertNewsRequest) =>
    api.post("/news", data),
  updateNews: (id: number, data: UpsertNewsRequest) =>
    api.put(`/news/${id}`, data),
  deleteNews: (id: number) =>
    api.delete(`/news/${id}`),

  // Projects
  createProject: (data: UpsertProjectRequest) =>
    api.post("/projects", data),
  updateProject: (id: number, data: UpsertProjectRequest) =>
    api.put(`/projects/${id}`, data),
  deleteProject: (id: number) =>
    api.delete(`/projects/${id}`),

  // Logos
  createLogo: (data: UpsertLogoRequest) =>
    api.post("/logos", data),
  updateLogo: (id: number, data: UpsertLogoRequest) =>
    api.put(`/logos/${id}`, data),
  deleteLogo: (id: number) =>
    api.delete(`/logos/${id}`),

  // Processes
  createProcess: (data: UpsertProcessRequest) =>
    api.post("/processes", data),
  updateProcess: (id: number, data: UpsertProcessRequest) =>
    api.put(`/processes/${id}`, data),
  deleteProcess: (id: number) =>
    api.delete(`/processes/${id}`),
  uploadProcessImage: (file: File, groupKey: string) => {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("groupKey", groupKey);
    return api.post<ProcessAssetUploadResponse>("/processes/upload-image", fd, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  uploadProcessFile: (file: File, groupKey: string) => {
    const fd = new FormData();
    fd.append("file", file);
    fd.append("groupKey", groupKey);
    return api.post<ProcessAssetUploadResponse>("/processes/upload-file", fd, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },

  // Services
  createService: (data: UpsertServiceAdminRequest) =>
    api.post<ServiceResponse>('/services', data),
  updateService: (id: number, data: UpsertServiceAdminRequest) =>
    api.put<ServiceResponse>(`/services/${id}`, data),
  deleteService: (id: number) =>
    api.delete(`/services/${id}`),

  // Slideshow
  getSlideshow: (lang = "vi", activeOnly = false) =>
    api.get<SlideshowAdminResponse[]>(`/slideshow?lang=${lang}&activeOnly=${activeOnly}`),
  createSlideshow: (data: UpsertSlideshowRequest) =>
    api.post("/slideshow", data),
  updateSlideshow: (id: number, data: UpsertSlideshowRequest) =>
    api.put(`/slideshow/${id}`, data),
  deleteSlideshow: (id: number) =>
    api.delete(`/slideshow/${id}`),

  // About sections
  getAboutSections: (activeOnly = false) =>
    api.get<AboutSectionAdminResponse[]>(`/about-sections?activeOnly=${activeOnly}`),
  createAboutSection: (data: UpsertAboutSectionRequest) =>
    api.post<AboutSectionAdminResponse>("/about-sections", data),
  updateAboutSection: (id: number, data: UpsertAboutSectionRequest) =>
    api.put<AboutSectionAdminResponse>(`/about-sections/${id}`, data),
  deleteAboutSection: (id: number) =>
    api.delete(`/about-sections/${id}`),

  // Job positions
  getJobPositions: (includeInactive = false) =>
    api.get<JobPositionResponse[]>(`/job-positions?includeInactive=${includeInactive}`),
  createJobPosition: (data: UpsertJobPositionRequest) =>
    api.post<JobPositionResponse>("/job-positions", data),
  updateJobPosition: (id: number, data: UpsertJobPositionRequest) =>
    api.put<JobPositionResponse>(`/job-positions/${id}`, data),
  deleteJobPosition: (id: number) =>
    api.delete(`/job-positions/${id}`),

  // Job applications
  getJobApplications: (positionId?: number, status?: string) => {
    const params = new URLSearchParams();
    if (positionId) params.append("positionId", String(positionId));
    if (status) params.append("status", status);
    return api.get<JobApplicationResponse[]>(`/job-applications?${params}`);
  },
  updateApplicationStatus: (id: number, status: string) =>
    api.patch<JobApplicationResponse>(`/job-applications/${id}/status`, { status }),
  deleteApplication: (id: number) =>
    api.delete(`/job-applications/${id}`),

  // Public: submit application (no auth needed but goes through same axios instance)
  submitJobApplication: (data: SubmitJobApplicationRequest) =>
    api.post<JobApplicationResponse>("/job-applications", data),

  // Email templates
  getEmailTemplates: () =>
    api.get<EmailTemplatesResponse>("/site-settings/email-templates"),
  updateEmailTemplates: (data: UpdateEmailTemplatesRequest) =>
    api.put<EmailTemplatesResponse>("/site-settings/email-templates", data),

  // OTP settings
  getOtpSettings: () =>
    api.get<OtpSettingsResponse>("/site-settings/otp-settings"),
  updateOtpSettings: (data: UpdateOtpSettingsRequest) =>
    api.put<OtpSettingsResponse>("/site-settings/otp-settings", data),

  // Map embed (Google Maps URL shown on Contact page)
  getMapEmbed: () =>
    api.get<MapEmbedResponse>("/site-settings/map-embed"),
  updateMapEmbed: (data: UpdateMapEmbedRequest) =>
    api.put<MapEmbedResponse>("/site-settings/map-embed", data),

  // Contact messages
  getContacts: (replied?: boolean) => {
    const params = new URLSearchParams();
    if (replied !== undefined) params.append("replied", String(replied));
    return api.get<ContactMessageResponse[]>(`/contacts?${params}`);
  },
  replyContact: (id: number, replyContent: string) =>
    api.post<ContactMessageResponse>(`/contacts/${id}/reply`, { replyContent }),
  markContactReplied: (id: number) =>
    api.patch<ContactMessageResponse>(`/contacts/${id}/mark-replied`),
  deleteContact: (id: number) =>
    api.delete(`/contacts/${id}`),

  // CRM leads
  listLeads: (params: LeadListParams = {}) => {
    const query = new URLSearchParams();
    if (params.status) query.append("status", params.status);
    if (params.sourceCode) query.append("sourceCode", params.sourceCode);
    if (params.ownerUserId != null) query.append("ownerUserId", String(params.ownerUserId));
    if (params.search) query.append("search", params.search);
    if (params.page) query.append("page", String(params.page));
    if (params.pageSize) query.append("pageSize", String(params.pageSize));
    const qs = query.toString();
    return api.get<LeadListResponse>(`/leads${qs ? `?${qs}` : ""}`);
  },
  getLead: (id: number) => api.get<LeadResponse>(`/leads/${id}`),
  createLead: (body: CreateLeadRequest) =>
    api.post<LeadResponse>("/leads", body),
  updateLead: (id: number, body: UpdateLeadRequest) =>
    api.put<LeadResponse>(`/leads/${id}`, body),
  deleteLead: (id: number) => api.delete(`/leads/${id}`),
  convertLead: (id: number, body: ConvertLeadRequest = {}) =>
    api.post<LeadResponse>(`/leads/${id}/convert`, body),
  addLeadActivity: (id: number, body: CreateLeadActivityRequest) =>
    api.post<LeadActivityResponse>(`/leads/${id}/activities`, body),

  // CRM customers
  listCustomers: (params: CustomerListParams = {}) => {
    const q = new URLSearchParams();
    if (params.type) q.append("type", params.type);
    if (params.status) q.append("status", params.status);
    if (params.ownerUserId != null) q.append("ownerUserId", String(params.ownerUserId));
    if (params.sourceCode) q.append("sourceCode", params.sourceCode);
    if (params.search) q.append("search", params.search);
    if (params.createdFrom) q.append("createdFrom", params.createdFrom);
    if (params.createdTo) q.append("createdTo", params.createdTo);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<CustomerListResponse>(`/customers${qs ? `?${qs}` : ""}`);
  },
  getCustomer: (id: number) => api.get<CustomerResponse>(`/customers/${id}`),
  createCustomer: (body: CreateCustomerRequest) => api.post<CustomerResponse>("/customers", body),
  updateCustomer: (id: number, body: UpdateCustomerRequest) => api.put<CustomerResponse>(`/customers/${id}`, body),
  deleteCustomer: (id: number) => api.delete(`/customers/${id}`),
  upsertCustomerContact: (id: number, body: UpsertCustomerContactRequest) =>
    api.post<CustomerContactResponse>(`/customers/${id}/contacts`, body),
  deleteCustomerContact: (id: number, contactId: number) =>
    api.delete(`/customers/${id}/contacts/${contactId}`),
  addCustomerActivity: (id: number, body: CreateCustomerActivityRequest) =>
    api.post<CustomerActivityResponse>(`/customers/${id}/activities`, body),

  // CRM opportunities (NIH-83)
  listOpportunities: (params: OpportunityListParams = {}) => {
    const q = new URLSearchParams();
    if (params.stage) q.append("stage", params.stage);
    if (params.customerId != null) q.append("customerId", String(params.customerId));
    if (params.ownerUserId != null) q.append("ownerUserId", String(params.ownerUserId));
    if (params.expectedCloseFrom) q.append("expectedCloseFrom", params.expectedCloseFrom);
    if (params.expectedCloseTo) q.append("expectedCloseTo", params.expectedCloseTo);
    if (params.minValue != null) q.append("minValue", String(params.minValue));
    if (params.maxValue != null) q.append("maxValue", String(params.maxValue));
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<OpportunityListResponse>(`/opportunities${qs ? `?${qs}` : ""}`);
  },
  getOpportunityPipeline: (params: OpportunityPipelineParams = {}) => {
    const q = new URLSearchParams();
    if (params.ownerUserId != null) q.append("ownerUserId", String(params.ownerUserId));
    if (params.customerId != null) q.append("customerId", String(params.customerId));
    if (params.expectedCloseFrom) q.append("expectedCloseFrom", params.expectedCloseFrom);
    if (params.expectedCloseTo) q.append("expectedCloseTo", params.expectedCloseTo);
    if (params.minValue != null) q.append("minValue", String(params.minValue));
    if (params.maxValue != null) q.append("maxValue", String(params.maxValue));
    const qs = q.toString();
    return api.get<OpportunityPipelineResponse>(`/opportunities/pipeline${qs ? `?${qs}` : ""}`);
  },
  getOpportunity: (id: number) => api.get<OpportunityResponse>(`/opportunities/${id}`),
  createOpportunity: (body: CreateOpportunityRequest) =>
    api.post<OpportunityResponse>("/opportunities", body),
  updateOpportunity: (id: number, body: UpdateOpportunityRequest) =>
    api.put<OpportunityResponse>(`/opportunities/${id}`, body),
  changeOpportunityStage: (id: number, body: ChangeOpportunityStageRequest) =>
    api.patch<OpportunityResponse>(`/opportunities/${id}/stage`, body),
  deleteOpportunity: (id: number) => api.delete(`/opportunities/${id}`),
  addOpportunityActivity: (id: number, body: AddOpportunityActivityRequest) =>
    api.post<OpportunityActivityResponse>(`/opportunities/${id}/activities`, body),

  // Quotes (NIH-84)
  listQuotes: (params: QuoteListParams = {}) => {
    const q = new URLSearchParams();
    if (params.status) q.append("status", params.status);
    if (params.opportunityId != null) q.append("opportunityId", String(params.opportunityId));
    if (params.customerId != null) q.append("customerId", String(params.customerId));
    if (params.ownerUserId != null) q.append("ownerUserId", String(params.ownerUserId));
    if (params.createdFrom) q.append("createdFrom", params.createdFrom);
    if (params.createdTo) q.append("createdTo", params.createdTo);
    if (params.minValue != null) q.append("minValue", String(params.minValue));
    if (params.maxValue != null) q.append("maxValue", String(params.maxValue));
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<QuoteListResponse>(`/quotes${qs ? `?${qs}` : ""}`);
  },
  getQuote: (id: number) => api.get<QuoteResponse>(`/quotes/${id}`),
  getQuoteVersions: (id: number) => api.get<QuoteVersionsResponse>(`/quotes/${id}/versions`),
  createQuote: (body: CreateQuoteRequest) => api.post<QuoteResponse>("/quotes", body),
  updateQuote: (id: number, body: UpdateQuoteRequest) => api.put<QuoteResponse>(`/quotes/${id}`, body),
  submitQuote: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/submit`, body),
  approveQuote: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/approve`, body),
  rejectQuoteInternal: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/reject-internal`, body),
  sendQuoteToCustomer: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/send`, body),
  markQuoteCustomerApproved: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/customer-approve`, body),
  markQuoteCustomerRejected: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/customer-reject`, body),
  cancelQuote: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/cancel`, body),
  extendQuoteValidity: (id: number, body: ExtendQuoteValidityRequest) =>
    api.post<QuoteResponse>(`/quotes/${id}/extend-validity`, body),
  deleteQuote: (id: number) => api.delete(`/quotes/${id}`),

  // Capability documents (NIH-98)
  listCapabilityDocuments: (params: CapabilityDocumentListParams = {}) => {
    const q = new URLSearchParams();
    if (params.tagCode) q.append("tagCode", params.tagCode);
    if (params.issuedYear != null) q.append("issuedYear", String(params.issuedYear));
    if (params.search) q.append("search", params.search);
    if (params.expiryState) q.append("expiryState", params.expiryState);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<CapabilityDocumentListResponse>(`/capability-documents${qs ? `?${qs}` : ""}`);
  },
  getCapabilityDocument: (id: number) =>
    api.get<CapabilityDocumentDetailResponse>(`/capability-documents/${id}`),
  uploadCapabilityDocument: (file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return api.post<CapabilityDocumentUploadResponse>(
      "/capability-documents/upload",
      formData,
    );
  },
  createCapabilityDocument: (body: UpsertCapabilityDocumentRequest) =>
    api.post<CapabilityDocumentResponse>("/capability-documents", body),
  updateCapabilityDocument: (id: number, body: UpsertCapabilityDocumentRequest) =>
    api.put<CapabilityDocumentResponse>(`/capability-documents/${id}`, body),
  replaceCapabilityDocumentFile: (id: number, body: ReplaceCapabilityDocumentFileRequest) =>
    api.post<CapabilityDocumentResponse>(`/capability-documents/${id}/replace-file`, body),
  deleteCapabilityDocument: (id: number) =>
    api.delete(`/capability-documents/${id}`),
  downloadCapabilityDocumentsZip: (ids: number[]) =>
    api.post<Blob>(`/capability-documents/download-zip`, { ids }, { responseType: "blob" }),

  // Tenders (NIH-85 / NIH-95 / NIH-96)
  listTenders: (params: TenderListParams = {}) => {
    const q = new URLSearchParams();
    if (params.status) q.append("status", params.status);
    if (params.customerId != null) q.append("customerId", String(params.customerId));
    if (params.preparerUserId != null) q.append("preparerUserId", String(params.preparerUserId));
    if (params.openingMonth != null) q.append("openingMonth", String(params.openingMonth));
    if (params.openingYear != null) q.append("openingYear", String(params.openingYear));
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<TenderListResponse>(`/tenders${qs ? `?${qs}` : ""}`);
  },
  getTender: (id: number) => api.get<TenderResponse>(`/tenders/${id}`),
  createTender: (body: CreateTenderRequest) => api.post<TenderResponse>("/tenders", body),
  updateTender: (id: number, body: UpdateTenderRequest) => api.put<TenderResponse>(`/tenders/${id}`, body),
  deleteTender: (id: number) => api.delete(`/tenders/${id}`),

  // Tender detail-page workflow (NIH-97)
  updateTenderChecklistItem: (
    tenderId: number,
    itemId: number,
    body: UpdateTenderChecklistItemRequest,
  ) => api.patch<TenderResponse>(`/tenders/${tenderId}/checklist/${itemId}`, body),
  uploadTenderChecklistFile: (tenderId: number, itemId: number, file: File) => {
    const form = new FormData();
    form.append("file", file);
    return api.post<TenderResponse>(
      `/tenders/${tenderId}/checklist/${itemId}/upload`,
      form,
      { headers: { "Content-Type": "multipart/form-data" } },
    );
  },
  attachTenderChecklistFromLibrary: (
    tenderId: number,
    body: AttachTenderChecklistFromLibraryRequest,
  ) => api.post<TenderResponse>(`/tenders/${tenderId}/checklist/attach-from-library`, body),
  markTenderWon: (tenderId: number, body: MarkTenderWonRequest) =>
    api.post<TenderResponse>(`/tenders/${tenderId}/mark-won`, body),
  markTenderLost: (tenderId: number, body: MarkTenderLostRequest) =>
    api.post<TenderResponse>(`/tenders/${tenderId}/mark-lost`, body),
  getTenderTimeline: (tenderId: number, limit = 100) =>
    api.get<TenderTimelineEvent[]>(`/tenders/${tenderId}/timeline`, { params: { limit } }),

  // Master data (read-only helper — full CRUD lives in NIH-379 admin page)
  getMasterDataOptions: (category: string) =>
    api.get<MasterDataOption[]>(`/master-data/${encodeURIComponent(category)}`),

  // Master data admin (NIH-230)
  listMasterDataCategories: () =>
    api.get<MasterDataCategory[]>("/master-data/categories"),
  listMasterDataByCategory: (category: string, includeInactive = false) =>
    api.get<MasterDataOption[]>(`/master-data/${encodeURIComponent(category)}`, {
      params: { includeInactive },
    }),
  createMasterDataOption: (category: string, body: UpsertMasterDataOptionRequest) =>
    api.post<MasterDataOption>(`/master-data/${encodeURIComponent(category)}`, body),
  updateMasterDataOption: (id: number, body: UpsertMasterDataOptionRequest) =>
    api.put<MasterDataOption>(`/master-data/options/${id}`, body),
  deleteMasterDataOption: (id: number) =>
    api.delete(`/master-data/options/${id}`),

  // Workflow config (NIH-225)
  listWorkflows: (includeInactive = false) =>
    api.get<WorkflowConfig[]>("/workflows", { params: { includeInactive } }),
  getWorkflow: (id: number) =>
    api.get<WorkflowConfig>(`/workflows/${id}`),
  createWorkflow: (body: UpsertWorkflowConfigRequest) =>
    api.post<WorkflowConfig>("/workflows", body),
  updateWorkflow: (id: number, body: UpsertWorkflowConfigRequest) =>
    api.put<WorkflowConfig>(`/workflows/${id}`, body),
  deleteWorkflow: (id: number) =>
    api.delete(`/workflows/${id}`),

  // Contracts (NIH-102)
  listContracts: (params?: ContractListParams) =>
    api.get<ContractListResponse>("/contracts", { params }),
  getContract: (id: number) =>
    api.get<ContractResponse>(`/contracts/${id}`),
  createContract: (body: UpsertContractRequest) =>
    api.post<ContractResponse>("/contracts", body),
  updateContract: (id: number, body: UpsertContractRequest) =>
    api.put<ContractResponse>(`/contracts/${id}`, body),
  deleteContract: (id: number) =>
    api.delete(`/contracts/${id}`),

  // Contract state / milestones / VO / attachments / timeline (NIH-104)
  transitionContract: (id: number, newStatus: ContractStatus) =>
    api.post<ContractResponse>(`/contracts/${id}/transition`, { newStatus }),
  updateMilestoneStatus: (
    contractId: number,
    milestoneId: number,
    status: PaymentMilestoneStatus,
  ) =>
    api.patch<ContractResponse>(
      `/contracts/${contractId}/milestones/${milestoneId}/status`,
      { status },
    ),
  listContractAppendices: (contractId: number) =>
    api.get<ContractAppendixResponse[]>(`/contracts/${contractId}/appendices`),
  createContractAppendix: (contractId: number, body: UpsertContractAppendixRequest) =>
    api.post<ContractAppendixResponse>(`/contracts/${contractId}/appendices`, body),
  updateContractAppendix: (
    contractId: number,
    voId: number,
    body: UpsertContractAppendixRequest,
  ) =>
    api.put<ContractAppendixResponse>(`/contracts/${contractId}/appendices/${voId}`, body),
  submitContractAppendix: (contractId: number, voId: number) =>
    api.post<ContractAppendixResponse>(`/contracts/${contractId}/appendices/${voId}/submit`),
  approveContractAppendix: (contractId: number, voId: number, note?: string) =>
    api.post<ContractAppendixResponse>(
      `/contracts/${contractId}/appendices/${voId}/approve`,
      { note },
    ),
  rejectContractAppendix: (contractId: number, voId: number, note: string) =>
    api.post<ContractAppendixResponse>(
      `/contracts/${contractId}/appendices/${voId}/reject`,
      { note },
    ),
  deleteContractAppendix: (contractId: number, voId: number) =>
    api.delete(`/contracts/${contractId}/appendices/${voId}`),
  uploadContractAppendixFile: (contractId: number, file: File) => {
    const form = new FormData();
    form.append("file", file);
    return api.post<AppendixUploadResponse>(
      `/contracts/${contractId}/appendices/files`,
      form,
      { headers: { "Content-Type": "multipart/form-data" } },
    );
  },
  listContractAttachments: (contractId: number) =>
    api.get<ContractAttachmentResponse[]>(`/contracts/${contractId}/attachments`),
  uploadContractAttachment: (
    contractId: number,
    file: File,
    kind: ContractAttachmentKind,
    label?: string,
  ) => {
    const form = new FormData();
    form.append("file", file);
    form.append("kind", kind);
    if (label) form.append("label", label);
    return api.post<ContractAttachmentResponse>(
      `/contracts/${contractId}/attachments`,
      form,
      { headers: { "Content-Type": "multipart/form-data" } },
    );
  },
  deleteContractAttachment: (contractId: number, attachmentId: number) =>
    api.delete(`/contracts/${contractId}/attachments/${attachmentId}`),
  getContractTimeline: (contractId: number, limit = 100) =>
    api.get<ContractTimelineEvent[]>(`/contracts/${contractId}/timeline`, {
      params: { limit },
    }),

  // Users / RBAC
  getUsers: (params: { skip?: number; take?: number; search?: string; role?: string }) =>
    api.get<UserListResponse>("/users", { params }),
  getUser: (id: number) =>
    api.get<UserDetailResponse>(`/users/${id}`),
  createUser: (data: CreateUserRequest, idempotencyKey?: string) =>
    api.post<UserDetailResponse>("/users", data, withIdempotencyKey(idempotencyKey)),
  updateUser: (id: number, data: UpdateUserRequest, idempotencyKey?: string) =>
    api.put<UserDetailResponse>(`/users/${id}`, data, withIdempotencyKey(idempotencyKey)),
  toggleUserActive: (id: number) =>
    api.patch<UserDetailResponse>(`/users/${id}/toggle-active`),
  deleteUser: (id: number) =>
    api.delete(`/users/${id}`),
  getUserRoles: () =>
    api.get<RoleCatalogResponse>("/users/roles"),

  // Audit log
  listAuditLogs: (params: ListAuditLogParams = {}) => {
    const query = new URLSearchParams();
    if (params.page) query.append("page", String(params.page));
    if (params.pageSize) query.append("pageSize", String(params.pageSize));
    if (params.from) query.append("from", params.from);
    if (params.to) query.append("to", params.to);
    if (params.action) query.append("action", params.action);
    if (params.actorPhone) query.append("actorPhone", params.actorPhone);
    if (params.ip) query.append("ip", params.ip);
    if (params.status) query.append("status", params.status);
    if (params.resourceType) query.append("resourceType", params.resourceType);
    if (params.resourceId) query.append("resourceId", params.resourceId);
    if (params.correlationId) query.append("correlationId", params.correlationId);
    if (params.search) query.append("search", params.search);
    return api.get<AuditLogPage>(`/audit-logs?${query}`);
  },
  deleteAuditLog: (id: number) =>
    api.delete(`/audit-logs/${id}`),
  deleteAuditLogRange: (params: DeleteAuditRangeParams) => {
    const query = new URLSearchParams();
    if (params.before) query.append("before", params.before);
    if (params.action) query.append("action", params.action);
    return api.delete<{ deleted: number }>(`/audit-logs?${query}`);
  },
  getAuditConfig: () =>
    api.get<AuditConfigDto>("/audit-logs/config"),
  updateAuditConfig: (data: AuditConfigDto) =>
    api.put<AuditConfigDto>("/audit-logs/config", data),
};

// ─── Slug helper ─────────────────────────────────────────────

export const slugify = (input: string) =>
  input
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/(^-|-$)+/g, "")
    .slice(0, 80) || `item-${Date.now()}`;
