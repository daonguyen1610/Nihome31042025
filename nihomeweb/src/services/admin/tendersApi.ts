import api from "@/lib/api";
import { withIdempotencyKey } from "@/lib/api";
import { withIfMatch } from "@/lib/api";
import type {
  CapabilityDocumentListParams,
  CapabilityDocumentListResponse,
  CapabilityDocumentDetailResponse,
  CapabilityDocumentUploadResponse,
  UpsertCapabilityDocumentRequest,
  CapabilityDocumentResponse,
  ReplaceCapabilityDocumentFileRequest,
} from "./types/capabilities";
import type {
  DeletionImpactResponse,
  ConfirmDeletionRequest,
  HardDeleteOperationResult,
} from "./types/operationalProjects";
import type {
  TenderStatisticsParams,
  TenderStatisticsResponse,
  TenderAssigneeOptionResponse,
  TenderListParams,
  TenderListResponse,
  TenderResponse,
  TenderReadinessResponse,
  CreateTenderRequest,
  UpdateTenderRequest,
  UpdateTenderChecklistItemRequest,
  CreateTenderChecklistItemRequest,
  AttachTenderChecklistFromLibraryRequest,
  MarkTenderWonRequest,
  MarkTenderLostRequest,
  TenderTimelineEvent,
  TransitionTenderRequest,
  SubmitTenderRequest,
  TenderEstimateRevisionResponse,
  TenderEstimateImportResponse,
} from "./types/tenders";
import { managedFileName } from "./helpers";

export const tendersApi = {
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
  getCapabilityDocumentContent: (path: string) =>
    api.get<Blob>(`/capability-documents/files/${managedFileName(path)}/content`, {
      responseType: "blob",
    }),
  uploadCapabilityDocument: (file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return api.post<CapabilityDocumentUploadResponse>("/capability-documents/upload", formData);
  },
  createCapabilityDocument: (body: UpsertCapabilityDocumentRequest) =>
    api.post<CapabilityDocumentResponse>("/capability-documents", body),
  updateCapabilityDocument: (id: number, body: UpsertCapabilityDocumentRequest) =>
    api.put<CapabilityDocumentResponse>(`/capability-documents/${id}`, body),
  replaceCapabilityDocumentFile: (id: number, body: ReplaceCapabilityDocumentFileRequest) =>
    api.post<CapabilityDocumentResponse>(`/capability-documents/${id}/replace-file`, body),
  getCapabilityDocumentDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/capability-documents/${id}/deletion-impact`),
  deleteCapabilityDocument: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult>(`/capability-documents/${id}`, { data: body }),
  downloadCapabilityDocumentsZip: (ids: number[]) =>
    api.post<Blob>(`/capability-documents/download-zip`, { ids }, { responseType: "blob" }),
  getTenderStatistics: (params: TenderStatisticsParams, signal?: AbortSignal) =>
    api.get<TenderStatisticsResponse>("/tenders/statistics", { params, signal }),
  getTenderReportPreparers: () =>
    api.get<TenderAssigneeOptionResponse[]>("/tenders/report-preparers"),
  getTenderReportCustomers: () =>
    api.get<{ id: number; name: string }[]>("/tenders/report-customers"),
  listTenders: (params: TenderListParams = {}) => {
    const q = new URLSearchParams();
    if (params.status) q.append("status", params.status);
    if (params.customerId != null) q.append("customerId", String(params.customerId));
    if (params.preparerUserId != null) q.append("preparerUserId", String(params.preparerUserId));
    if (params.openingMonth != null) q.append("openingMonth", String(params.openingMonth));
    if (params.openingYear != null) q.append("openingYear", String(params.openingYear));
    if (params.search) q.append("search", params.search);
    if (params.fromDate) q.append("fromDate", params.fromDate);
    if (params.toDate) q.append("toDate", params.toDate);
    if (params.submittedOnly) q.append("submittedOnly", "true");
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<TenderListResponse>(`/tenders${qs ? `?${qs}` : ""}`);
  },
  getTender: (id: number) => api.get<TenderResponse>(`/tenders/${id}`),
  getTenderReadiness: (id: number) => api.get<TenderReadinessResponse>(`/tenders/${id}/readiness`),
  uploadTenderInvitationDocument: (tenderId: number, file: File) => {
    const form = new FormData();
    form.append("file", file);
    return api.post<TenderResponse>(`/tenders/${tenderId}/invitation-documents`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  listTenderAssigneeOptions: () =>
    api.get<TenderAssigneeOptionResponse[]>("/tenders/assignee-options"),
  createTender: (body: CreateTenderRequest) => api.post<TenderResponse>("/tenders", body),
  updateTender: (id: number, body: UpdateTenderRequest) =>
    api.put<TenderResponse>(`/tenders/${id}`, body),
  getTenderDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/tenders/${id}/deletion-impact`),
  deleteTender: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult | null>(`/tenders/${id}`, { data: body }),
  updateTenderChecklistItem: (
    tenderId: number,
    itemId: number,
    body: UpdateTenderChecklistItemRequest,
  ) => api.patch<TenderResponse>(`/tenders/${tenderId}/checklist/${itemId}`, body),
  createTenderChecklistItem: (tenderId: number, body: CreateTenderChecklistItemRequest) =>
    api.post<TenderResponse>(`/tenders/${tenderId}/checklist`, body),
  deleteTenderChecklistItem: (tenderId: number, itemId: number) =>
    api.delete<TenderResponse>(`/tenders/${tenderId}/checklist/${itemId}`),
  uploadTenderChecklistFile: (tenderId: number, itemId: number, file: File) => {
    const form = new FormData();
    form.append("file", file);
    return api.post<TenderResponse>(`/tenders/${tenderId}/checklist/${itemId}/upload`, form, {
      headers: { "Content-Type": "multipart/form-data" },
    });
  },
  getTenderChecklistFileContent: (tenderId: number, itemId: number) =>
    api.get<Blob>(`/tenders/${tenderId}/checklist/${itemId}/content`, {
      responseType: "blob",
    }),
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
  transitionTender: (tenderId: number, body: TransitionTenderRequest) =>
    api.post<TenderResponse>(`/tenders/${tenderId}/transition`, body),
  submitTender: (tenderId: number, body: SubmitTenderRequest, idempotencyKey: string) =>
    api.post<TenderResponse>(`/tenders/${tenderId}/submit`, body, {
      headers: {
        ...withIdempotencyKey(idempotencyKey).headers,
        ...withIfMatch(body.rowVersion).headers,
      },
    }),
  downloadTenderEstimateTemplate: (tenderId: number) =>
    api.get<Blob>(`/tenders/${tenderId}/estimates/template`, { responseType: "blob" }),
  listTenderEstimates: (tenderId: number) =>
    api.get<TenderEstimateRevisionResponse[]>(`/tenders/${tenderId}/estimates`),
  getTenderEstimate: (tenderId: number, revisionId: number) =>
    api.get<TenderEstimateRevisionResponse>(`/tenders/${tenderId}/estimates/${revisionId}`),
  importTenderEstimate: (tenderId: number, file: File) => {
    const form = new FormData();
    form.append("file", file);
    return api.post<TenderEstimateImportResponse>(`/tenders/${tenderId}/estimates/import`, form);
  },
  submitTenderEstimate: (tenderId: number, revisionId: number, note?: string | null) =>
    api.post<TenderEstimateRevisionResponse>(
      `/tenders/${tenderId}/estimates/${revisionId}/submit`,
      { note },
    ),
  approveTenderEstimate: (tenderId: number, revisionId: number, note?: string | null) =>
    api.post<TenderEstimateRevisionResponse>(
      `/tenders/${tenderId}/estimates/${revisionId}/approve`,
      { note },
    ),
  rejectTenderEstimate: (tenderId: number, revisionId: number, note?: string | null) =>
    api.post<TenderEstimateRevisionResponse>(
      `/tenders/${tenderId}/estimates/${revisionId}/reject`,
      { note },
    ),
};
