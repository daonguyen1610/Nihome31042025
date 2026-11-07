export type AsBuiltStatus = "Draft" | "Submitted" | "Approved" | "Archived" | "Cancelled";

export type AsBuiltCategory =
  "Drawing" | "AcceptanceMinute" | "TestReport" | "WarrantyCertificate" | "Other" | string;

export interface AsBuiltDocumentCategoryResponse {
  id: number;
  code: string;
  name: string;
  nameVi: string;
  nameEn: string;
  nameZh: string;
  nameJa: string;
  isRequired: boolean;
  isActive: boolean;
  sortOrder: number;
  documentCount: number;
}

export interface UpsertAsBuiltDocumentCategoryRequest {
  code: string;
  name: string;
  isRequired: boolean;
  isActive: boolean;
  sortOrder: number;
}

export interface AsBuiltDocumentResponse {
  id: number;
  designProjectId: number;
  designProjectName: string;
  documentCode: string;
  title: string;
  categoryId: number;
  category: AsBuiltCategory;
  categoryName: string;
  description?: string | null;
  fileUrl?: string | null;
  status: AsBuiltStatus;
  note?: string | null;
  submittedAt?: string | null;
  submittedByUserId?: number | null;
  submittedByName?: string | null;
  approvedAt?: string | null;
  approvedByUserId?: number | null;
  approvedByName?: string | null;
  archivedAt?: string | null;
  createdAt: string;
  createdByUserId?: number | null;
  updatedAt: string;
  updatedByUserId?: number | null;
}

export interface AsBuiltDocumentListResponse {
  items: AsBuiltDocumentResponse[];
  total: number;
  page: number;
  pageSize: number;
  statusCounts: Partial<Record<AsBuiltStatus, number>>;
  categoryCounts: Partial<Record<AsBuiltCategory, number>>;
  completedRequiredCategories: number;
  totalRequiredCategories: number;
}

export interface AsBuiltDocumentListParams {
  designProjectId?: number;
  category?: string;
  status?: string;
  search?: string;
  openOnly?: boolean;
  sortBy?: "category" | "code" | "title" | "project" | "status" | "updatedAt";
  sortDirection?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export interface CreateAsBuiltDocumentRequest {
  designProjectId: number;
  title: string;
  category: AsBuiltCategory;
  description?: string | null;
  fileUrl?: string | null;
  note?: string | null;
}

export interface UpdateAsBuiltDocumentRequest {
  title: string;
  category: AsBuiltCategory;
  description?: string | null;
  fileUrl?: string | null;
  note?: string | null;
}

export interface TransitionAsBuiltStatusRequest {
  status: AsBuiltStatus;
  note?: string | null;
}

export interface BulkDeleteAsBuiltDocumentsRequest {
  ids: number[];
}

export interface AsBuiltDocumentBulkDeleteResponse {
  deletedIds: number[];
  skippedIds: number[];
}
