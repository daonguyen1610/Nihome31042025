import api from "@/lib/api";
import { withIdempotencyKey } from "@/lib/api";
import { withIfMatch } from "@/lib/api";
import { newIdempotencyKey } from "@/lib/api";
import type {
  DesignProjectListParams,
  DesignProjectListResponse,
  DesignProjectResponse,
  CreateDesignProjectRequest,
  UpdateDesignProjectRequest,
  DesignScheduleQuery,
  DesignScheduleResponse,
  DesignScheduleOutsourceOptionResponse,
  InitializeDesignScheduleRequest,
  UpsertDesignSchedulePhaseRequest,
  DesignSchedulePhaseResponse,
  UpsertDesignScheduleTaskRequest,
  DesignScheduleTaskResponse,
  DesignReviewResponse,
  DesignReviewStage,
} from "./types/designProjects";
import type {
  DeletionImpactResponse,
  ConfirmDeletionRequest,
  HardDeleteOperationResult,
} from "./types/operationalProjects";
import type {
  PermitChecklistListParams,
  PermitChecklistListResponse,
  PermitChecklistItemResponse,
  PermitChecklistHistoryResponse,
  CreatePermitChecklistItemRequest,
  UpdatePermitChecklistItemRequest,
} from "./types/permits";
import type { PermitDocumentKind } from "./types/vendors";
import type {
  ConceptOptionListParams,
  ConceptOptionListResponse,
  ConceptOptionResponse,
  CreateConceptOptionRequest,
  UpdateConceptOptionRequest,
  TransitionConceptOptionStatusRequest,
  ConceptRevisionResponse,
  ConceptArtifactKind,
} from "./types/conceptOptions";
import type {
  BasicDesignDocListParams,
  BasicDesignDocListResponse,
  BasicDesignDocResponse,
  CreateBasicDesignDocRequest,
  UpdateBasicDesignDocRequest,
  TransitionBasicDesignDocStatusRequest,
} from "./types/basicDesign";
import type {
  ShopDrawingListParams,
  ShopDrawingListResponse,
  ShopDrawingResponse,
  CreateShopDrawingRequest,
  UpdateShopDrawingRequest,
  BulkDeleteShopDrawingsRequest,
  ShopDrawingBulkDeleteResponse,
  TransitionShopDrawingStatusRequest,
} from "./types/shopDrawings";
import type {
  DrawingRevisionListParams,
  DrawingRevisionListResponse,
  DrawingRevisionResponse,
  CreateDrawingRevisionRequest,
  DrawingRevisionDiffResponse,
} from "./types/drawingRevisions";
import type {
  IfcReleaseListParams,
  IfcReleaseListResponse,
  IfcReleaseResponse,
  CreateIfcReleaseRequest,
  UpdateIfcReleaseRequest,
  AddIfcReleaseItemsRequest,
  AddIfcReleaseRecipientRequest,
  AcknowledgeIfcReleaseRecipientRequest,
} from "./types/ifcReleases";

export const designApi = {
  listDesignProjects: (params: DesignProjectListParams = {}) => {
    const q = new URLSearchParams();
    if (params.customerId != null) q.append("customerId", String(params.customerId));
    if (params.contractId != null) q.append("contractId", String(params.contractId));
    if (params.projectManagerUserId != null)
      q.append("projectManagerUserId", String(params.projectManagerUserId));
    if (params.designLeadUserId != null)
      q.append("designLeadUserId", String(params.designLeadUserId));
    if (params.stage) q.append("stage", params.stage);
    if (params.status) q.append("status", params.status);
    if (params.deadlineFrom) q.append("deadlineFrom", params.deadlineFrom);
    if (params.deadlineTo) q.append("deadlineTo", params.deadlineTo);
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<DesignProjectListResponse>(`/design-projects${qs ? `?${qs}` : ""}`);
  },
  getDesignProject: (id: number) => api.get<DesignProjectResponse>(`/design-projects/${id}`),
  createDesignProject: (body: CreateDesignProjectRequest) =>
    api.post<DesignProjectResponse>("/design-projects", body),
  updateDesignProject: (id: number, body: UpdateDesignProjectRequest) =>
    api.put<DesignProjectResponse>(`/design-projects/${id}`, body),
  getDesignProjectDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/design-projects/${id}/deletion-impact`),
  deleteDesignProject: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult | null>(`/design-projects/${id}`, { data: body }),
  getDesignSchedule: (projectId: number, params: DesignScheduleQuery = {}) =>
    api.get<DesignScheduleResponse>(`/operational-projects/${projectId}/design-schedule`, {
      params,
    }),
  getDesignScheduleOutsourceOptions: (projectId: number) =>
    api.get<DesignScheduleOutsourceOptionResponse[]>(
      `/operational-projects/${projectId}/design-schedule/outsource-options`,
    ),
  initializeDesignSchedule: (
    projectId: number,
    body: InitializeDesignScheduleRequest,
    idempotencyKey: string,
  ) =>
    api.post<DesignScheduleResponse>(
      `/operational-projects/${projectId}/design-schedule/initialize`,
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  updateDesignSchedulePhase: (
    projectId: number,
    phaseId: number,
    body: UpsertDesignSchedulePhaseRequest,
    idempotencyKey: string,
  ) =>
    api.put<DesignSchedulePhaseResponse>(
      `/operational-projects/${projectId}/design-schedule/phases/${phaseId}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  createDesignScheduleTask: (
    projectId: number,
    phaseId: number,
    body: UpsertDesignScheduleTaskRequest,
    idempotencyKey: string,
  ) =>
    api.post<DesignScheduleTaskResponse>(
      `/operational-projects/${projectId}/design-schedule/phases/${phaseId}/tasks`,
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  updateDesignScheduleTask: (
    projectId: number,
    taskId: number,
    body: UpsertDesignScheduleTaskRequest,
    idempotencyKey: string,
  ) =>
    api.put<DesignScheduleTaskResponse>(
      `/operational-projects/${projectId}/design-schedule/tasks/${taskId}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  completeDesignScheduleTask: (
    projectId: number,
    taskId: number,
    rowVersion: string,
    idempotencyKey: string,
  ) =>
    api.post<DesignScheduleTaskResponse>(
      `/operational-projects/${projectId}/design-schedule/tasks/${taskId}/complete`,
      { rowVersion },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  reviewDesignScheduleTask: (
    projectId: number,
    taskId: number,
    approved: boolean,
    comment: string | null,
    rowVersion: string,
    idempotencyKey: string,
  ) =>
    api.post<DesignScheduleTaskResponse>(
      `/operational-projects/${projectId}/design-schedule/tasks/${taskId}/review`,
      { decision: approved ? "Approve" : "Reject", comment, rowVersion },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  listDesignReviews: (projectId: number) =>
    api.get<DesignReviewResponse[]>(`/design-projects/${projectId}/reviews`),
  listDesignReviewRecipientCandidates: (projectId: number) =>
    api.get<
      Array<{
        userId: number;
        name: string;
        roleCode: string;
        roleName: string;
        roleLabelKey?: string | null;
        departmentId?: number | null;
        departmentLabelKey?: string | null;
      }>
    >(`/design-projects/${projectId}/reviews/recipient-candidates`),
  downloadDesignReviewDocument: (projectId: number, reviewId: number, documentId: number) =>
    api.get<Blob>(
      `/design-projects/${projectId}/reviews/${reviewId}/documents/${documentId}/content`,
      { responseType: "blob" },
    ),
  createDesignReview: (
    projectId: number,
    body: {
      stage: DesignReviewStage;
      title: string;
      recipientUserIds: number[];
      recipientDepartmentRoleGroupIds: number[];
      projectDocumentIds: number[];
      conceptRevisionId?: number | null;
    },
    key: string,
  ) =>
    api.post<DesignReviewResponse>(
      `/design-projects/${projectId}/reviews`,
      body,
      withIdempotencyKey(key),
    ),
  submitDesignReviewInternal: (
    projectId: number,
    reviewId: number,
    rowVersion: string,
    key: string,
  ) =>
    api.post<DesignReviewResponse>(
      `/design-projects/${projectId}/reviews/${reviewId}/submit-internal`,
      { rowVersion },
      { headers: { ...withIdempotencyKey(key).headers, ...withIfMatch(rowVersion).headers } },
    ),
  commentDesignReview: (
    projectId: number,
    reviewId: number,
    body: string,
    projectDocumentId: number | null,
    key: string,
  ) =>
    api.post<DesignReviewResponse>(
      `/design-projects/${projectId}/reviews/${reviewId}/comments`,
      { body, projectDocumentId },
      withIdempotencyKey(key),
    ),
  consolidateDesignReview: (
    projectId: number,
    reviewId: number,
    internalSummary: string,
    rowVersion: string,
    key: string,
  ) =>
    api.post<DesignReviewResponse>(
      `/design-projects/${projectId}/reviews/${reviewId}/consolidate`,
      { internalSummary, rowVersion },
      { headers: { ...withIdempotencyKey(key).headers, ...withIfMatch(rowVersion).headers } },
    ),
  submitDesignReviewClient: (
    projectId: number,
    reviewId: number,
    rowVersion: string,
    key: string,
  ) =>
    api.post<DesignReviewResponse>(
      `/design-projects/${projectId}/reviews/${reviewId}/submit-client`,
      { rowVersion },
      { headers: { ...withIdempotencyKey(key).headers, ...withIfMatch(rowVersion).headers } },
    ),
  recordDesignClientDecision: (
    projectId: number,
    reviewId: number,
    decision: "ClientApproved" | "ClientChangesRequested" | "Rejected",
    note: string | null,
    rowVersion: string,
    key: string,
    acceptance?: { documentId: number; signedBy: string; signedAt: string },
  ) =>
    api.post<DesignReviewResponse>(
      `/design-projects/${projectId}/reviews/${reviewId}/client-decision`,
      {
        decision,
        note,
        rowVersion,
        clientAcceptanceDocumentId: acceptance?.documentId,
        clientAcceptanceSignedBy: acceptance?.signedBy,
        clientAcceptanceSignedAt: acceptance?.signedAt,
      },
      { headers: { ...withIdempotencyKey(key).headers, ...withIfMatch(rowVersion).headers } },
    ),
  getHardDeleteOperation: (operationId: string) =>
    api.get<HardDeleteOperationResult>(`/hard-delete-operations/${operationId}`),
  retryHardDeleteOperation: (operationId: string) =>
    api.post<HardDeleteOperationResult>(`/hard-delete-operations/${operationId}/retry`),
  listPermits: (params: PermitChecklistListParams = {}) => {
    const q = new URLSearchParams();
    if (params.operationalProjectId != null)
      q.append("operationalProjectId", String(params.operationalProjectId));
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.ownerUserId != null) q.append("ownerUserId", String(params.ownerUserId));
    if (params.approverUserId != null) q.append("approverUserId", String(params.approverUserId));
    if (params.status) q.append("status", params.status);
    if (params.permitTypeCode) q.append("permitTypeCode", params.permitTypeCode);
    if (params.overdue) q.append("overdue", "true");
    if (params.dueSoon) q.append("dueSoon", "true");
    if (params.expiringSoon) q.append("expiringSoon", "true");
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<PermitChecklistListResponse>(`/permits${qs ? `?${qs}` : ""}`);
  },
  getPermit: (id: number) => api.get<PermitChecklistItemResponse>(`/permits/${id}`),
  getPermitHistory: (id: number) =>
    api.get<PermitChecklistHistoryResponse[]>(`/permits/${id}/history`),
  createPermit: (body: CreatePermitChecklistItemRequest) =>
    api.post<PermitChecklistItemResponse>("/permits", body),
  updatePermit: (id: number, body: UpdatePermitChecklistItemRequest) =>
    api.patch<PermitChecklistItemResponse>(`/permits/${id}`, body),
  deletePermit: (id: number) => api.delete(`/permits/${id}`),
  uploadPermitDocument: (id: number, kind: PermitDocumentKind, file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return api.post<PermitChecklistItemResponse>(`/permits/${id}/documents/${kind}`, formData);
  },
  listConceptOptions: (params: ConceptOptionListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.status) q.append("status", params.status);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<ConceptOptionListResponse>(`/concept-options${qs ? `?${qs}` : ""}`);
  },
  getConceptOption: (id: number) => api.get<ConceptOptionResponse>(`/concept-options/${id}`),
  createConceptOption: (body: CreateConceptOptionRequest) =>
    api.post<ConceptOptionResponse>("/concept-options", body),
  updateConceptOption: (id: number, body: UpdateConceptOptionRequest) =>
    api.put<ConceptOptionResponse>(`/concept-options/${id}`, body),
  getConceptOptionDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/concept-options/${id}/deletion-impact`),
  deleteConceptOption: (id: number, body: ConfirmDeletionRequest) =>
    api.delete(`/concept-options/${id}`, { data: body }),
  transitionConceptOption: (id: number, body: TransitionConceptOptionStatusRequest) =>
    api.post<ConceptOptionResponse>(`/concept-options/${id}/status`, body),
  listConceptRevisions: (id: number) =>
    api.get<ConceptRevisionResponse[]>(`/concept-options/${id}/revisions`),
  createConceptRevision: (id: number) =>
    api.post<ConceptRevisionResponse>(
      `/concept-options/${id}/revisions`,
      {},
      withIdempotencyKey(newIdempotencyKey()),
    ),
  addConceptArtifact: (
    id: number,
    revisionId: number,
    body: { projectDocumentId: number; kind: ConceptArtifactKind; floorLabel?: string | null },
  ) =>
    api.post<ConceptRevisionResponse>(
      `/concept-options/${id}/revisions/${revisionId}/artifacts`,
      body,
      withIdempotencyKey(newIdempotencyKey()),
    ),
  removeConceptArtifact: (id: number, revisionId: number, artifactId: number) =>
    api.delete<ConceptRevisionResponse>(
      `/concept-options/${id}/revisions/${revisionId}/artifacts/${artifactId}`,
    ),
  waiveConceptChecklist: (
    id: number,
    revisionId: number,
    body: { kind: ConceptArtifactKind; reason: string },
  ) =>
    api.post<ConceptRevisionResponse>(
      `/concept-options/${id}/revisions/${revisionId}/waivers`,
      body,
      withIdempotencyKey(newIdempotencyKey()),
    ),
  removeConceptWaiver: (id: number, revisionId: number, kind: ConceptArtifactKind) =>
    api.delete<ConceptRevisionResponse>(
      `/concept-options/${id}/revisions/${revisionId}/waivers/${kind}`,
    ),
  addConceptFeedback: (
    id: number,
    revisionId: number,
    body: {
      customerName: string;
      channel: string;
      evidenceSource: string;
      occurredAt: string;
      comment: string;
      projectDocumentId?: number | null;
    },
  ) =>
    api.post<ConceptRevisionResponse>(
      `/concept-options/${id}/revisions/${revisionId}/feedback`,
      body,
      withIdempotencyKey(newIdempotencyKey()),
    ),
  respondConceptFeedback: (
    id: number,
    revisionId: number,
    feedbackId: number,
    response: string,
    outcome: string,
  ) =>
    api.post<ConceptRevisionResponse>(
      `/concept-options/${id}/revisions/${revisionId}/feedback/${feedbackId}/response`,
      { response, outcome },
      withIdempotencyKey(newIdempotencyKey()),
    ),
  listBasicDesignDocs: (params: BasicDesignDocListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.disciplineCode) q.append("disciplineCode", params.disciplineCode);
    if (params.status) q.append("status", params.status);
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<BasicDesignDocListResponse>(`/basic-design-docs${qs ? `?${qs}` : ""}`);
  },
  getBasicDesignDoc: (id: number) => api.get<BasicDesignDocResponse>(`/basic-design-docs/${id}`),
  getBasicDesignDocContent: (id: number) =>
    api.get<Blob>(`/basic-design-docs/${id}/content`, { responseType: "blob" }),
  createBasicDesignDoc: (body: CreateBasicDesignDocRequest) =>
    api.post<BasicDesignDocResponse>("/basic-design-docs", body),
  updateBasicDesignDoc: (id: number, body: UpdateBasicDesignDocRequest) =>
    api.put<BasicDesignDocResponse>(`/basic-design-docs/${id}`, body),
  getBasicDesignDocDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/basic-design-docs/${id}/deletion-impact`),
  deleteBasicDesignDoc: (id: number, body: ConfirmDeletionRequest) =>
    api.delete(`/basic-design-docs/${id}`, { data: body }),
  transitionBasicDesignDoc: (id: number, body: TransitionBasicDesignDocStatusRequest) =>
    api.post<BasicDesignDocResponse>(`/basic-design-docs/${id}/status`, body),
  uploadBasicDesignDocFile: (id: number, file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return api.post<BasicDesignDocResponse>(`/basic-design-docs/${id}/upload`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  unlockShopDrawing: (designProjectId: number) =>
    api.post<DesignProjectResponse>(
      `/basic-design-docs/design-project/${designProjectId}/unlock-shop-drawing`,
    ),
  listShopDrawings: (params: ShopDrawingListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.disciplineCode) q.append("disciplineCode", params.disciplineCode);
    if (params.constructionItem) q.append("constructionItem", params.constructionItem);
    if (params.status) q.append("status", params.status);
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<ShopDrawingListResponse>(`/shop-drawings${qs ? `?${qs}` : ""}`);
  },
  getShopDrawing: (id: number) => api.get<ShopDrawingResponse>(`/shop-drawings/${id}`),
  getShopDrawingContent: (id: number) =>
    api.get<Blob>(`/shop-drawings/${id}/content`, { responseType: "blob" }),
  createShopDrawing: (body: CreateShopDrawingRequest) =>
    api.post<ShopDrawingResponse>("/shop-drawings", body),
  updateShopDrawing: (id: number, body: UpdateShopDrawingRequest) =>
    api.put<ShopDrawingResponse>(`/shop-drawings/${id}`, body),
  getShopDrawingDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/shop-drawings/${id}/deletion-impact`),
  deleteShopDrawing: (id: number, body: ConfirmDeletionRequest) =>
    api.delete(`/shop-drawings/${id}`, { data: body }),
  bulkDeleteShopDrawings: (body: BulkDeleteShopDrawingsRequest) =>
    api.post<ShopDrawingBulkDeleteResponse>("/shop-drawings/bulk-delete", body),
  transitionShopDrawing: (id: number, body: TransitionShopDrawingStatusRequest) =>
    api.post<ShopDrawingResponse>(`/shop-drawings/${id}/status`, body),
  uploadShopDrawingFile: (id: number, file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return api.post<ShopDrawingResponse>(`/shop-drawings/${id}/upload`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  listDrawingRevisions: (params: DrawingRevisionListParams) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.targetType) q.append("targetType", params.targetType);
    if (params.targetId != null) q.append("targetId", String(params.targetId));
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<DrawingRevisionListResponse>(`/drawing-revisions${qs ? `?${qs}` : ""}`);
  },
  getDrawingRevision: (id: number) => api.get<DrawingRevisionResponse>(`/drawing-revisions/${id}`),
  createDrawingRevision: (body: CreateDrawingRevisionRequest) =>
    api.post<DrawingRevisionResponse>("/drawing-revisions", body),
  diffDrawingRevisions: (fromId: number, toId: number) =>
    api.get<DrawingRevisionDiffResponse>(`/drawing-revisions/diff?fromId=${fromId}&toId=${toId}`),
  listIfcReleases: (params: IfcReleaseListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.status) q.append("status", params.status);
    if (params.dateFrom) q.append("dateFrom", params.dateFrom);
    if (params.dateTo) q.append("dateTo", params.dateTo);
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<IfcReleaseListResponse>(`/ifc-releases${qs ? `?${qs}` : ""}`);
  },
  getIfcRelease: (id: number) => api.get<IfcReleaseResponse>(`/ifc-releases/${id}`),
  createIfcRelease: (body: CreateIfcReleaseRequest) =>
    api.post<IfcReleaseResponse>("/ifc-releases", body),
  updateIfcRelease: (id: number, body: UpdateIfcReleaseRequest) =>
    api.put<IfcReleaseResponse>(`/ifc-releases/${id}`, body),
  getIfcReleaseDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/ifc-releases/${id}/deletion-impact`),
  deleteIfcRelease: (id: number, body: ConfirmDeletionRequest) =>
    api.delete(`/ifc-releases/${id}`, { data: body }),
  addIfcReleaseItems: (id: number, body: AddIfcReleaseItemsRequest) =>
    api.post<IfcReleaseResponse>(`/ifc-releases/${id}/items`, body),
  removeIfcReleaseItem: (id: number, itemId: number) =>
    api.delete<IfcReleaseResponse>(`/ifc-releases/${id}/items/${itemId}`),
  addIfcReleaseRecipient: (id: number, body: AddIfcReleaseRecipientRequest) =>
    api.post<IfcReleaseResponse>(`/ifc-releases/${id}/recipients`, body),
  removeIfcReleaseRecipient: (id: number, recipientId: number) =>
    api.delete<IfcReleaseResponse>(`/ifc-releases/${id}/recipients/${recipientId}`),
  acknowledgeIfcReleaseRecipient: (
    id: number,
    recipientId: number,
    body: AcknowledgeIfcReleaseRecipientRequest,
  ) =>
    api.post<IfcReleaseResponse>(`/ifc-releases/${id}/recipients/${recipientId}/acknowledge`, body),
  releaseIfcRelease: (id: number) => api.post<IfcReleaseResponse>(`/ifc-releases/${id}/release`),
  cancelIfcRelease: (id: number) => api.post<IfcReleaseResponse>(`/ifc-releases/${id}/cancel`),
};
