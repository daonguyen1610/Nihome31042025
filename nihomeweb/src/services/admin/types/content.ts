import type { ContentItem } from "@/services/contentApi";

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

export interface RoleGroupOptionResponse {
  id: number;
  code: string;
  labelKey: string;
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
  quoteEmailSubjectTemplate: string | null;
  quoteEmailBodyTemplate: string | null;
  projectScheduleEmailSubjectTemplate: string;
  projectScheduleEmailBodyTemplate: string;
}

export interface ProjectScheduleEmailReviewItem {
  id: number;
  projectId: number;
  revisionId: number;
  version: number;
  projectCode: string;
  attemptCount: number;
  lastAttemptAt: string | null;
  toEmail?: string | null;
  subject?: string | null;
  body?: string | null;
}

export interface ProjectScheduleEmailPreview {
  recipientUserId: number;
  toEmail: string;
  subject: string;
  body: string;
}

export interface ProjectScheduleEmailPreviewResponse {
  previewToken: string;
  emails: ProjectScheduleEmailPreview[];
}

export interface WorkPackageApprovalPreview {
  previewToken: string;
  toEmail: string;
  subject: string;
  body: string;
  ccEmails: string[];
}

export interface QuoteApprovalTemplates {
  subject: string;
  body: string;
  decisionSubject: string;
  decisionBody: string;
}

export interface ContractReminderTemplates {
  renewalSubject: string;
  renewalBody: string;
  endSubject: string;
  endBody: string;
}

export interface ContractReminder {
  id: number;
  kind: "Renewal" | "End";
  reminderDate: string;
  toEmail: string;
  subject: string;
  body: string;
  status: "Scheduled" | "Sending" | "Sent" | "Cancelled" | "NeedsReview";
  errorCode: string | null;
  createdAt: string;
  attemptedAt: string | null;
  sentAt: string | null;
}

export interface ContractReminderPreview {
  kind: "Renewal" | "End";
  reminderDate: string;
  previewToken: string;
  contactId: number;
  toEmail: string;
  subject: string;
  body: string;
  contacts: { id: number; name: string; email: string }[];
}

export interface UpdateEmailTemplatesRequest {
  newApplicationEmailSubjectTemplate?: string | null;
  newApplicationEmailBodyTemplate?: string | null;
  notificationEmail?: string | null;
  otpEmailSubjectTemplate?: string | null;
  otpEmailBodyTemplate?: string | null;
  quoteEmailSubjectTemplate?: string | null;
  quoteEmailBodyTemplate?: string | null;
  projectScheduleEmailSubjectTemplate?: string | null;
  projectScheduleEmailBodyTemplate?: string | null;
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

export interface DisplaySettingsResponse {
  timeZoneId: string;
  currency: "VND" | "USD";
  /** VND per 1 USD used to convert recorded amounts for display. */
  usdVndRate: number;
}

export interface UpdateDisplaySettingsRequest {
  timeZoneId: string;
  currency: "VND" | "USD";
  usdVndRate: number;
}

export interface TimeZoneOption {
  id: string;
  label: string;
  utcOffsetMinutes: number;
}

export interface GoogleDriveAdminStatusResponse {
  status:
    "Disabled" | "Connected" | "ReadOnly" | "InvalidRoot" | "ReconnectRequired" | "Unavailable";
  oauthConfigured: boolean;
  hasStoredCredential: boolean;
  accountEmail: string | null;
  connectedAt: string | null;
  rootFolderName: string | null;
  rootFolderLink: string | null;
  error: string | null;
}

export interface GoogleDriveOAuthStartResponse {
  authorizationUrl: string;
}

export interface GoogleDriveDisconnectResponse {
  hadStoredCredential: boolean;
  providerRevoked: boolean;
}

export interface GoogleDriveFolderConfiguration {
  surveyMedia: string;
  crmPreDesign: string;
  designConcept: string;
  designBasic: string;
  designShopDrawing: string;
  legalPermits: string;
  constructionAcceptance: string;
  procurement: string;
  financeContracts: string;
}

export interface GoogleDriveAdminConfigurationResponse {
  enabled: boolean;
  clientId: string;
  hasClientSecret: boolean;
  hasRefreshToken: boolean;
  oAuthRedirectUri: string;
  frontendReturnUrl: string;
  rootFolderId: string;
  instanceId: string;
  applicationName: string;
  folders: GoogleDriveFolderConfiguration;
  supportsAllDrives: boolean;
  deleteDriveFilesOnWebDelete: boolean;
  deleteWebRecordsOnDriveDelete: boolean;
  pollIntervalSeconds: number;
  accountEmail: string | null;
  connectedAt: string | null;
  rowVersion?: string;
}

export interface UpdateGoogleDriveConfigurationRequest {
  enabled: boolean;
  clientId: string;
  clientSecret?: string;
  oAuthRedirectUri: string;
  frontendReturnUrl: string;
  rootFolderId: string;
  instanceId: string;
  applicationName: string;
  folders: GoogleDriveFolderConfiguration;
  supportsAllDrives: boolean;
  deleteDriveFilesOnWebDelete: boolean;
  deleteWebRecordsOnDriveDelete: boolean;
  pollIntervalSeconds: number;
  rowVersion: string;
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
