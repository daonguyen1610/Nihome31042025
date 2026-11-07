import api from "@/lib/api";
import type { ServiceResponse } from "@/services/contentApi";
import type {
  UpsertActivityRequest,
  ActivityCategoryResponse,
  UpsertActivityCategoryRequest,
  NewsCategoryResponse,
  UpsertNewsCategoryRequest,
  ProjectCategoryResponse,
  UpsertProjectCategoryRequest,
  EmploymentTypeResponse,
  UpsertEmploymentTypeRequest,
  RecruitmentDropdownOptionResponse,
  UpsertRecruitmentDropdownOptionRequest,
  RoleGroupOptionResponse,
  UpsertNewsRequest,
  UpsertProjectRequest,
  UpsertLogoRequest,
  UpsertProcessRequest,
  ProcessAssetUploadResponse,
  UpsertServiceAdminRequest,
  SlideshowAdminResponse,
  UpsertSlideshowRequest,
  AboutSectionAdminResponse,
  UpsertAboutSectionRequest,
  JobPositionResponse,
  UpsertJobPositionRequest,
  JobApplicationResponse,
  SubmitJobApplicationRequest,
  EmailTemplatesResponse,
  ProjectScheduleEmailReviewItem,
  QuoteApprovalTemplates,
  WorkPackageApprovalPreview,
  ContractReminderTemplates,
  ContractReminder,
  ContractReminderPreview,
  UpdateEmailTemplatesRequest,
  OtpSettingsResponse,
  UpdateOtpSettingsRequest,
  MapEmbedResponse,
  UpdateMapEmbedRequest,
  GoogleDriveAdminStatusResponse,
  GoogleDriveAdminConfigurationResponse,
  UpdateGoogleDriveConfigurationRequest,
  GoogleDriveOAuthStartResponse,
  GoogleDriveDisconnectResponse,
  ContactMessageResponse,
} from "./types/content";
import type {
  AsBuiltDocumentCategoryResponse,
  UpsertAsBuiltDocumentCategoryRequest,
} from "./types/asBuilt";

export const contentApi = {
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
  createActivity: (data: UpsertActivityRequest) => api.post("/activities", data),
  updateActivity: (id: number, data: UpsertActivityRequest) => api.put(`/activities/${id}`, data),
  deleteActivity: (id: number) => api.delete(`/activities/${id}`),
  getActivityCategories: (includeInactive = false) =>
    api.get<ActivityCategoryResponse[]>(`/activity-categories?includeInactive=${includeInactive}`),
  createActivityCategory: (data: UpsertActivityCategoryRequest) =>
    api.post<ActivityCategoryResponse>("/activity-categories", data),
  updateActivityCategory: (id: number, data: UpsertActivityCategoryRequest) =>
    api.put<ActivityCategoryResponse>(`/activity-categories/${id}`, data),
  deleteActivityCategory: (id: number) => api.delete(`/activity-categories/${id}`),
  getNewsCategories: (includeInactive = false) =>
    api.get<NewsCategoryResponse[]>(`/news-categories?includeInactive=${includeInactive}`),
  createNewsCategory: (data: UpsertNewsCategoryRequest) =>
    api.post<NewsCategoryResponse>("/news-categories", data),
  updateNewsCategory: (id: number, data: UpsertNewsCategoryRequest) =>
    api.put<NewsCategoryResponse>(`/news-categories/${id}`, data),
  deleteNewsCategory: (id: number) => api.delete(`/news-categories/${id}`),
  getProjectCategories: (includeInactive = false) =>
    api.get<ProjectCategoryResponse[]>(`/project-categories?includeInactive=${includeInactive}`),
  createProjectCategory: (data: UpsertProjectCategoryRequest) =>
    api.post<ProjectCategoryResponse>("/project-categories", data),
  updateProjectCategory: (id: number, data: UpsertProjectCategoryRequest) =>
    api.put<ProjectCategoryResponse>(`/project-categories/${id}`, data),
  deleteProjectCategory: (id: number) => api.delete(`/project-categories/${id}`),
  getAsBuiltDocumentCategories: (includeInactive = false) =>
    api.get<AsBuiltDocumentCategoryResponse[]>(
      `/asbuilt-categories?includeInactive=${includeInactive}`,
    ),
  createAsBuiltDocumentCategory: (data: UpsertAsBuiltDocumentCategoryRequest) =>
    api.post<AsBuiltDocumentCategoryResponse>("/asbuilt-categories", data),
  updateAsBuiltDocumentCategory: (id: number, data: UpsertAsBuiltDocumentCategoryRequest) =>
    api.put<AsBuiltDocumentCategoryResponse>(`/asbuilt-categories/${id}`, data),
  deleteAsBuiltDocumentCategory: (id: number) => api.delete(`/asbuilt-categories/${id}`),
  getEmploymentTypes: (includeInactive = false, lang = "vi") =>
    api.get<EmploymentTypeResponse[]>(
      `/employment-types?includeInactive=${includeInactive}&lang=${lang}`,
    ),
  createEmploymentType: (data: UpsertEmploymentTypeRequest) =>
    api.post<EmploymentTypeResponse>("/employment-types", data),
  updateEmploymentType: (id: number, data: UpsertEmploymentTypeRequest) =>
    api.put<EmploymentTypeResponse>(`/employment-types/${id}`, data),
  deleteEmploymentType: (id: number) => api.delete(`/employment-types/${id}`),
  getRecruitmentDropdownOptions: (type: string, includeInactive = false, lang = "vi") =>
    api.get<RecruitmentDropdownOptionResponse[]>(
      `/recruitment-dropdown-options?type=${encodeURIComponent(type)}&includeInactive=${includeInactive}&lang=${lang}`,
    ),
  createRecruitmentDropdownOption: (data: UpsertRecruitmentDropdownOptionRequest) =>
    api.post<RecruitmentDropdownOptionResponse>("/recruitment-dropdown-options", data),
  updateRecruitmentDropdownOption: (id: number, data: UpsertRecruitmentDropdownOptionRequest) =>
    api.put<RecruitmentDropdownOptionResponse>(`/recruitment-dropdown-options/${id}`, data),
  deleteRecruitmentDropdownOption: (id: number) =>
    api.delete(`/recruitment-dropdown-options/${id}`),
  getRoleGroupOptions: () =>
    api.get<RoleGroupOptionResponse[]>("/recruitment-dropdown-options/role-group-options"),
  createNews: (data: UpsertNewsRequest) => api.post("/news", data),
  updateNews: (id: number, data: UpsertNewsRequest) => api.put(`/news/${id}`, data),
  deleteNews: (id: number) => api.delete(`/news/${id}`),
  createProject: (data: UpsertProjectRequest) => api.post("/projects", data),
  updateProject: (id: number, data: UpsertProjectRequest) => api.put(`/projects/${id}`, data),
  deleteProject: (id: number) => api.delete(`/projects/${id}`),
  createLogo: (data: UpsertLogoRequest) => api.post("/logos", data),
  updateLogo: (id: number, data: UpsertLogoRequest) => api.put(`/logos/${id}`, data),
  deleteLogo: (id: number) => api.delete(`/logos/${id}`),
  createProcess: (data: UpsertProcessRequest) => api.post("/processes", data),
  updateProcess: (id: number, data: UpsertProcessRequest) => api.put(`/processes/${id}`, data),
  deleteProcess: (id: number) => api.delete(`/processes/${id}`),
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
  createService: (data: UpsertServiceAdminRequest) => api.post<ServiceResponse>("/services", data),
  updateService: (id: number, data: UpsertServiceAdminRequest) =>
    api.put<ServiceResponse>(`/services/${id}`, data),
  deleteService: (id: number) => api.delete(`/services/${id}`),
  getSlideshow: (lang = "vi", activeOnly = false) =>
    api.get<SlideshowAdminResponse[]>(`/slideshow?lang=${lang}&activeOnly=${activeOnly}`),
  createSlideshow: (data: UpsertSlideshowRequest) => api.post("/slideshow", data),
  updateSlideshow: (id: number, data: UpsertSlideshowRequest) => api.put(`/slideshow/${id}`, data),
  deleteSlideshow: (id: number) => api.delete(`/slideshow/${id}`),
  getAboutSections: (activeOnly = false) =>
    api.get<AboutSectionAdminResponse[]>(`/about-sections?activeOnly=${activeOnly}`),
  createAboutSection: (data: UpsertAboutSectionRequest) =>
    api.post<AboutSectionAdminResponse>("/about-sections", data),
  updateAboutSection: (id: number, data: UpsertAboutSectionRequest) =>
    api.put<AboutSectionAdminResponse>(`/about-sections/${id}`, data),
  deleteAboutSection: (id: number) => api.delete(`/about-sections/${id}`),
  getJobPositions: (includeInactive = false) =>
    api.get<JobPositionResponse[]>(`/job-positions?includeInactive=${includeInactive}`),
  createJobPosition: (data: UpsertJobPositionRequest) =>
    api.post<JobPositionResponse>("/job-positions", data),
  updateJobPosition: (id: number, data: UpsertJobPositionRequest) =>
    api.put<JobPositionResponse>(`/job-positions/${id}`, data),
  deleteJobPosition: (id: number) => api.delete(`/job-positions/${id}`),
  getJobApplications: (positionId?: number, status?: string) => {
    const params = new URLSearchParams();
    if (positionId) params.append("positionId", String(positionId));
    if (status) params.append("status", status);
    return api.get<JobApplicationResponse[]>(`/job-applications?${params}`);
  },
  updateApplicationStatus: (id: number, status: string) =>
    api.patch<JobApplicationResponse>(`/job-applications/${id}/status`, { status }),
  deleteApplication: (id: number) => api.delete(`/job-applications/${id}`),
  submitJobApplication: (data: SubmitJobApplicationRequest) =>
    api.post<JobApplicationResponse>("/job-applications", data),
  getEmailTemplates: () => api.get<EmailTemplatesResponse>("/site-settings/email-templates"),
  getProjectScheduleEmailsNeedingReview: () =>
    api.get<ProjectScheduleEmailReviewItem[]>(
      "/site-settings/email-templates/project-schedule/needs-review",
    ),
  previewProjectScheduleEmail: (data: { subject: string; body: string }) =>
    api.post<{ subject: string; body: string }>(
      "/site-settings/email-templates/project-schedule/preview",
      data,
    ),
  getWorkPackageApprovalTemplate: () =>
    api.get<{ subject: string; body: string }>("/site-settings/work-package-approval-template"),
  updateWorkPackageApprovalTemplate: (data: { subject: string; body: string }) =>
    api.put<{ subject: string; body: string }>(
      "/site-settings/work-package-approval-template",
      data,
    ),
  getQuoteApprovalTemplate: () =>
    api.get<QuoteApprovalTemplates>("/site-settings/quote-approval-template"),
  updateQuoteApprovalTemplate: (data: {
    subject?: string;
    body?: string;
    decisionSubject?: string;
    decisionBody?: string;
  }) => api.put<QuoteApprovalTemplates>("/site-settings/quote-approval-template", data),
  previewWorkPackageApprovalEmail: (
    packageId: number,
    offerId?: number,
    recipients?: { toEmail: string; ccEmails: string[] },
  ) =>
    api.post<WorkPackageApprovalPreview>(
      offerId
        ? `/work-packages/${packageId}/input-price-offers/${offerId}/approval-email-preview`
        : `/work-packages/${packageId}/finance-review/approval-email-preview`,
      recipients,
    ),
  getContractReminderTemplates: () =>
    api.get<ContractReminderTemplates>("/site-settings/contract-reminder-templates"),
  updateContractReminderTemplates: (data: ContractReminderTemplates) =>
    api.put<ContractReminderTemplates>("/site-settings/contract-reminder-templates", data),
  listContractReminders: (id: number) => api.get<ContractReminder[]>(`/contracts/${id}/reminders`),
  previewContractReminder: (
    id: number,
    data: { kind: "Renewal" | "End"; reminderDate: string; contactId?: number },
  ) => api.post<ContractReminderPreview>(`/contracts/${id}/reminders/preview`, data),
  scheduleContractReminder: (id: number, previewToken: string) =>
    api.post<ContractReminder>(`/contracts/${id}/reminders`, { previewToken }),
  cancelContractReminder: (id: number, reminderId: number) =>
    api.post(`/contracts/${id}/reminders/${reminderId}/cancel`),
  updateEmailTemplates: (data: UpdateEmailTemplatesRequest) =>
    api.put<EmailTemplatesResponse>("/site-settings/email-templates", data),
  getOtpSettings: () => api.get<OtpSettingsResponse>("/site-settings/otp-settings"),
  updateOtpSettings: (data: UpdateOtpSettingsRequest) =>
    api.put<OtpSettingsResponse>("/site-settings/otp-settings", data),
  getContractUploadLimit: () =>
    api.get<{ maxFileSizeMb: number; maxAllowedFileSizeMb: number }>(
      "/site-settings/contract-uploads",
    ),
  updateContractUploadLimit: (maxFileSizeMb: number) =>
    api.put<{ maxFileSizeMb: number; maxAllowedFileSizeMb: number }>(
      "/site-settings/contract-uploads",
      { maxFileSizeMb },
    ),
  getMapEmbed: () => api.get<MapEmbedResponse>("/site-settings/map-embed"),
  updateMapEmbed: (data: UpdateMapEmbedRequest) =>
    api.put<MapEmbedResponse>("/site-settings/map-embed", data),
  getGoogleDriveAdminStatus: () =>
    api.get<GoogleDriveAdminStatusResponse>("/site-settings/google-drive/status"),
  getGoogleDriveConfiguration: () =>
    api.get<GoogleDriveAdminConfigurationResponse>("/site-settings/google-drive/configuration"),
  updateGoogleDriveConfiguration: (data: UpdateGoogleDriveConfigurationRequest) =>
    api.put<GoogleDriveAdminConfigurationResponse>(
      "/site-settings/google-drive/configuration",
      data,
    ),
  startGoogleDriveOAuth: () =>
    api.post<GoogleDriveOAuthStartResponse>("/site-settings/google-drive/oauth/start"),
  disconnectGoogleDriveOAuth: () =>
    api.post<GoogleDriveDisconnectResponse>("/site-settings/google-drive/oauth/disconnect"),
  getContacts: (replied?: boolean) => {
    const params = new URLSearchParams();
    if (replied !== undefined) params.append("replied", String(replied));
    return api.get<ContactMessageResponse[]>(`/contacts?${params}`);
  },
  replyContact: (id: number, replyContent: string) =>
    api.post<ContactMessageResponse>(`/contacts/${id}/reply`, { replyContent }),
  markContactReplied: (id: number) =>
    api.patch<ContactMessageResponse>(`/contacts/${id}/mark-replied`),
  deleteContact: (id: number) => api.delete(`/contacts/${id}`),
};
