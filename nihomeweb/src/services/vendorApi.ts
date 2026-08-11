import api from "@/lib/api";

export type VendorType = "Supplier" | "SubContractor" | "Both";
export type VendorDocumentType = "Capability" | "License" | "Other";
export type VendorSortField = "vendorCode" | "companyName" | "vendorType" | "ownerName" | "averageScore" | "updatedAt";
export type SortDirection = "asc" | "desc";

export interface VendorDocumentResponse {
  id: number;
  documentType: VendorDocumentType;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  createdAt: string;
  createdByUserId: number;
}

export interface VendorEvaluationResponse {
  id: number;
  projectId: number;
  projectName: string;
  projectCode: string;
  scoreQuality: number;
  scoreSchedule: number;
  scoreCost: number;
  scoreSafety: number;
  averageScore: number;
  comment?: string | null;
  evaluatedByUserId: number;
  evaluatorName: string;
  evaluatedAt: string;
  updatedByUserId: number;
  updatedByName: string;
  updatedAt: string;
}

export interface VendorResponse {
  id: number;
  vendorCode: string;
  companyName: string;
  vendorType: VendorType;
  taxCode?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  contactPerson?: string | null;
  licenseNo?: string | null;
  serviceGroupCode: string;
  ownerUserId: number;
  ownerName: string;
  isActive: boolean;
  createdAt: string;
  createdByUserId: number;
  updatedAt: string;
  updatedByUserId: number;
  averageScore?: number | null;
  documents: VendorDocumentResponse[];
  evaluations: VendorEvaluationResponse[];
}

export interface VendorListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: VendorResponse[];
}

export interface VendorListParams {
  search?: string;
  type?: VendorType;
  isActive?: boolean;
  ownerUserId?: number;
  serviceGroupCode?: string;
  sortBy?: VendorSortField;
  sortDirection?: SortDirection;
  page?: number;
  pageSize?: number;
}

export interface UpsertVendorRequest {
  vendorCode: string;
  companyName: string;
  vendorType: VendorType;
  taxCode?: string | null;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  contactPerson?: string | null;
  licenseNo?: string | null;
  serviceGroupCode: string;
  ownerUserId: number;
  isActive: boolean;
}

export interface UpsertVendorEvaluationRequest {
  projectId: number;
  scoreQuality: number;
  scoreSchedule: number;
  scoreCost: number;
  scoreSafety: number;
  comment?: string | null;
}

export interface VendorAuditResponse {
  id: number;
  action: string;
  message: string;
  actorUserId?: number | null;
  actorPhone?: string | null;
  status: string;
  oldValueJson?: string | null;
  newValueJson?: string | null;
  createdAt: string;
}

export interface VendorOwnerOptionResponse {
  id: number;
  fullName: string;
  email: string;
  phoneNumber: string;
}

export interface VendorProjectOptionResponse {
  id: number;
  projectCode: string;
  name: string;
}

const buildParams = (params: VendorListParams, includePaging: boolean) => {
  const query: Record<string, string | number | boolean> = {};
  if (params.search?.trim()) query.search = params.search.trim();
  if (params.type) query.type = params.type;
  if (params.isActive != null) query.isActive = params.isActive;
  if (params.ownerUserId != null) query.ownerUserId = params.ownerUserId;
  if (params.serviceGroupCode) query.serviceGroupCode = params.serviceGroupCode;
  if (params.sortBy) query.sortBy = params.sortBy;
  if (params.sortDirection) query.sortDirection = params.sortDirection;
  if (includePaging) {
    if (params.page != null) query.page = params.page;
    if (params.pageSize != null) query.pageSize = params.pageSize;
  }
  return query;
};

const basePath = "/procurement/vendors";

export const vendorApi = {
  list: (params: VendorListParams = {}) =>
    api.get<VendorListResponse>(basePath, { params: buildParams(params, true) }),
  export: (params: VendorListParams = {}) =>
    api.get<VendorResponse[]>(`${basePath}/export`, { params: buildParams(params, false) }),
  ownerOptions: () => api.get<VendorOwnerOptionResponse[]>(`${basePath}/owner-options`),
  projectOptions: () => api.get<VendorProjectOptionResponse[]>(`${basePath}/project-options`),
  get: (id: number) => api.get<VendorResponse>(`${basePath}/${id}`),
  create: (body: UpsertVendorRequest) => api.post<VendorResponse>(basePath, body),
  update: (id: number, body: UpsertVendorRequest) => api.put<VendorResponse>(`${basePath}/${id}`, body),
  history: (id: number) => api.get<VendorAuditResponse[]>(`${basePath}/${id}/history`),
  uploadDocument: (id: number, documentType: VendorDocumentType, file: File) => {
    const formData = new FormData();
    formData.append("documentType", documentType);
    formData.append("file", file);
    return api.post<VendorDocumentResponse>(`${basePath}/${id}/documents`, formData);
  },
  downloadDocument: (id: number, documentId: number) =>
    api.get<Blob>(`${basePath}/${id}/documents/${documentId}/download`, { responseType: "blob" }),
  deleteDocument: (id: number, documentId: number) =>
    api.delete(`${basePath}/${id}/documents/${documentId}`),
  createEvaluation: (id: number, body: UpsertVendorEvaluationRequest) =>
    api.post<VendorEvaluationResponse>(`${basePath}/${id}/evaluations`, body),
  updateEvaluation: (id: number, evaluationId: number, body: UpsertVendorEvaluationRequest) =>
    api.put<VendorEvaluationResponse>(`${basePath}/${id}/evaluations/${evaluationId}`, body),
  deleteEvaluation: (id: number, evaluationId: number) =>
    api.delete(`${basePath}/${id}/evaluations/${evaluationId}`),
};
