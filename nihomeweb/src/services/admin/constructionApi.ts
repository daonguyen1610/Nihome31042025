import type { WorkPackageApprovalPreview } from "./types/content";
import api from "@/lib/api";
import { withIdempotencyKey } from "@/lib/api";
import { withIfMatch } from "@/lib/api";
import type {
  ConstructionTaskListParams,
  ConstructionTaskListResponse,
  ConstructionTaskDrawingsResponse,
  ConstructionTaskResponse,
  CreateConstructionTaskRequest,
  UpdateConstructionTaskRequest,
  UpdateConstructionTaskProgressRequest,
  SetConstructionTaskPredecessorsRequest,
  BulkDeleteConstructionTasksRequest,
  ConstructionTaskBulkDeleteResponse,
} from "./types/constructionTasks";
import type {
  SiteDiaryListParams,
  SiteDiaryListResponse,
  SiteDiaryResponse,
  CreateSiteDiaryRequest,
  UpdateSiteDiaryRequest,
  BulkDeleteSiteDiariesRequest,
  SiteDiaryBulkDeleteResponse,
} from "./types/siteDiaries";
import type {
  PunchItemListParams,
  PunchItemListResponse,
  PunchItemResponse,
  CreatePunchItemRequest,
  UpdatePunchItemRequest,
  TransitionPunchStatusRequest,
  BulkDeletePunchItemsRequest,
  PunchItemBulkDeleteResponse,
} from "./types/punchList";
import type {
  ProcurementWorkspaceResponse,
  MaterialRequestListParams,
  MaterialRequestListResponse,
  ProjectBoqRevisionListParams,
  ProjectBoqRevisionListResponse,
  ProjectBoqRevisionResponse,
  ProjectBoqRevisionRequest,
  BoqLibraryTemplate,
  BoqLibrarySheetOutline,
  BoqTemplateResponse,
  ProjectBoqWorkbookPreviewResponse,
  WorkPackageBoqWorkbook,
  MaterialRequestUpsertRequest,
  MaterialRequestResponse,
  MaterialRequestDetailResponse,
  MaterialAlertListParams,
  MaterialAlertListResponse,
  MaterialAlertResponse,
  ProcurementContractLineRequest,
  ProcurementContractLineResponse,
  WarehouseTransactionListParams,
  WarehouseTransactionListResponse,
  WarehouseReceiptDetailResponse,
  WarehouseReceiptUpsertRequest,
  WarehouseReceiptResponse,
  WarehouseIssueDetailResponse,
  WarehouseIssueUpsertRequest,
  WarehouseIssueResponse,
  VendorRatingUpsertRequest,
  VendorRatingResponse,
} from "./types/procurement";
import type {
  PaymentReferencesResponse,
  PaymentRequestResponse,
  PaymentRequestUpsertRequest,
  AccountingPeriodResponse,
  AccountingCorrectionResponse,
  AccountingCorrectionUpsertRequest,
  AcceptanceRecordListParams,
  AcceptanceRecordListResponse,
  AcceptanceRecordResponse,
  CreateAcceptanceRecordRequest,
  UpdateAcceptanceRecordRequest,
  TransitionAcceptanceStatusRequest,
  BulkDeleteAcceptanceRecordsRequest,
  AcceptanceRecordBulkDeleteResponse,
} from "./types/finance";
import type {
  AsBuiltDocumentListParams,
  AsBuiltDocumentListResponse,
  AsBuiltDocumentResponse,
  CreateAsBuiltDocumentRequest,
  UpdateAsBuiltDocumentRequest,
  TransitionAsBuiltStatusRequest,
  BulkDeleteAsBuiltDocumentsRequest,
  AsBuiltDocumentBulkDeleteResponse,
} from "./types/asBuilt";
import type {
  HandoverRecordListParams,
  HandoverRecordListResponse,
  HandoverRecordResponse,
  CreateHandoverRecordRequest,
  UpdateHandoverRecordRequest,
  TransitionHandoverStatusRequest,
} from "./types/handover";
import { postIdempotent, uploadBusinessDocument } from "./helpers";

export const constructionApi = {
  listConstructionTasks: (params: ConstructionTaskListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.ownerUserId != null) q.append("ownerUserId", String(params.ownerUserId));
    if (params.status) q.append("status", params.status);
    if (params.search) q.append("search", params.search);
    if (params.overdueOnly) q.append("overdueOnly", "true");
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<ConstructionTaskListResponse>(`/construction-tasks${qs ? `?${qs}` : ""}`);
  },
  getConstructionTaskDrawings: (id: number) =>
    api.get<ConstructionTaskDrawingsResponse>(`/construction-tasks/${id}/drawings`),
  setConstructionTaskDrawings: (
    id: number,
    body: { version: string; ifcReleaseItemIds: number[] },
  ) => api.put<ConstructionTaskDrawingsResponse>(`/construction-tasks/${id}/drawings`, body),
  getConstructionTaskDrawingContent: (id: number, itemId: number) =>
    api.get<Blob>(`/construction-tasks/${id}/drawings/${itemId}/content`, { responseType: "blob" }),
  getConstructionTask: (id: number) =>
    api.get<ConstructionTaskResponse>(`/construction-tasks/${id}`),
  createConstructionTask: (body: CreateConstructionTaskRequest) =>
    api.post<ConstructionTaskResponse>("/construction-tasks", body),
  updateConstructionTask: (id: number, body: UpdateConstructionTaskRequest) =>
    api.put<ConstructionTaskResponse>(`/construction-tasks/${id}`, body),
  updateConstructionTaskProgress: (id: number, body: UpdateConstructionTaskProgressRequest) =>
    api.patch<ConstructionTaskResponse>(`/construction-tasks/${id}/progress`, body),
  setConstructionTaskPredecessors: (id: number, body: SetConstructionTaskPredecessorsRequest) =>
    api.put<ConstructionTaskResponse>(`/construction-tasks/${id}/predecessors`, body),
  deleteConstructionTask: (id: number) => api.delete(`/construction-tasks/${id}`),
  bulkDeleteConstructionTasks: (body: BulkDeleteConstructionTasksRequest) =>
    api.post<ConstructionTaskBulkDeleteResponse>("/construction-tasks/bulk-delete", body),
  listSiteDiaries: (params: SiteDiaryListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.weatherCode) q.append("weatherCode", params.weatherCode);
    if (params.status) q.append("status", params.status);
    if (params.dateFrom) q.append("dateFrom", params.dateFrom);
    if (params.dateTo) q.append("dateTo", params.dateTo);
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<SiteDiaryListResponse>(`/site-diaries${qs ? `?${qs}` : ""}`);
  },
  getSiteDiary: (id: number) => api.get<SiteDiaryResponse>(`/site-diaries/${id}`),
  createSiteDiary: (body: CreateSiteDiaryRequest) =>
    api.post<SiteDiaryResponse>("/site-diaries", body),
  updateSiteDiary: (id: number, body: UpdateSiteDiaryRequest) =>
    api.put<SiteDiaryResponse>(`/site-diaries/${id}`, body),
  submitSiteDiary: (id: number) => api.post<SiteDiaryResponse>(`/site-diaries/${id}/submit`),
  confirmSiteDiary: (id: number) => api.post<SiteDiaryResponse>(`/site-diaries/${id}/confirm`),
  reopenSiteDiary: (id: number) => api.post<SiteDiaryResponse>(`/site-diaries/${id}/reopen`),
  deleteSiteDiary: (id: number) => api.delete(`/site-diaries/${id}`),
  bulkDeleteSiteDiaries: (body: BulkDeleteSiteDiariesRequest) =>
    api.post<SiteDiaryBulkDeleteResponse>("/site-diaries/bulk-delete", body),
  listPunchItems: (params: PunchItemListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.assigneeUserId != null) q.append("assigneeUserId", String(params.assigneeUserId));
    if (params.status) q.append("status", params.status);
    if (params.severity) q.append("severity", params.severity);
    if (params.openOnly) q.append("openOnly", "true");
    if (params.overdueOnly) q.append("overdueOnly", "true");
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<PunchItemListResponse>(`/punch-items${qs ? `?${qs}` : ""}`);
  },
  getPunchItem: (id: number) => api.get<PunchItemResponse>(`/punch-items/${id}`),
  createPunchItem: (body: CreatePunchItemRequest) =>
    api.post<PunchItemResponse>("/punch-items", body),
  updatePunchItem: (id: number, body: UpdatePunchItemRequest) =>
    api.put<PunchItemResponse>(`/punch-items/${id}`, body),
  transitionPunchStatus: (id: number, body: TransitionPunchStatusRequest) =>
    api.post<PunchItemResponse>(`/punch-items/${id}/status`, body),
  verifyPunchItem: (id: number, body?: Partial<TransitionPunchStatusRequest>) =>
    api.post<PunchItemResponse>(`/punch-items/${id}/verify`, body ?? {}),
  deletePunchItem: (id: number) => api.delete(`/punch-items/${id}`),
  bulkDeletePunchItems: (body: BulkDeletePunchItemsRequest) =>
    api.post<PunchItemBulkDeleteResponse>("/punch-items/bulk-delete", body),
  getProcurementWorkspace: (projectId: number) =>
    api.get<ProcurementWorkspaceResponse>(`/operational-projects/${projectId}/procurement`),
  listMaterialRequests: (projectId: number, params: MaterialRequestListParams = {}) => {
    const query = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== "") query.append(key, String(value));
    });
    const suffix = query.toString();
    return api.get<MaterialRequestListResponse>(
      `/operational-projects/${projectId}/procurement/material-requests${suffix ? `?${suffix}` : ""}`,
    );
  },
  listProjectBoqRevisions: (projectId: number, params: ProjectBoqRevisionListParams = {}) =>
    api.get<ProjectBoqRevisionListResponse>(
      `/operational-projects/${projectId}/procurement/boq-revisions`,
      { params },
    ),
  exportProjectBoqRevisions: (projectId: number, params: ProjectBoqRevisionListParams = {}) =>
    api.get<Blob>(`/operational-projects/${projectId}/procurement/boq-revisions/export`, {
      params,
      responseType: "blob",
    }),
  getProjectBoqRevision: (projectId: number, id: number) =>
    api.get<ProjectBoqRevisionResponse>(
      `/operational-projects/${projectId}/procurement/boq-revisions/${id}`,
    ),
  listBoqLibrary: () => api.get<BoqLibraryTemplate[]>("/boq-templates"),
  downloadBoqLibraryFile: (code: string) =>
    api.get<Blob>(`/boq-templates/${code}/file`, { responseType: "blob" }),
  getBoqLibrarySheet: (code: string, sheet: string) =>
    api.get<BoqLibrarySheetOutline>(`/boq-templates/${code}/sheets/${encodeURIComponent(sheet)}`),
  getBoqTemplates: (projectId: number) =>
    api.get<BoqTemplateResponse[]>(`/operational-projects/${projectId}/procurement/boq-templates`),
  downloadBoqTemplate: (projectId: number, code: string) =>
    api.get<Blob>(`/operational-projects/${projectId}/procurement/boq-templates/${code}/file`, {
      responseType: "blob",
    }),
  previewWorkPackageBoq: (packageId: number, templateCode: string, file: File) => {
    const form = new FormData();
    form.append("templateCode", templateCode);
    form.append("file", file);
    return api.post<ProjectBoqWorkbookPreviewResponse>(
      `/work-packages/${packageId}/boq-workbooks/preview`,
      form,
    );
  },
  importWorkPackageBoq: (
    packageId: number,
    body: { templateCode: string; file: File; sourceHash: string },
  ) => {
    const form = new FormData();
    form.append("templateCode", body.templateCode);
    form.append("file", body.file);
    form.append("sourceHash", body.sourceHash);
    return api.post<WorkPackageBoqWorkbook>(`/work-packages/${packageId}/boq-workbooks`, form);
  },
  downloadWorkPackageBoq: (packageId: number, id: number) =>
    api.get<Blob>(`/work-packages/${packageId}/boq-workbooks/${id}/file`, { responseType: "blob" }),
  previewWorkPackageBoqApproval: (
    packageId: number,
    id: number,
    recipients?: { toEmail: string; ccEmails: string[] },
  ) =>
    api.post<WorkPackageApprovalPreview>(
      `/work-packages/${packageId}/boq-workbooks/${id}/approval-email-preview`,
      recipients,
    ),
  submitWorkPackageBoq: (
    packageId: number,
    id: number,
    rowVersion: string,
    idempotencyKey: string,
    previewToken: string,
  ) =>
    api.post<WorkPackageBoqWorkbook>(
      `/work-packages/${packageId}/boq-workbooks/${id}/submit`,
      { rowVersion, previewToken },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  decideWorkPackageBoq: (
    packageId: number,
    id: number,
    approved: boolean,
    reason: string | null,
    rowVersion: string,
    idempotencyKey: string,
  ) =>
    api.post<WorkPackageBoqWorkbook>(
      `/work-packages/${packageId}/boq-workbooks/${id}/decision`,
      { approved, reason, rowVersion },
      {
        headers: {
          ...withIdempotencyKey(idempotencyKey).headers,
          ...withIfMatch(rowVersion).headers,
        },
      },
    ),
  previewProjectBoqWorkbook: (projectId: number, templateCode: string, file: File) => {
    const form = new FormData();
    form.append("templateCode", templateCode);
    form.append("file", file);
    return api.post<ProjectBoqWorkbookPreviewResponse>(
      `/operational-projects/${projectId}/procurement/boq-workbooks/preview`,
      form,
    );
  },
  importProjectBoqWorkbook: (
    projectId: number,
    body: { templateCode: string; file: File; sourceHash: string; upstreamContractId?: number },
  ) => {
    const form = new FormData();
    form.append("templateCode", body.templateCode);
    form.append("file", body.file);
    form.append("sourceHash", body.sourceHash);
    if (body.upstreamContractId) form.append("upstreamContractId", String(body.upstreamContractId));
    return api.post<ProjectBoqRevisionResponse>(
      `/operational-projects/${projectId}/procurement/boq-workbooks`,
      form,
    );
  },
  downloadBoqWorkbook: (projectId: number, id: number) =>
    api.get<Blob>(`/operational-projects/${projectId}/procurement/boq-revisions/${id}/workbook`, {
      responseType: "blob",
    }),
  createProjectBoqRevision: (projectId: number, body: ProjectBoqRevisionRequest) =>
    postIdempotent<ProjectBoqRevisionResponse>(
      `/operational-projects/${projectId}/procurement/boq-revisions`,
      body,
    ),
  updateProjectBoqRevision: (projectId: number, id: number, body: ProjectBoqRevisionRequest) =>
    api.put<ProjectBoqRevisionResponse>(
      `/operational-projects/${projectId}/procurement/boq-revisions/${id}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(crypto.randomUUID()).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  submitProjectBoqRevision: (projectId: number, id: number, rowVersion: string) =>
    postIdempotent<ProjectBoqRevisionResponse>(
      `/operational-projects/${projectId}/procurement/boq-revisions/${id}/submit`,
      { rowVersion },
    ),
  decideProjectBoqRevision: (
    projectId: number,
    id: number,
    approved: boolean,
    rowVersion: string,
    reason?: string,
  ) =>
    postIdempotent<ProjectBoqRevisionResponse>(
      `/operational-projects/${projectId}/procurement/boq-revisions/${id}/decision`,
      { approved, rowVersion, reason },
    ),
  convertProjectBoqToContract: (
    projectId: number,
    id: number,
    upstreamContractId: number,
    rowVersion: string,
  ) =>
    postIdempotent<ProjectBoqRevisionResponse>(
      `/operational-projects/${projectId}/procurement/boq-revisions/${id}/contract`,
      { upstreamContractId, rowVersion },
    ),
  createMaterialRequest: (projectId: number, body: MaterialRequestUpsertRequest) =>
    postIdempotent<MaterialRequestResponse>(
      `/operational-projects/${projectId}/procurement/material-requests`,
      body,
    ),
  getMaterialRequest: (projectId: number, id: number) =>
    api.get<MaterialRequestDetailResponse>(
      `/operational-projects/${projectId}/procurement/material-requests/${id}`,
    ),
  updateMaterialRequest: (projectId: number, id: number, body: MaterialRequestUpsertRequest) =>
    api.put<MaterialRequestResponse>(
      `/operational-projects/${projectId}/procurement/material-requests/${id}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(crypto.randomUUID()).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  submitMaterialRequest: (projectId: number, id: number, rowVersion: string) =>
    postIdempotent<MaterialRequestResponse>(
      `/operational-projects/${projectId}/procurement/material-requests/${id}/submit`,
      { rowVersion },
    ),
  decideMaterialRequest: (
    projectId: number,
    id: number,
    approved: boolean,
    rowVersion: string,
    reason?: string,
  ) =>
    postIdempotent<MaterialRequestResponse>(
      `/operational-projects/${projectId}/procurement/material-requests/${id}/decision`,
      { approved, rowVersion, reason },
    ),
  cancelMaterialRequest: (projectId: number, id: number, rowVersion: string, reason: string) =>
    postIdempotent<MaterialRequestResponse>(
      `/operational-projects/${projectId}/procurement/material-requests/${id}/cancel`,
      { rowVersion, reason },
    ),
  listMaterialAlerts: (projectId: number, params: MaterialAlertListParams = {}) =>
    api.get<MaterialAlertListResponse>(
      `/operational-projects/${projectId}/procurement/material-alerts`,
      { params },
    ),
  getMaterialAlert: (projectId: number, id: number) =>
    api.get<MaterialAlertResponse>(
      `/operational-projects/${projectId}/procurement/material-alerts/${id}`,
    ),
  evaluateMaterialAlerts: (projectId: number) =>
    postIdempotent<MaterialAlertResponse[]>(
      `/operational-projects/${projectId}/procurement/material-alerts/evaluate`,
      {},
    ),
  acknowledgeMaterialAlert: (projectId: number, id: number, note: string, rowVersion: string) =>
    postIdempotent<MaterialAlertResponse>(
      `/operational-projects/${projectId}/procurement/material-alerts/${id}/acknowledge`,
      { note, rowVersion },
    ),
  createProcurementContractLine: (projectId: number, body: ProcurementContractLineRequest) =>
    postIdempotent<ProcurementContractLineResponse>(
      `/operational-projects/${projectId}/procurement/contract-lines`,
      body,
    ),
  listWarehouseTransactions: (projectId: number, params: WarehouseTransactionListParams = {}) =>
    api.get<WarehouseTransactionListResponse>(
      `/operational-projects/${projectId}/procurement/warehouse-transactions`,
      { params },
    ),
  getWarehouseReceipt: (projectId: number, id: number) =>
    api.get<WarehouseReceiptDetailResponse>(
      `/operational-projects/${projectId}/procurement/receipts/${id}`,
    ),
  createWarehouseReceipt: (projectId: number, body: WarehouseReceiptUpsertRequest) =>
    postIdempotent<WarehouseReceiptResponse>(
      `/operational-projects/${projectId}/procurement/receipts`,
      body,
    ),
  updateWarehouseReceipt: (projectId: number, id: number, body: WarehouseReceiptUpsertRequest) =>
    api.put<WarehouseReceiptResponse>(
      `/operational-projects/${projectId}/procurement/receipts/${id}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(crypto.randomUUID()).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  postWarehouseReceipt: (projectId: number, id: number, rowVersion: string) =>
    postIdempotent<WarehouseReceiptResponse>(
      `/operational-projects/${projectId}/procurement/receipts/${id}/post`,
      { rowVersion },
    ),
  reverseWarehouseReceipt: (projectId: number, id: number, rowVersion: string, reason: string) =>
    postIdempotent<WarehouseReceiptResponse>(
      `/operational-projects/${projectId}/procurement/receipts/${id}/reverse`,
      { rowVersion, reason },
    ),
  getWarehouseIssue: (projectId: number, id: number) =>
    api.get<WarehouseIssueDetailResponse>(
      `/operational-projects/${projectId}/procurement/issues/${id}`,
    ),
  createWarehouseIssue: (projectId: number, body: WarehouseIssueUpsertRequest) =>
    postIdempotent<WarehouseIssueResponse>(
      `/operational-projects/${projectId}/procurement/issues`,
      body,
    ),
  updateWarehouseIssue: (projectId: number, id: number, body: WarehouseIssueUpsertRequest) =>
    api.put<WarehouseIssueResponse>(
      `/operational-projects/${projectId}/procurement/issues/${id}`,
      body,
      {
        headers: {
          ...withIdempotencyKey(crypto.randomUUID()).headers,
          ...withIfMatch(body.rowVersion).headers,
        },
      },
    ),
  postWarehouseIssue: (projectId: number, id: number, rowVersion: string) =>
    postIdempotent<WarehouseIssueResponse>(
      `/operational-projects/${projectId}/procurement/issues/${id}/post`,
      { rowVersion },
    ),
  reverseWarehouseIssue: (projectId: number, id: number, rowVersion: string, reason: string) =>
    postIdempotent<WarehouseIssueResponse>(
      `/operational-projects/${projectId}/procurement/issues/${id}/reverse`,
      { rowVersion, reason },
    ),
  createVendorRating: (projectId: number, body: VendorRatingUpsertRequest) =>
    postIdempotent<VendorRatingResponse>(
      `/operational-projects/${projectId}/procurement/vendor-ratings`,
      body,
    ),
  submitVendorRating: (projectId: number, id: number, rowVersion: string) =>
    postIdempotent<VendorRatingResponse>(
      `/operational-projects/${projectId}/procurement/vendor-ratings/${id}/submit`,
      { rowVersion },
    ),
  decideVendorRating: (
    projectId: number,
    id: number,
    approved: boolean,
    rowVersion: string,
    reason?: string,
  ) =>
    postIdempotent<VendorRatingResponse>(
      `/operational-projects/${projectId}/procurement/vendor-ratings/${id}/decision`,
      { approved, rowVersion, reason },
    ),
  getPaymentReferences: () => api.get<PaymentReferencesResponse>("/finance/payment-references"),
  listPaymentRequests: () => api.get<PaymentRequestResponse[]>("/finance/payment-requests"),
  createPaymentRequest: (body: PaymentRequestUpsertRequest) =>
    postIdempotent<PaymentRequestResponse>("/finance/payment-requests", body),
  updatePaymentRequest: (id: number, body: PaymentRequestUpsertRequest) =>
    api.put<PaymentRequestResponse>(`/finance/payment-requests/${id}`, body),
  revisePaymentRequest: (id: number, rowVersion: string, reason: string) =>
    postIdempotent<PaymentRequestResponse>(`/finance/payment-requests/${id}/revise`, {
      rowVersion,
      reason,
    }),
  submitPaymentRequest: (id: number, rowVersion: string, reason?: string) =>
    postIdempotent<PaymentRequestResponse>(`/finance/payment-requests/${id}/submit`, {
      rowVersion,
      reason,
    }),
  validatePaymentRequest: (id: number, rowVersion: string, reason?: string) =>
    postIdempotent<PaymentRequestResponse>(`/finance/payment-requests/${id}/validate`, {
      rowVersion,
      reason,
    }),
  decidePaymentRequest: (id: number, approved: boolean, rowVersion: string, reason?: string) =>
    postIdempotent<PaymentRequestResponse>(`/finance/payment-requests/${id}/decision`, {
      approved,
      rowVersion,
      reason,
    }),
  payPaymentRequest: (id: number, rowVersion: string, reason?: string) =>
    postIdempotent<PaymentRequestResponse>(`/finance/payment-requests/${id}/pay`, {
      rowVersion,
      reason,
    }),
  cancelPaymentRequest: (id: number, rowVersion: string, reason?: string) =>
    postIdempotent<PaymentRequestResponse>(`/finance/payment-requests/${id}/cancel`, {
      rowVersion,
      reason,
    }),
  listAccountingPeriods: () => api.get<AccountingPeriodResponse[]>("/finance/periods"),
  createAccountingPeriod: (year: number, month: number) =>
    postIdempotent<AccountingPeriodResponse>("/finance/periods", { year, month }),
  startClosingAccountingPeriod: (id: number, rowVersion: string, reason?: string) =>
    postIdempotent<AccountingPeriodResponse>(`/finance/periods/${id}/start-closing`, {
      rowVersion,
      reason,
    }),
  closeAccountingPeriod: (id: number, rowVersion: string, reason?: string) =>
    postIdempotent<AccountingPeriodResponse>(`/finance/periods/${id}/close`, {
      rowVersion,
      reason,
    }),
  listAccountingCorrections: () => api.get<AccountingCorrectionResponse[]>("/finance/corrections"),
  createAccountingCorrection: (body: AccountingCorrectionUpsertRequest) =>
    postIdempotent<AccountingCorrectionResponse>("/finance/corrections", body),
  submitAccountingCorrection: (id: number, rowVersion: string, reason?: string) =>
    postIdempotent<AccountingCorrectionResponse>(`/finance/corrections/${id}/submit`, {
      rowVersion,
      reason,
    }),
  decideAccountingCorrection: (
    id: number,
    approved: boolean,
    rowVersion: string,
    reason?: string,
  ) =>
    postIdempotent<AccountingCorrectionResponse>(`/finance/corrections/${id}/decision`, {
      approved,
      rowVersion,
      reason,
    }),
  reverseAccountingCorrection: (id: number, rowVersion: string, reason: string) =>
    postIdempotent<AccountingCorrectionResponse>(`/finance/corrections/${id}/reverse`, {
      rowVersion,
      reason,
    }),
  listAcceptanceRecords: (params: AcceptanceRecordListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.constructionTaskId != null)
      q.append("constructionTaskId", String(params.constructionTaskId));
    if (params.responsibleUserId != null)
      q.append("responsibleUserId", String(params.responsibleUserId));
    if (params.acceptanceFrom) q.append("acceptanceFrom", params.acceptanceFrom);
    if (params.acceptanceTo) q.append("acceptanceTo", params.acceptanceTo);
    if (params.status) q.append("status", params.status);
    if (params.search) q.append("search", params.search);
    if (params.openOnly) q.append("openOnly", "true");
    if (params.overdueOnly) q.append("overdueOnly", "true");
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortDirection) q.append("sortDirection", params.sortDirection);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<AcceptanceRecordListResponse>(`/acceptance-records${qs ? `?${qs}` : ""}`);
  },
  getAcceptanceRecord: (id: number) =>
    api.get<AcceptanceRecordResponse>(`/acceptance-records/${id}`),
  createAcceptanceRecord: (body: CreateAcceptanceRecordRequest) =>
    api.post<AcceptanceRecordResponse>("/acceptance-records", body),
  updateAcceptanceRecord: (id: number, body: UpdateAcceptanceRecordRequest) =>
    api.put<AcceptanceRecordResponse>(`/acceptance-records/${id}`, body),
  transitionAcceptanceStatus: (id: number, body: TransitionAcceptanceStatusRequest) =>
    api.post<AcceptanceRecordResponse>(`/acceptance-records/${id}/status`, body),
  approveAcceptanceRecord: (id: number, body: TransitionAcceptanceStatusRequest) =>
    api.post<AcceptanceRecordResponse>(`/acceptance-records/${id}/approve`, body),
  deleteAcceptanceRecord: (id: number) => api.delete(`/acceptance-records/${id}`),
  bulkDeleteAcceptanceRecords: (body: BulkDeleteAcceptanceRecordsRequest) =>
    api.post<AcceptanceRecordBulkDeleteResponse>("/acceptance-records/bulk-delete", body),
  uploadAcceptanceDocument: (file: File) => uploadBusinessDocument("acceptance", file),
  listAsBuiltDocuments: (params: AsBuiltDocumentListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.category) q.append("category", params.category);
    if (params.status) q.append("status", params.status);
    if (params.search) q.append("search", params.search);
    if (params.openOnly) q.append("openOnly", "true");
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortDirection) q.append("sortDirection", params.sortDirection);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<AsBuiltDocumentListResponse>(`/as-built-documents${qs ? `?${qs}` : ""}`);
  },
  exportAsBuiltDocuments: (params: AsBuiltDocumentListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.category) q.append("category", params.category);
    if (params.status) q.append("status", params.status);
    if (params.search) q.append("search", params.search);
    if (params.openOnly) q.append("openOnly", "true");
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortDirection) q.append("sortDirection", params.sortDirection);
    const qs = q.toString();
    return api.get<Blob>(`/as-built-documents/export${qs ? `?${qs}` : ""}`, {
      responseType: "blob",
    });
  },
  getAsBuiltDocument: (id: number) => api.get<AsBuiltDocumentResponse>(`/as-built-documents/${id}`),
  createAsBuiltDocument: (body: CreateAsBuiltDocumentRequest) =>
    api.post<AsBuiltDocumentResponse>("/as-built-documents", body),
  updateAsBuiltDocument: (id: number, body: UpdateAsBuiltDocumentRequest) =>
    api.put<AsBuiltDocumentResponse>(`/as-built-documents/${id}`, body),
  transitionAsBuiltStatus: (id: number, body: TransitionAsBuiltStatusRequest) =>
    api.post<AsBuiltDocumentResponse>(`/as-built-documents/${id}/status`, body),
  approveAsBuiltDocument: (id: number, body: TransitionAsBuiltStatusRequest) =>
    api.post<AsBuiltDocumentResponse>(`/as-built-documents/${id}/approve`, body),
  deleteAsBuiltDocument: (id: number) => api.delete(`/as-built-documents/${id}`),
  bulkDeleteAsBuiltDocuments: (body: BulkDeleteAsBuiltDocumentsRequest) =>
    api.post<AsBuiltDocumentBulkDeleteResponse>("/as-built-documents/bulk-delete", body),
  uploadAsBuiltDocumentFile: (file: File) => uploadBusinessDocument("as-built", file),
  listHandoverRecords: (params: HandoverRecordListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.responsibleUserId != null)
      q.append("responsibleUserId", String(params.responsibleUserId));
    if (params.status) q.append("status", params.status);
    if (params.plannedFrom) q.append("plannedFrom", params.plannedFrom);
    if (params.plannedTo) q.append("plannedTo", params.plannedTo);
    if (params.search) q.append("search", params.search);
    if (params.readyOnly) q.append("readyOnly", "true");
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortDirection) q.append("sortDirection", params.sortDirection);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<HandoverRecordListResponse>(`/handover-records${qs ? `?${qs}` : ""}`);
  },
  exportHandoverRecords: (params: HandoverRecordListParams = {}) => {
    const q = new URLSearchParams();
    if (params.designProjectId != null) q.append("designProjectId", String(params.designProjectId));
    if (params.responsibleUserId != null)
      q.append("responsibleUserId", String(params.responsibleUserId));
    if (params.status) q.append("status", params.status);
    if (params.plannedFrom) q.append("plannedFrom", params.plannedFrom);
    if (params.plannedTo) q.append("plannedTo", params.plannedTo);
    if (params.search) q.append("search", params.search);
    if (params.readyOnly) q.append("readyOnly", "true");
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortDirection) q.append("sortDirection", params.sortDirection);
    const qs = q.toString();
    return api.get<Blob>(`/handover-records/export${qs ? `?${qs}` : ""}`, { responseType: "blob" });
  },
  getHandoverRecord: (id: number) => api.get<HandoverRecordResponse>(`/handover-records/${id}`),
  createHandoverRecord: (body: CreateHandoverRecordRequest) =>
    api.post<HandoverRecordResponse>("/handover-records", body),
  uploadHandoverDocument: (file: File) => uploadBusinessDocument("handover", file),
  updateHandoverRecord: (id: number, body: UpdateHandoverRecordRequest) =>
    api.put<HandoverRecordResponse>(`/handover-records/${id}`, body),
  transitionHandoverStatus: (id: number, body: TransitionHandoverStatusRequest) =>
    api.post<HandoverRecordResponse>(`/handover-records/${id}/status`, body),
  completeHandoverRecord: (id: number, body: TransitionHandoverStatusRequest) =>
    api.post<HandoverRecordResponse>(`/handover-records/${id}/complete`, body),
  deleteHandoverRecord: (id: number) => api.delete(`/handover-records/${id}`),
};
