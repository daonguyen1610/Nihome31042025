export type LeadStatus =
  "New" | "Contacted" | "Interested" | "NotInterested" | "Converted" | "Junk";

export type LeadActivityType = "Call" | "Email" | "Meeting" | "Note";

export interface LeadActivityResponse {
  id: number;
  type: LeadActivityType;
  content: string;
  createdByUserId: number;
  createdByName?: string;
  createdAt: string;
}

export interface LeadResponse {
  id: number;
  name: string;
  companyName?: string;
  phone?: string;
  email?: string;
  sourceCode: string;
  segmentCode: string;
  status: LeadStatus;
  ownerUserId?: number;
  ownerName?: string;
  note?: string;
  convertedAt?: string;
  convertedCustomerId?: number;
  convertedOpportunityId?: number;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  activities: LeadActivityResponse[];
}

export interface LeadListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: LeadResponse[];
}

export interface CreateLeadRequest {
  name: string;
  companyName?: string;
  phone?: string;
  email?: string;
  sourceCode: string;
  segmentCode: string;
  ownerUserId?: number | null;
  note?: string;
}

export interface UpdateLeadRequest {
  rowVersion?: string;
  name: string;
  companyName?: string;
  phone?: string;
  email?: string;
  sourceCode: string;
  segmentCode: string;
  status: LeadStatus;
  ownerUserId?: number | null;
  note?: string;
}

export interface ConvertLeadRequest {
  rowVersion?: string;
  customerId?: number | null;
  opportunityId?: number | null;
  /** Required when the lead has a company name and no customerId is supplied. */
  taxId?: string | null;
  address?: string | null;
  representativeName?: string | null;
  note?: string;
}

export type UnconvertOutcome = "DeletedBoth" | "DeletedOpportunity" | "UnlinkedOnly";

export interface UnconvertLeadResponse {
  outcome: UnconvertOutcome;
  keptCustomerId: number | null;
  keptOpportunityId: number | null;
  lead: LeadResponse;
}

export interface CreateLeadActivityRequest {
  type: LeadActivityType;
  content: string;
}

export interface LeadListParams {
  status?: LeadStatus;
  sourceCode?: string;
  segmentCode?: string;
  ownerUserId?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface MasterDataOption {
  id: number;
  category: string;
  code: string;
  name: string;
  labelKey?: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface MasterDataCategory {
  category: string;
  totalCount: number;
  activeCount: number;
}

export interface UpsertMasterDataOptionRequest {
  code: string;
  name: string;
  labelKey?: string | null;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
}
