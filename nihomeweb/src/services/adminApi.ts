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

export interface UpsertActivityCategoryRequest {
  name: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface ActivityCategoryResponse {
  id: number;
  name: string;
  isActive: boolean;
  sortOrder: number;
}

export interface UpsertProjectCategoryRequest {
  name: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface ProjectCategoryResponse {
  id: number;
  name: string;
  isActive: boolean;
  sortOrder: number;
}

export interface UpsertNewsCategoryRequest {
  name: string;
  isActive?: boolean;
  sortOrder?: number;
}

export interface NewsCategoryResponse {
  id: number;
  name: string;
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

export const adminApi = {
  uploadImage: (file: File, previousImageUrl?: string) => {
    const formData = new FormData();
    formData.append("file", file);
    if (previousImageUrl) {
      formData.append("previousImageUrl", previousImageUrl);
    }
    return api.post<{ imageUrl: string }>("/system/upload-image", formData);
  },

  uploadVideo: (file: File, previousImageUrl?: string) => {
    const formData = new FormData();
    formData.append("file", file);
    if (previousImageUrl) {
      formData.append("previousImageUrl", previousImageUrl);
    }
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
    api.post<LeadResponse>("/leads", body, withIdempotencyKey()),
  updateLead: (id: number, body: UpdateLeadRequest) =>
    api.put<LeadResponse>(`/leads/${id}`, body),
  deleteLead: (id: number) => api.delete(`/leads/${id}`),
  convertLead: (id: number, body: ConvertLeadRequest = {}) =>
    api.post<LeadResponse>(`/leads/${id}/convert`, body),
  addLeadActivity: (id: number, body: CreateLeadActivityRequest) =>
    api.post<LeadActivityResponse>(`/leads/${id}/activities`, body),

  // Master data (read-only helper — full CRUD lives in NIH-379 admin page)
  getMasterDataOptions: (category: string) =>
    api.get<MasterDataOption[]>(`/master-data/${encodeURIComponent(category)}`),

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
