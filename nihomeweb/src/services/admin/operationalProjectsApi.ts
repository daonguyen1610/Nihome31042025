import api from "@/lib/api";
import { withIdempotencyKey } from "@/lib/api";
import { withIfMatch } from "@/lib/api";
import type { ProjectReportFilters } from "@/types/projectReports";
import type { ProjectReportResponse } from "@/types/projectReports";
import type { ProjectReportExportParams } from "@/types/projectReports";
import type {
  DepartmentWorkPackageQuery,
  DepartmentWorkPackageListResponse,
  DeletionImpactResponse,
  ConfirmDeletionRequest,
  DepartmentWorkPackageResponse,
  DepartmentWorkPackageOptionsResponse,
  CreateDepartmentWorkPackageRequest,
  UpdateDepartmentWorkPackageRequest,
  UpsertDepartmentWorkTaskRequest,
  DepartmentWorkTaskResponse,
  DepartmentWorkTaskAttachmentResponse,
  WorkTaskStatus,
  UpsertInputPriceOfferRequest,
  InputPriceOfferResponse,
  UpsertFinanceReviewRequest,
  FinanceReviewResponse,
  OperationalProjectListParams,
  OperationalProjectListResponse,
  OperationalProjectCodeSuggestionResponse,
  OperationalProjectResponse,
  ProjectScheduleResponse,
  SaveProjectScheduleRevisionRequest,
  ConstructionPreparationResponse,
  UpdateConstructionPreparationRequest,
  ConstructionResourceRequestResponse,
  ConstructionProcurementResponse,
  ContractType,
  ContractResponse,
  ConstructionResourceReferenceDataResponse,
  CreateConstructionResourceRequest,
  UpdateConstructionResourceRequest,
  QaQcTemplateResponse,
  CreateQaQcTemplateRequest,
  QaQcDossierResponse,
  CreateQaQcDossierRequest,
  UpdateQaQcDossierRequest,
  ConstructionQsBatchResponse,
  ConstructionQsReferenceDataResponse,
  CreateConstructionQsBatchRequest,
  UpdateConstructionQsBatchRequest,
  OperationalProjectTimelineItem,
  HseViolationListParams,
  HseViolationListResponse,
  HseViolationResponse,
  HseViolationFieldsRequest,
  UpdateHseViolationRequest,
  TransitionHseViolationRequest,
  CorrectHseViolationRequest,
  CreateOperationalProjectRequest,
  UpdateOperationalProjectRequest,
  RenameOperationalProjectRequest,
  ReopenOperationalProjectRequest,
  HardDeleteOperationResult,
  KpiDefinitionResponse,
  KpiUserOptionResponse,
  KpiTargetDirection,
  KpiDashboardResponse,
  ProjectDocumentCategoryResponse,
  ProjectDocumentResponse,
} from "./types/operationalProjects";
import type {
  ProjectScheduleEmailPreviewResponse,
  ProjectScheduleEmailReviewItem,
} from "./types/content";
import type {
  OperationalProjectTeamResponse,
  OperationalProjectTeamHistoryResponse,
  ProjectMemberCandidateResponse,
  UpsertOperationalProjectMemberRequest,
  OperationalProjectMemberResponse,
  UpdateDesignProjectMemberPermissionsRequest,
  UpsertOperationalProjectAssignmentRequest,
  OperationalProjectAssignmentResponse,
  OperationalProjectAssignmentAttachmentResponse,
  OperationalProjectAssignmentCommentResponse,
} from "./types/designProjects";
import { postIdempotent } from "./helpers";

export const operationalProjectsApi = {
  listWorkPackages: (params: DepartmentWorkPackageQuery = {}) =>
    api.get<DepartmentWorkPackageListResponse>("/work-packages", { params }),
  getWorkPackageDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/work-packages/${id}/deletion-impact`),
  deleteWorkPackage: (id: number, body: ConfirmDeletionRequest, idempotencyKey: string) =>
    api.delete<void>(`/work-packages/${id}`, {
      data: body,
      headers: { "Idempotency-Key": idempotencyKey },
    }),
  getWorkPackage: (id: number) => api.get<DepartmentWorkPackageResponse>(`/work-packages/${id}`),
  getWorkPackageDepartments: () =>
    api.get<DepartmentWorkPackageOptionsResponse["departments"]>("/work-packages/departments"),
  getWorkPackageOptions: () =>
    api.get<DepartmentWorkPackageOptionsResponse>("/work-packages/options"),
  createWorkPackage: (body: CreateDepartmentWorkPackageRequest, idempotencyKey: string) =>
    api.post<DepartmentWorkPackageResponse>(
      "/work-packages",
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  updateWorkPackage: (
    id: number,
    body: UpdateDepartmentWorkPackageRequest,
    idempotencyKey: string,
  ) =>
    api.put<DepartmentWorkPackageResponse>(`/work-packages/${id}`, body, {
      headers: {
        ...withIdempotencyKey(idempotencyKey).headers,
        ...withIfMatch(body.rowVersion).headers,
      },
    }),
  addWorkTask: (packageId: number, body: UpsertDepartmentWorkTaskRequest, idempotencyKey: string) =>
    api.post<DepartmentWorkTaskResponse>(
      `/work-packages/${packageId}/tasks`,
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  listWorkTaskAttachments: (packageId: number, taskId: number) =>
    api.get<DepartmentWorkTaskAttachmentResponse[]>(
      `/work-packages/${packageId}/tasks/${taskId}/attachments`,
    ),
  uploadWorkTaskAttachment: (packageId: number, taskId: number, file: File, key: string) => {
    const formData = new FormData();
    formData.append("file", file);
    return api.post<DepartmentWorkTaskAttachmentResponse>(
      `/work-packages/${packageId}/tasks/${taskId}/attachments`,
      formData,
      withIdempotencyKey(key),
    );
  },
  downloadWorkTaskAttachment: (packageId: number, taskId: number, attachmentId: number) =>
    api.get<Blob>(
      `/work-packages/${packageId}/tasks/${taskId}/attachments/${attachmentId}/content`,
      { responseType: "blob" },
    ),
  deleteWorkTaskAttachment: (packageId: number, taskId: number, attachmentId: number) =>
    api.delete(`/work-packages/${packageId}/tasks/${taskId}/attachments/${attachmentId}`),
  updateWorkTask: (
    packageId: number,
    taskId: number,
    body: UpsertDepartmentWorkTaskRequest,
    idempotencyKey: string,
  ) =>
    api.put<DepartmentWorkTaskResponse>(`/work-packages/${packageId}/tasks/${taskId}`, body, {
      headers: {
        ...withIdempotencyKey(idempotencyKey).headers,
        ...withIfMatch(body.rowVersion ?? "").headers,
      },
    }),
  updateWorkTaskProgress: (
    packageId: number,
    taskId: number,
    body: { progressPercent: number; status: WorkTaskStatus; rowVersion: string },
    idempotencyKey: string,
  ) =>
    api.put<DepartmentWorkTaskResponse>(
      `/work-packages/${packageId}/tasks/${taskId}/progress`,
      body,
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  verifyWorkTask: (packageId: number, taskId: number, rowVersion: string, idempotencyKey: string) =>
    api.post<DepartmentWorkTaskResponse>(
      `/work-packages/${packageId}/tasks/${taskId}/verify`,
      { confirm: true, rowVersion },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  rejectWorkTask: (
    packageId: number,
    taskId: number,
    rowVersion: string,
    reason: string,
    idempotencyKey: string,
  ) =>
    api.post<DepartmentWorkTaskResponse>(
      `/work-packages/${packageId}/tasks/${taskId}/verify`,
      { confirm: false, reason, rowVersion },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  addInputPriceOffer: (
    packageId: number,
    body: UpsertInputPriceOfferRequest,
    idempotencyKey: string,
  ) =>
    api.post<InputPriceOfferResponse>(
      `/work-packages/${packageId}/input-price-offers`,
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  updateInputPriceOffer: (
    packageId: number,
    offerId: number,
    body: UpsertInputPriceOfferRequest,
    idempotencyKey: string,
  ) =>
    api.put<InputPriceOfferResponse>(
      `/work-packages/${packageId}/input-price-offers/${offerId}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(body.rowVersion ?? "").headers,
        },
      },
    ),
  submitInputPriceOffer: (
    packageId: number,
    offerId: number,
    rowVersion: string,
    idempotencyKey: string,
    previewToken: string,
  ) =>
    api.post<InputPriceOfferResponse>(
      `/work-packages/${packageId}/input-price-offers/${offerId}/submit`,
      { rowVersion, previewToken },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  decideInputPriceOffer: (
    packageId: number,
    offerId: number,
    approved: boolean,
    reason: string | null,
    rowVersion: string,
    idempotencyKey: string,
  ) =>
    api.post<InputPriceOfferResponse>(
      `/work-packages/${packageId}/input-price-offers/${offerId}/decision`,
      { approved, reason, rowVersion },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  saveFinanceReview: (
    packageId: number,
    body: UpsertFinanceReviewRequest,
    idempotencyKey: string,
  ) =>
    api.put<FinanceReviewResponse>(`/work-packages/${packageId}/finance-review`, body, {
      headers: {
        ...withIdempotencyKey(idempotencyKey).headers,
        ...(body.rowVersion ? withIfMatch(body.rowVersion).headers : {}),
      },
    }),
  submitFinanceReview: (
    packageId: number,
    rowVersion: string,
    idempotencyKey: string,
    previewToken: string,
  ) =>
    api.post<FinanceReviewResponse>(
      `/work-packages/${packageId}/finance-review/submit`,
      { rowVersion, previewToken },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  decideFinanceReview: (
    packageId: number,
    approved: boolean,
    reason: string | null,
    rowVersion: string,
    idempotencyKey: string,
  ) =>
    api.post<FinanceReviewResponse>(
      `/work-packages/${packageId}/finance-review/decision`,
      { approved, reason, rowVersion },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  listOperationalProjects: (params: OperationalProjectListParams = {}) =>
    api.get<OperationalProjectListResponse>("/operational-projects", { params }),
  suggestOperationalProjectCode: () =>
    api.get<OperationalProjectCodeSuggestionResponse>("/operational-projects/code-suggestion"),
  getProjectReports: (params: ProjectReportFilters = {}) =>
    api.get<ProjectReportResponse>("/reports/projects", { params }),
  exportProjectReports: (params: ProjectReportExportParams) =>
    api.get<Blob>("/reports/projects/export", { params, responseType: "blob" }),
  getOperationalProject: (id: number) =>
    api.get<OperationalProjectResponse>(`/operational-projects/${id}`),
  getProjectSchedule: (projectId: number) =>
    api.get<ProjectScheduleResponse>(`/operational-projects/${projectId}/master-schedule`),
  previewProjectScheduleForAction: (
    projectId: number,
    revisionId: number,
    action: "Submit" | "Publish",
    recipientUserId?: number,
  ) =>
    api.get<ProjectScheduleEmailPreviewResponse>(
      `/operational-projects/${projectId}/master-schedule/revisions/${revisionId}/email-preview`,
      { params: { action, recipientUserId } },
    ),
  getProjectScheduleEmailsForReview: (projectId: number) =>
    api.get<ProjectScheduleEmailReviewItem[]>(
      `/operational-projects/${projectId}/master-schedule/emails/needs-review`,
    ),
  resolveProjectScheduleEmail: (
    projectId: number,
    emailId: number,
    decision: "ConfirmReceived" | "Retry",
  ) =>
    api.post<void>(`/operational-projects/${projectId}/master-schedule/emails/${emailId}/resolve`, {
      decision,
    }),
  createProjectScheduleRevision: (projectId: number, body: SaveProjectScheduleRevisionRequest) =>
    api.post<ProjectScheduleResponse>(
      `/operational-projects/${projectId}/master-schedule/revisions`,
      body,
      withIdempotencyKey(crypto.randomUUID()),
    ),
  updateProjectScheduleRevision: (
    projectId: number,
    revisionId: number,
    body: SaveProjectScheduleRevisionRequest,
  ) =>
    api.put<ProjectScheduleResponse>(
      `/operational-projects/${projectId}/master-schedule/revisions/${revisionId}`,
      body,
      withIdempotencyKey(crypto.randomUUID()),
    ),
  submitProjectScheduleRevision: (
    projectId: number,
    revisionId: number,
    approverUserId: number,
    rowVersion: string,
    previewToken: string,
  ) =>
    api.post<ProjectScheduleResponse>(
      `/operational-projects/${projectId}/master-schedule/revisions/${revisionId}/submit`,
      { approverUserId, rowVersion, previewToken },
      withIdempotencyKey(crypto.randomUUID()),
    ),
  reviewProjectScheduleRevision: (
    projectId: number,
    revisionId: number,
    decision: "Approve" | "Reject",
    comment: string,
    rowVersion: string,
    previewToken?: string,
  ) =>
    api.post<ProjectScheduleResponse>(
      `/operational-projects/${projectId}/master-schedule/revisions/${revisionId}/review`,
      { decision, comment, rowVersion, previewToken },
      withIdempotencyKey(crypto.randomUUID()),
    ),
  getConstructionPreparation: (projectId: number) =>
    api.get<ConstructionPreparationResponse>(
      `/operational-projects/${projectId}/construction/preparation`,
    ),
  updateConstructionPreparation: (projectId: number, body: UpdateConstructionPreparationRequest) =>
    api.put<ConstructionPreparationResponse>(
      `/operational-projects/${projectId}/construction/preparation`,
      body,
      {
        headers: {
          ...withIdempotencyKey(crypto.randomUUID()).headers,
          ...(body.rowVersion ? withIfMatch(body.rowVersion).headers : {}),
        },
      },
    ),
  evaluateConstructionReadiness: (projectId: number, rowVersion: string) =>
    api.post<ConstructionPreparationResponse>(
      `/operational-projects/${projectId}/construction/readiness/evaluate`,
      { rowVersion },
      {
        headers: {
          ...withIdempotencyKey(crypto.randomUUID()).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  startConstruction: (projectId: number, rowVersion: string) =>
    api.post<ConstructionPreparationResponse>(
      `/operational-projects/${projectId}/construction/readiness/start`,
      { rowVersion },
      {
        headers: {
          ...withIdempotencyKey(crypto.randomUUID()).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  listConstructionResourceRequests: (projectId: number) =>
    api.get<ConstructionResourceRequestResponse[]>(
      `/operational-projects/${projectId}/construction/resource-requests`,
    ),
  listConstructionProcurement: (projectId: number, params?: { type?: string; state?: string }) =>
    api.get<ConstructionProcurementResponse[]>(
      `/operational-projects/${projectId}/procurement/construction-resources`,
      { params },
    ),
  listConstructionProcurementContracts: (projectId: number) =>
    api.get<{ id: number; contractNumber: string; status: string }[]>(
      `/operational-projects/${projectId}/procurement/construction-resources/contracts`,
    ),
  listConstructionSupplyDocuments: (projectId: number) =>
    api.get<{ id: number; originalFileName: string }[]>(
      `/operational-projects/${projectId}/procurement/construction-resources/documents`,
    ),
  createConstructionProcurementContract: (
    projectId: number,
    body: { vendorId: number; type: ContractType; value: number; scopeOfWork: string | null },
    idempotencyKey: string,
  ) =>
    api.post<ContractResponse>(
      `/operational-projects/${projectId}/procurement/construction-resources/contracts`,
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  linkConstructionProcurementContract: (
    projectId: number,
    id: number,
    contractId: number,
    rowVersion: string,
  ) =>
    postIdempotent<ConstructionProcurementResponse>(
      `/operational-projects/${projectId}/procurement/construction-resources/${id}/contract`,
      { contractId, rowVersion },
    ),
  confirmConstructionSupply: (
    projectId: number,
    id: number,
    documentId: number,
    rowVersion: string,
  ) =>
    postIdempotent<ConstructionProcurementResponse>(
      `/operational-projects/${projectId}/procurement/construction-resources/${id}/supply`,
      { documentId, rowVersion },
    ),
  getConstructionResourceReferenceData: (projectId: number) =>
    api.get<ConstructionResourceReferenceDataResponse>(
      `/operational-projects/${projectId}/construction/resource-references`,
    ),
  createConstructionResourceRequest: (projectId: number, body: CreateConstructionResourceRequest) =>
    postIdempotent<ConstructionResourceRequestResponse>(
      `/operational-projects/${projectId}/construction/resource-requests`,
      body,
    ),
  updateConstructionResourceRequest: (
    projectId: number,
    id: number,
    body: UpdateConstructionResourceRequest,
  ) =>
    api.put<ConstructionResourceRequestResponse>(
      `/operational-projects/${projectId}/construction/resource-requests/${id}`,
      body,
      withIfMatch(body.rowVersion),
    ),
  submitConstructionResourceRequest: (projectId: number, id: number, rowVersion: string) =>
    postIdempotent<ConstructionResourceRequestResponse>(
      `/operational-projects/${projectId}/construction/resource-requests/${id}/submit`,
      { rowVersion },
    ),
  decideConstructionResourceRequest: (
    projectId: number,
    id: number,
    approved: boolean,
    reason: string,
    rowVersion: string,
  ) =>
    postIdempotent<ConstructionResourceRequestResponse>(
      `/operational-projects/${projectId}/construction/resource-requests/${id}/cm-decision`,
      { approved, reason, rowVersion },
    ),
  listQaQcTemplates: (projectId: number) =>
    api.get<QaQcTemplateResponse[]>(
      `/operational-projects/${projectId}/construction/qa-qc/templates`,
    ),
  createQaQcTemplate: (projectId: number, body: CreateQaQcTemplateRequest) =>
    postIdempotent<QaQcTemplateResponse>(
      `/operational-projects/${projectId}/construction/qa-qc/templates`,
      body,
    ),
  publishQaQcTemplate: (projectId: number, id: number, rowVersion: string) =>
    postIdempotent<QaQcTemplateResponse>(
      `/operational-projects/${projectId}/construction/qa-qc/templates/${id}/publish`,
      { rowVersion },
    ),
  listQaQcDossiers: (projectId: number) =>
    api.get<QaQcDossierResponse[]>(
      `/operational-projects/${projectId}/construction/qa-qc/dossiers`,
    ),
  createQaQcDossier: (projectId: number, body: CreateQaQcDossierRequest) =>
    postIdempotent<QaQcDossierResponse>(
      `/operational-projects/${projectId}/construction/qa-qc/dossiers`,
      body,
    ),
  updateQaQcDossier: (projectId: number, id: number, body: UpdateQaQcDossierRequest) =>
    api.put<QaQcDossierResponse>(
      `/operational-projects/${projectId}/construction/qa-qc/dossiers/${id}`,
      body,
      withIfMatch(body.rowVersion),
    ),
  submitQaQcDossier: (projectId: number, id: number, rowVersion: string) =>
    postIdempotent<QaQcDossierResponse>(
      `/operational-projects/${projectId}/construction/qa-qc/dossiers/${id}/submit`,
      { rowVersion },
    ),
  decideQaQcDossier: (
    projectId: number,
    id: number,
    approved: boolean,
    reason: string,
    rowVersion: string,
  ) =>
    postIdempotent<QaQcDossierResponse>(
      `/operational-projects/${projectId}/construction/qa-qc/dossiers/${id}/cm-decision`,
      { approved, reason, rowVersion },
    ),
  listConstructionQsBatches: (projectId: number) =>
    api.get<ConstructionQsBatchResponse[]>(
      `/operational-projects/${projectId}/construction/qs-batches`,
    ),
  getConstructionQsReferenceData: (projectId: number) =>
    api.get<ConstructionQsReferenceDataResponse>(
      `/operational-projects/${projectId}/construction/qs-references`,
    ),
  createConstructionQsBatch: (projectId: number, body: CreateConstructionQsBatchRequest) =>
    postIdempotent<ConstructionQsBatchResponse>(
      `/operational-projects/${projectId}/construction/qs-batches`,
      body,
    ),
  updateConstructionQsBatch: (
    projectId: number,
    id: number,
    body: UpdateConstructionQsBatchRequest,
  ) =>
    api.put<ConstructionQsBatchResponse>(
      `/operational-projects/${projectId}/construction/qs-batches/${id}`,
      body,
      withIfMatch(body.rowVersion),
    ),
  submitConstructionQsBatch: (projectId: number, id: number, rowVersion: string) =>
    postIdempotent<ConstructionQsBatchResponse>(
      `/operational-projects/${projectId}/construction/qs-batches/${id}/submit`,
      { rowVersion },
    ),
  decideConstructionQsBatch: (
    projectId: number,
    id: number,
    approved: boolean,
    reason: string,
    rowVersion: string,
  ) =>
    postIdempotent<ConstructionQsBatchResponse>(
      `/operational-projects/${projectId}/construction/qs-batches/${id}/cm-decision`,
      { approved, reason, rowVersion },
    ),
  getOperationalProjectTimeline: (id: number) =>
    api.get<OperationalProjectTimelineItem[]>(`/operational-projects/${id}/timeline`),
  getOperationalProjectTeam: (projectId: number, designProjectId?: number) =>
    api.get<OperationalProjectTeamResponse>(`/operational-projects/${projectId}/team`, {
      params: { designProjectId },
    }),
  getOperationalProjectTeamHistory: (projectId: number, designProjectId?: number) =>
    api.get<OperationalProjectTeamHistoryResponse[]>(
      `/operational-projects/${projectId}/team/history`,
      { params: { designProjectId } },
    ),
  getOperationalProjectTeamCandidates: (projectId: number, designProjectId?: number) =>
    api.get<ProjectMemberCandidateResponse[]>(
      `/operational-projects/${projectId}/team/candidates`,
      { params: { designProjectId } },
    ),
  listHseViolations: (projectId: number, params: HseViolationListParams = {}) =>
    api.get<HseViolationListResponse>(`/operational-projects/${projectId}/hse-violations`, {
      params,
    }),
  getHseViolation: (projectId: number, id: number) =>
    api.get<HseViolationResponse>(`/operational-projects/${projectId}/hse-violations/${id}`),
  createHseViolation: (
    projectId: number,
    body: HseViolationFieldsRequest,
    idempotencyKey: string,
  ) =>
    api.post<HseViolationResponse>(
      `/operational-projects/${projectId}/hse-violations`,
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  updateHseViolation: (
    projectId: number,
    id: number,
    body: UpdateHseViolationRequest,
    idempotencyKey: string,
  ) =>
    api.put<HseViolationResponse>(`/operational-projects/${projectId}/hse-violations/${id}`, body, {
      headers: {
        ...withIdempotencyKey(idempotencyKey).headers,
        ...withIfMatch(body.rowVersion).headers,
      },
    }),
  transitionHseViolation: (
    projectId: number,
    id: number,
    action: "report" | "confirm" | "reject" | "remediate" | "cancel" | "close",
    body: TransitionHseViolationRequest,
    idempotencyKey: string,
  ) =>
    api.post<HseViolationResponse>(
      `/operational-projects/${projectId}/hse-violations/${id}/${action}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  correctHseViolation: (
    projectId: number,
    id: number,
    body: CorrectHseViolationRequest,
    idempotencyKey: string,
  ) =>
    api.post<HseViolationResponse>(
      `/operational-projects/${projectId}/hse-violations/${id}/corrections`,
      body,
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  addOperationalProjectMember: (
    projectId: number,
    body: UpsertOperationalProjectMemberRequest,
    idempotencyKey: string,
  ) =>
    api.post<OperationalProjectMemberResponse>(
      `/operational-projects/${projectId}/team/members`,
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  updateOperationalProjectMemberDesignPermissions: (
    projectId: number,
    memberId: number,
    body: UpdateDesignProjectMemberPermissionsRequest,
  ) =>
    api.put<OperationalProjectMemberResponse>(
      `/operational-projects/${projectId}/team/members/${memberId}/design-permissions`,
      body,
    ),
  updateOperationalProjectMember: (
    projectId: number,
    memberId: number,
    body: UpsertOperationalProjectMemberRequest,
    idempotencyKey: string,
  ) =>
    api.put<OperationalProjectMemberResponse>(
      `/operational-projects/${projectId}/team/members/${memberId}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  addOperationalProjectAssignment: (
    projectId: number,
    body: UpsertOperationalProjectAssignmentRequest,
    idempotencyKey: string,
  ) =>
    api.post<OperationalProjectAssignmentResponse>(
      `/operational-projects/${projectId}/team/assignments`,
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  updateOperationalProjectAssignment: (
    projectId: number,
    assignmentId: number,
    body: UpsertOperationalProjectAssignmentRequest,
    idempotencyKey: string,
  ) =>
    api.put<OperationalProjectAssignmentResponse>(
      `/operational-projects/${projectId}/team/assignments/${assignmentId}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  listOperationalProjectAssignmentAttachments: (
    projectId: number,
    assignmentId: number,
    designProjectId?: number | null,
  ) =>
    api.get<OperationalProjectAssignmentAttachmentResponse[]>(
      `/operational-projects/${projectId}/team/assignments/${assignmentId}/attachments`,
      { params: designProjectId ? { designProjectId } : undefined },
    ),
  uploadOperationalProjectAssignmentAttachment: (
    projectId: number,
    assignmentId: number,
    file: File,
    key: string,
    designProjectId?: number | null,
  ) => {
    const formData = new FormData();
    formData.append("file", file);
    return api.post<OperationalProjectAssignmentAttachmentResponse>(
      `/operational-projects/${projectId}/team/assignments/${assignmentId}/attachments`,
      formData,
      {
        ...withIdempotencyKey(key),
        params: designProjectId ? { designProjectId } : undefined,
      },
    );
  },
  downloadOperationalProjectAssignmentAttachment: (
    projectId: number,
    assignmentId: number,
    attachmentId: number,
    designProjectId?: number | null,
  ) =>
    api.get<Blob>(
      `/operational-projects/${projectId}/team/assignments/${assignmentId}/attachments/${attachmentId}/content`,
      { responseType: "blob", params: designProjectId ? { designProjectId } : undefined },
    ),
  deleteOperationalProjectAssignmentAttachment: (
    projectId: number,
    assignmentId: number,
    attachmentId: number,
    designProjectId?: number | null,
  ) =>
    api.delete(
      `/operational-projects/${projectId}/team/assignments/${assignmentId}/attachments/${attachmentId}`,
      { params: designProjectId ? { designProjectId } : undefined },
    ),
  listOperationalProjectAssignmentComments: (
    projectId: number,
    assignmentId: number,
    designProjectId?: number | null,
  ) =>
    api.get<OperationalProjectAssignmentCommentResponse[]>(
      `/operational-projects/${projectId}/team/assignments/${assignmentId}/comments`,
      { params: designProjectId ? { designProjectId } : undefined },
    ),
  addOperationalProjectAssignmentComment: (
    projectId: number,
    assignmentId: number,
    body: string,
    designProjectId?: number | null,
  ) =>
    api.post<OperationalProjectAssignmentCommentResponse>(
      `/operational-projects/${projectId}/team/assignments/${assignmentId}/comments`,
      { body },
      { params: designProjectId ? { designProjectId } : undefined },
    ),
  deleteOperationalProjectAssignmentComment: (
    projectId: number,
    assignmentId: number,
    commentId: number,
    designProjectId?: number | null,
  ) =>
    api.delete(
      `/operational-projects/${projectId}/team/assignments/${assignmentId}/comments/${commentId}`,
      { params: designProjectId ? { designProjectId } : undefined },
    ),
  createOperationalProject: (body: CreateOperationalProjectRequest) =>
    api.post<OperationalProjectResponse>("/operational-projects", body),
  updateOperationalProject: (id: number, body: UpdateOperationalProjectRequest) =>
    api.put<OperationalProjectResponse>(`/operational-projects/${id}`, body),
  renameOperationalProject: (
    id: number,
    body: RenameOperationalProjectRequest,
    idempotencyKey: string,
  ) =>
    api.patch<OperationalProjectResponse>(`/operational-projects/${id}/code`, body, {
      headers: {
        ...withIfMatch(body.rowVersion).headers,
        ...withIdempotencyKey(idempotencyKey).headers,
      },
    }),
  retryOperationalProjectDriveRename: (id: number, idempotencyKey: string) =>
    api.post<OperationalProjectResponse>(
      `/operational-projects/${id}/code/drive-retry`,
      {},
      withIdempotencyKey(idempotencyKey),
    ),
  reopenOperationalProject: (
    id: number,
    body: ReopenOperationalProjectRequest,
    idempotencyKey: string,
  ) =>
    api.post<OperationalProjectResponse>(`/operational-projects/${id}/reopen`, body, {
      headers: {
        ...withIfMatch(body.rowVersion).headers,
        ...withIdempotencyKey(idempotencyKey).headers,
      },
    }),
  getOperationalProjectDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/operational-projects/${id}/deletion-impact`),
  deleteOperationalProject: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult | null>(`/operational-projects/${id}`, {
      ...withIfMatch(body.rowVersion),
      data: body,
    }),
  listKpiDefinitions: () => api.get<KpiDefinitionResponse[]>("/kpi/definitions"),
  listKpiEligibleUsers: () => api.get<KpiUserOptionResponse[]>("/kpi/eligible-users"),
  updateKpiDefinition: (
    id: number,
    body: {
      weight: number;
      targetValue?: number | null;
      minimumAcceptableScore?: number | null;
      targetDirection: KpiTargetDirection;
      isActive: boolean;
      rowVersion: string;
    },
  ) =>
    api.put<KpiDefinitionResponse>(`/kpi/definitions/${id}`, body, {
      headers: withIfMatch(body.rowVersion).headers,
    }),
  calculateKpi: (body: { year: number; month: number; userId?: number }, idempotencyKey: string) =>
    api.post<KpiDashboardResponse>("/kpi/calculate", body, withIdempotencyKey(idempotencyKey)),
  getKpiDashboard: (params: { year: number; month: number; userId?: number }) =>
    api.get<KpiDashboardResponse>("/kpi/dashboard", { params }),
  lockKpiPeriod: (
    year: number,
    month: number,
    body: { userId?: number; note: string; rowVersion: string },
  ) =>
    api.post<KpiDashboardResponse>(`/kpi/periods/${year}/${month}/lock`, body, {
      headers: withIfMatch(body.rowVersion).headers,
    }),
  exportKpi: (params: { year: number; month: number; userId?: number }) =>
    api.get<Blob>("/kpi/export", { params, responseType: "blob" }),
  listProjectDocumentCategories: () =>
    api.get<ProjectDocumentCategoryResponse[]>("/operational-projects/document-categories"),
  listProjectDocuments: (projectId: number, designProjectId?: number) =>
    api.get<ProjectDocumentResponse[]>(`/operational-projects/${projectId}/documents`, {
      params: designProjectId ? { designProjectId } : undefined,
    }),
  uploadProjectDocument: (
    projectId: number,
    file: File,
    category: string,
    idempotencyKey: string,
    source?: { module: string; entityType: string; recordId: number },
  ) => {
    const formData = new FormData();
    formData.append("file", file);
    formData.append("category", category);
    if (source) {
      formData.append("sourceModule", source.module);
      formData.append("sourceEntityType", source.entityType);
      formData.append("sourceRecordId", String(source.recordId));
    }
    return api.post<ProjectDocumentResponse>(
      `/operational-projects/${projectId}/documents`,
      formData,
      withIdempotencyKey(idempotencyKey),
    );
  },
  downloadProjectDocument: (projectId: number, documentId: number) =>
    api.get<Blob>(`/operational-projects/${projectId}/documents/${documentId}/content`, {
      responseType: "blob",
    }),
  deleteProjectDocument: (projectId: number, documentId: number) =>
    api.delete(`/operational-projects/${projectId}/documents/${documentId}`),
  retryProjectDocument: (projectId: number, documentId: number) =>
    api.post<ProjectDocumentResponse>(
      `/operational-projects/${projectId}/documents/${documentId}/retry`,
    ),
  classifyProjectDocument: (projectId: number, documentId: number, category: string) =>
    api.post<ProjectDocumentResponse>(
      `/operational-projects/${projectId}/documents/${documentId}/classify`,
      { category },
    ),
  resolveProjectDocumentKeepBoth: (projectId: number, documentId: number) =>
    api.post<ProjectDocumentResponse>(
      `/operational-projects/${projectId}/documents/${documentId}/resolve-conflict`,
      {
        confirmKeepBoth: true,
      },
    ),
};
