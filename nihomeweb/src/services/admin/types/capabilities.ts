export type CapabilityDocumentExpiryState = "none" | "expired" | "critical" | "warning" | "ok";

export const CAPABILITY_DOCUMENT_EXPIRY_STATES: CapabilityDocumentExpiryState[] = [
  "none",
  "expired",
  "critical",
  "warning",
  "ok",
];

export interface CapabilityDocumentVersionResponse {
  id: number;
  versionNumber: number;
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
  uploadedByUserId?: number | null;
  uploadedByName?: string | null;
  createdAt: string;
}

export interface CapabilityDocumentResponse {
  id: number;
  name: string;
  tagCode: string;
  tagLabel?: string | null;
  issuedDate?: string | null;
  expiryDate?: string | null;
  description?: string | null;
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
  currentVersion: number;
  expiryState: CapabilityDocumentExpiryState;
  uploadedByUserId?: number | null;
  uploadedByName?: string | null;
  createdAt: string;
  updatedAt: string;
  previousVersionCount: number;
}

export interface CapabilityDocumentDetailResponse extends CapabilityDocumentResponse {
  versions: CapabilityDocumentVersionResponse[];
}

export interface CapabilityDocumentListResponse {
  items: CapabilityDocumentResponse[];
  total: number;
  page: number;
  pageSize: number;
}

export interface UpsertCapabilityDocumentRequest {
  name: string;
  tagCode: string;
  issuedDate?: string | null;
  expiryDate?: string | null;
  description?: string | null;
  filePath?: string;
  originalFileName?: string;
  fileSize?: number;
  contentType?: string;
}

export interface ReplaceCapabilityDocumentFileRequest {
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
}

export interface CapabilityDocumentUploadResponse {
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
}

export interface CapabilityDocumentListParams {
  tagCode?: string;
  issuedYear?: number;
  search?: string;
  expiryState?: CapabilityDocumentExpiryState;
  page?: number;
  pageSize?: number;
}
