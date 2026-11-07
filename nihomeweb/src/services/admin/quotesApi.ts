import api from "@/lib/api";
import type {
  QuoteListParams,
  QuoteListResponse,
  QuoteResponse,
  QuoteVersionsResponse,
  CreateQuoteRequest,
  UpdateQuoteRequest,
  QuoteApprovalCandidate,
  QuoteApprovalPreview,
  QuoteWorkflowRequest,
  QuoteEmailPreviewResponse,
  SendQuoteEmailRequest,
  ExtendQuoteValidityRequest,
  QuoteDocumentResponse,
  MaterialRateCatalogType,
  MaterialRateCatalogResponse,
  UpsertMaterialRateCatalogRequest,
  MaterialRateRevisionResponse,
  CreateMaterialRateRevisionRequest,
  UpsertInvestmentRateLineRequest,
  UpsertBoqMaterialRateLineRequest,
  MaterialRateImportResponse,
  BoqImportTemplateResponse,
  BoqWorkbookPreviewResponse,
  BoqWorkbookSheetPreviewResponse,
} from "./types/quotes";
import type {
  DeletionImpactResponse,
  ConfirmDeletionRequest,
  HardDeleteOperationResult,
} from "./types/operationalProjects";

export const quotesApi = {
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
  exportQuoteSpreadsheet: (id: number, lang: string) =>
    api.get<Blob>(`/quotes/${id}/export.xlsx`, { params: { lang }, responseType: "blob" }),
  createQuote: (body: CreateQuoteRequest) => api.post<QuoteResponse>("/quotes", body),
  updateQuote: (id: number, body: UpdateQuoteRequest) =>
    api.put<QuoteResponse>(`/quotes/${id}`, body),
  getQuoteApprovalCandidates: (id: number) =>
    api.get<QuoteApprovalCandidate[]>(`/quotes/${id}/approval-candidates`),
  previewQuoteApprovalEmail: (id: number, body: { approverUserId: number; rowVersion: string }) =>
    api.post<QuoteApprovalPreview>(`/quotes/${id}/approval-email-preview`, body),
  submitQuote: (
    id: number,
    body: QuoteWorkflowRequest & { approverUserId: number; previewToken: string },
  ) => api.post<QuoteResponse>(`/quotes/${id}/submit`, body),
  approveQuote: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/approve`, body),
  rejectQuoteInternal: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/reject-internal`, body),
  getQuoteEmailPreview: (id: number) =>
    api.get<QuoteEmailPreviewResponse>(`/quotes/${id}/email-preview`),
  sendQuoteToCustomer: (id: number, body: SendQuoteEmailRequest) =>
    api.post<QuoteResponse>(`/quotes/${id}/send`, body),
  markQuoteCustomerApproved: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/customer-approve`, body),
  markQuoteCustomerRejected: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/customer-reject`, body),
  cancelQuote: (id: number, body: QuoteWorkflowRequest = {}) =>
    api.post<QuoteResponse>(`/quotes/${id}/cancel`, body),
  extendQuoteValidity: (id: number, body: ExtendQuoteValidityRequest) =>
    api.post<QuoteResponse>(`/quotes/${id}/extend-validity`, body),
  listQuoteDocuments: (id: number) => api.get<QuoteDocumentResponse[]>(`/quotes/${id}/documents`),
  getQuoteDocumentContent: (id: number, documentId: number) =>
    api.get<Blob>(`/quotes/${id}/documents/${documentId}/content`, {
      responseType: "blob",
    }),
  uploadQuoteDocument: (id: number, file: File, label?: string) => {
    const formData = new FormData();
    formData.append("file", file);
    if (label?.trim()) formData.append("label", label.trim());
    return api.post<QuoteDocumentResponse>(`/quotes/${id}/documents`, formData);
  },
  deleteQuoteDocument: (id: number, documentId: number) =>
    api.delete(`/quotes/${id}/documents/${documentId}`),
  getQuoteDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/quotes/${id}/deletion-impact`),
  deleteQuote: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult>(`/quotes/${id}`, { data: body }),
  listMaterialRateCatalogs: (
    search?: string,
    includeInactive = false,
    catalogType?: MaterialRateCatalogType,
  ) =>
    api.get<MaterialRateCatalogResponse[]>("/material-rate-catalogs", {
      params: { search: search?.trim() || undefined, includeInactive, catalogType },
    }),
  getMaterialRateCatalog: (id: number) =>
    api.get<MaterialRateCatalogResponse>(`/material-rate-catalogs/${id}`),
  createMaterialRateCatalog: (body: UpsertMaterialRateCatalogRequest) =>
    api.post<MaterialRateCatalogResponse>("/material-rate-catalogs", body),
  updateMaterialRateCatalog: (id: number, body: UpsertMaterialRateCatalogRequest) =>
    api.put<MaterialRateCatalogResponse>(`/material-rate-catalogs/${id}`, body),
  deleteMaterialRateCatalog: (id: number) => api.delete(`/material-rate-catalogs/${id}`),
  downloadMaterialRateTemplate: (catalogType: MaterialRateCatalogType = "InvestmentRate") =>
    api.get<Blob>("/material-rate-catalogs/csv-template", {
      params: { catalogType },
      responseType: "blob",
    }),
  downloadMaterialRateExcelTemplate: (
    language: "vi" | "en" | "zh" | "ja",
    catalogType: MaterialRateCatalogType = "InvestmentRate",
  ) =>
    api.get<Blob>("/material-rate-catalogs/excel-template", {
      params: { language, catalogType },
      responseType: "blob",
    }),
  downloadMaterialRateTemplatePackage: (
    language: "vi" | "en" | "zh" | "ja",
    catalogType: MaterialRateCatalogType = "InvestmentRate",
  ) =>
    api.get<Blob>("/material-rate-catalogs/template-package", {
      params: { language, catalogType },
      responseType: "blob",
    }),
  listMaterialRateRevisions: (catalogId: number) =>
    api.get<MaterialRateRevisionResponse[]>(`/material-rate-catalogs/${catalogId}/revisions`),
  getMaterialRateRevision: (catalogId: number, revisionId: number) =>
    api.get<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}`,
    ),
  getEffectiveMaterialRateRevision: (catalogId: number, onDate: string) =>
    api.get<MaterialRateRevisionResponse>(`/material-rate-catalogs/${catalogId}/effective`, {
      params: { onDate },
    }),
  createMaterialRateRevision: (catalogId: number, body: CreateMaterialRateRevisionRequest) =>
    api.post<MaterialRateRevisionResponse>(`/material-rate-catalogs/${catalogId}/revisions`, body),
  createInvestmentMaterialRateLine: (
    catalogId: number,
    revisionId: number,
    body: UpsertInvestmentRateLineRequest,
  ) =>
    api.post<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/investment-lines`,
      body,
    ),
  updateInvestmentMaterialRateLine: (
    catalogId: number,
    revisionId: number,
    lineId: number,
    body: UpsertInvestmentRateLineRequest,
  ) =>
    api.put<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/investment-lines/${lineId}`,
      body,
    ),
  deleteInvestmentMaterialRateLine: (catalogId: number, revisionId: number, lineId: number) =>
    api.delete<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/investment-lines/${lineId}`,
    ),
  createBoqMaterialRateLine: (
    catalogId: number,
    revisionId: number,
    body: UpsertBoqMaterialRateLineRequest,
  ) =>
    api.post<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines`,
      body,
    ),
  updateBoqMaterialRateLine: (
    catalogId: number,
    revisionId: number,
    lineId: number,
    body: UpsertBoqMaterialRateLineRequest,
  ) =>
    api.put<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines/${lineId}`,
      body,
    ),
  deleteBoqMaterialRateLine: (catalogId: number, revisionId: number, lineId: number) =>
    api.delete<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/lines/${lineId}`,
    ),
  importMaterialRateRevision: (catalogId: number, revisionId: number, file: File) => {
    const formData = new FormData();
    formData.append("file", file);
    return api.post<MaterialRateImportResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/import`,
      formData,
    );
  },
  listBoqImportTemplates: () =>
    api.get<BoqImportTemplateResponse[]>("/material-rate-catalogs/boq-import-templates"),
  createBoqImportTemplate: (body: Omit<BoqImportTemplateResponse, "id" | "isSystem">) =>
    api.post<BoqImportTemplateResponse>("/material-rate-catalogs/boq-import-templates", body),
  previewBoqWorkbook: (catalogId: number, revisionId: number, file: File) => {
    const data = new FormData();
    data.append("file", file);
    return api.post<BoqWorkbookPreviewResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/boq-workbook/preview`,
      data,
    );
  },
  importBoqWorkbook: (
    catalogId: number,
    revisionId: number,
    file: File,
    sourceHash: string,
    mappings: BoqWorkbookSheetPreviewResponse[],
  ) => {
    const data = new FormData();
    const selectedMappings = mappings
      .filter((mapping) => mapping.include)
      .map((mapping) => ({
        sheetName: mapping.name,
        include: true,
        headerRow: mapping.headerRow,
        itemCodeColumn: mapping.itemCodeColumn,
        descriptionColumn: mapping.descriptionColumn,
        unitColumn: mapping.unitColumn,
        quantityColumn: mapping.quantityColumn,
        unitPriceColumn: mapping.unitPriceColumn,
      }));
    data.append("file", file);
    data.append("sourceHash", sourceHash);
    data.append("mappingsJson", JSON.stringify(selectedMappings));
    return api.post<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/boq-workbook/import`,
      data,
    );
  },
  approveMaterialRateRevision: (catalogId: number, revisionId: number, note?: string) =>
    api.post<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/approve`,
      { note: note?.trim() || null },
    ),
  rejectMaterialRateRevision: (catalogId: number, revisionId: number, note: string) =>
    api.post<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/reject`,
      { note: note.trim() },
    ),
  retireMaterialRateRevision: (catalogId: number, revisionId: number, note?: string) =>
    api.post<MaterialRateRevisionResponse>(
      `/material-rate-catalogs/${catalogId}/revisions/${revisionId}/retire`,
      { note: note?.trim() || null },
    ),
};
