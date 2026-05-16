import api from "@/lib/api";

// ─── Request types ───────────────────────────────────────────

export interface UpsertActivityRequest {
  slug: string;
  date: string;
  imageUrl: string;
  category: string;
  author?: string;
  title: string;
  excerpt: string;
  content: string[];
  sortOrder?: number;
}

export interface UpsertNewsRequest {
  slug: string;
  date: string;
  imageUrl: string;
  category: string;
  title: string;
  excerpt: string;
  content: string[];
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
  description?: string;
  challenges?: string[];
  solutions?: string[];
  highlights?: { label: string; value: string }[];
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
  isActive: boolean;
  sortOrder: number;
  applicationCount: number;
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

export type UserRole = "SUPER_ADMIN" | "ADMIN" | "USER";

export interface UserListItemResponse {
  id: number;
  phoneNumber: string;
  fullName?: string;
  email?: string;
  role: UserRole;
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
  email?: string;
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

  // Employment types
  getEmploymentTypes: (includeInactive = false) =>
    api.get<EmploymentTypeResponse[]>(`/employment-types?includeInactive=${includeInactive}`),
  createEmploymentType: (data: UpsertEmploymentTypeRequest) =>
    api.post<EmploymentTypeResponse>("/employment-types", data),
  updateEmploymentType: (id: number, data: UpsertEmploymentTypeRequest) =>
    api.put<EmploymentTypeResponse>(`/employment-types/${id}`, data),
  deleteEmploymentType: (id: number) =>
    api.delete(`/employment-types/${id}`),

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

  // Users / RBAC
  getUsers: (params: { skip?: number; take?: number; search?: string; role?: string }) =>
    api.get<UserListResponse>("/users", { params }),
  getUser: (id: number) =>
    api.get<UserDetailResponse>(`/users/${id}`),
  createUser: (data: CreateUserRequest) =>
    api.post<UserDetailResponse>("/users", data),
  updateUser: (id: number, data: UpdateUserRequest) =>
    api.put<UserDetailResponse>(`/users/${id}`, data),
  toggleUserActive: (id: number) =>
    api.patch<UserDetailResponse>(`/users/${id}/toggle-active`),
  deleteUser: (id: number) =>
    api.delete(`/users/${id}`),
  getUserRoles: () =>
    api.get<RoleCatalogResponse>("/users/roles"),
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
