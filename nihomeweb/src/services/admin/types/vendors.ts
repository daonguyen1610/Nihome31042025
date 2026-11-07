export type VendorType = "Supplier" | "SubContractor" | "Both";

export interface VendorResponse {
  id: number;
  vendorCode: string;
  companyName: string;
  vendorType: VendorType;
  taxCode?: string;
  phone?: string;
  email?: string;
  address?: string;
  contactPerson?: string;
  licenseNo?: string;
  tradeCategory?: string;
  capabilityFileUrl?: string;
  driveFolder?: string;
  isActive: boolean;
  createdByUserId: number;
  createdByName?: string;
  createdAt: string;
  updatedByUserId?: number;
  updatedByName?: string;
  updatedAt: string;
  rowVersion: string;
  contracts: VendorContractSummary[];
  ratings: VendorRatingSummary[];
  history: VendorHistorySummary[];
}

export interface VendorContractSummary {
  id: number;
  contractNumber: string;
  status: string;
  operationalProjectId?: number | null;
  operationalProjectCode?: string | null;
  operationalProjectName?: string | null;
}

export interface VendorRatingSummary {
  id: number;
  contractId: number;
  contractNumber: string;
  operationalProjectId: number;
  operationalProjectCode: string;
  status: string;
  overallScore: number;
  approvedAt?: string | null;
}

export interface VendorHistorySummary {
  id: number;
  action: string;
  message: string;
  actorUserId?: number | null;
  actorName?: string | null;
  createdAt: string;
}

export interface VendorListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: VendorResponse[];
}

export interface CreateVendorRequest {
  vendorCode: string;
  companyName: string;
  vendorType: VendorType;
  taxCode?: string;
  phone?: string;
  email?: string;
  address?: string;
  contactPerson?: string;
  licenseNo?: string;
  tradeCategory?: string;
  capabilityFileUrl?: string;
  capabilityUploadToken?: string;
  driveFolder?: string;
}

export interface UpdateVendorRequest extends CreateVendorRequest {
  isActive: boolean;
  rowVersion?: string;
}

export interface VendorListParams {
  vendorType?: VendorType;
  isActive?: boolean;
  search?: string;
  sortBy?: "vendorCode" | "companyName" | "vendorType" | "updatedAt";
  sortDirection?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export interface BusinessDocumentUploadResponse {
  path: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
  claimToken?: string;
}

export type PermitDocumentKind = "SubmittedPackage" | "IssuedPermit";
