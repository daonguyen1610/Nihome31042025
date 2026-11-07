import api from "@/lib/api";
import { withIfMatch } from "@/lib/api";
import { withIdempotencyKey } from "@/lib/api";
import type {
  LeadListParams,
  LeadListResponse,
  LeadResponse,
  CreateLeadRequest,
  UpdateLeadRequest,
  ConvertLeadRequest,
  UnconvertLeadResponse,
  CreateLeadActivityRequest,
  LeadActivityResponse,
} from "./types/leads";
import type {
  DeletionImpactResponse,
  ConfirmDeletionRequest,
  HardDeleteOperationResult,
} from "./types/operationalProjects";
import type {
  CustomerListParams,
  CustomerListResponse,
  CustomerResponse,
  ContractCareActivityListResponse,
  ContractCareActivityType,
  ContractCareActivityResponse,
  CreateCustomerRequest,
  UpdateCustomerRequest,
  UpsertCustomerContactRequest,
  CustomerContactResponse,
  CreateCustomerActivityRequest,
  CustomerActivityResponse,
  CustomerDocumentResponse,
} from "./types/customers";
import type {
  VendorListParams,
  VendorListResponse,
  VendorResponse,
  CreateVendorRequest,
  UpdateVendorRequest,
} from "./types/vendors";
import type {
  OpportunityListParams,
  OpportunityListResponse,
  OpportunityPipelineParams,
  OpportunityPipelineResponse,
  OpportunityResponse,
  OpportunityOwnerOption,
  OpportunityHistoryItem,
  CreateOpportunityRequest,
  UpdateOpportunityRequest,
  ChangeOpportunityStageRequest,
  OpportunityDesignStartResponse,
  AddOpportunityActivityRequest,
  OpportunityActivityResponse,
} from "./types/contractDocuments";
import { uploadBusinessDocument } from "./helpers";

export const crmApi = {
  listLeads: (params: LeadListParams = {}) => {
    const query = new URLSearchParams();
    if (params.status) query.append("status", params.status);
    if (params.sourceCode) query.append("sourceCode", params.sourceCode);
    if (params.segmentCode) query.append("segmentCode", params.segmentCode);
    if (params.ownerUserId != null) query.append("ownerUserId", String(params.ownerUserId));
    if (params.search) query.append("search", params.search);
    if (params.page) query.append("page", String(params.page));
    if (params.pageSize) query.append("pageSize", String(params.pageSize));
    const qs = query.toString();
    return api.get<LeadListResponse>(`/leads${qs ? `?${qs}` : ""}`);
  },
  getLead: (id: number) => api.get<LeadResponse>(`/leads/${id}`),
  createLead: (body: CreateLeadRequest) => api.post<LeadResponse>("/leads", body),
  updateLead: (id: number, body: UpdateLeadRequest) => api.put<LeadResponse>(`/leads/${id}`, body),
  getLeadDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/leads/${id}/deletion-impact`),
  deleteLead: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult | null>(`/leads/${id}`, {
      ...withIfMatch(body.rowVersion),
      data: body,
    }),
  convertLead: (id: number, body: ConvertLeadRequest = {}) =>
    api.post<LeadResponse>(`/leads/${id}/convert`, body),
  unconvertLead: (id: number, rowVersion?: string) =>
    api.post<UnconvertLeadResponse>(`/leads/${id}/unconvert`, {}, withIfMatch(rowVersion)),
  addLeadActivity: (id: number, body: CreateLeadActivityRequest) =>
    api.post<LeadActivityResponse>(`/leads/${id}/activities`, body),
  listCustomers: (params: CustomerListParams = {}) => {
    const q = new URLSearchParams();
    if (params.type) q.append("type", params.type);
    if (params.status) q.append("status", params.status);
    if (params.ownerUserId != null) q.append("ownerUserId", String(params.ownerUserId));
    if (params.sourceCode) q.append("sourceCode", params.sourceCode);
    if (params.search) q.append("search", params.search);
    if (params.createdFrom) q.append("createdFrom", params.createdFrom);
    if (params.createdTo) q.append("createdTo", params.createdTo);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<CustomerListResponse>(`/customers${qs ? `?${qs}` : ""}`);
  },
  getCustomer: (id: number) => api.get<CustomerResponse>(`/customers/${id}`),
  listContractCareActivities: (id: number, page = 1) =>
    api.get<ContractCareActivityListResponse>(`/contracts/${id}/care-activities`, {
      params: { page, pageSize: 20 },
    }),
  addContractCareActivity: (
    id: number,
    body: { type: ContractCareActivityType; content: string },
    idempotencyKey: string,
  ) =>
    api.post<ContractCareActivityResponse>(
      `/contracts/${id}/care-activities`,
      body,
      withIdempotencyKey(idempotencyKey),
    ),
  createCustomer: (body: CreateCustomerRequest) => api.post<CustomerResponse>("/customers", body),
  updateCustomer: (id: number, body: UpdateCustomerRequest) =>
    api.put<CustomerResponse>(`/customers/${id}`, body),
  getCustomerDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/customers/${id}/deletion-impact`),
  deleteCustomer: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult | null>(`/customers/${id}`, {
      ...withIfMatch(body.rowVersion),
      data: body,
    }),
  upsertCustomerContact: (id: number, body: UpsertCustomerContactRequest) =>
    api.post<CustomerContactResponse>(`/customers/${id}/contacts`, body),
  deleteCustomerContact: (id: number, contactId: number) =>
    api.delete(`/customers/${id}/contacts/${contactId}`),
  addCustomerActivity: (id: number, body: CreateCustomerActivityRequest) =>
    api.post<CustomerActivityResponse>(`/customers/${id}/activities`, body),
  listCustomerDocuments: (id: number) =>
    api.get<CustomerDocumentResponse[]>(`/customers/${id}/documents`),
  getCustomerDocumentContent: (id: number, documentId: number) =>
    api.get<Blob>(`/customers/${id}/documents/${documentId}/content`, {
      responseType: "blob",
    }),
  uploadCustomerDocument: (id: number, file: File, label?: string) => {
    const formData = new FormData();
    formData.append("file", file);
    if (label?.trim()) formData.append("label", label.trim());
    return api.post<CustomerDocumentResponse>(`/customers/${id}/documents`, formData);
  },
  deleteCustomerDocument: (id: number, documentId: number) =>
    api.delete(`/customers/${id}/documents/${documentId}`),
  listVendors: (params: VendorListParams = {}) => {
    const q = new URLSearchParams();
    if (params.vendorType) q.append("vendorType", params.vendorType);
    if (params.isActive != null) q.append("isActive", String(params.isActive));
    if (params.search) q.append("search", params.search);
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortDirection) q.append("sortDirection", params.sortDirection);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    const qs = q.toString();
    return api.get<VendorListResponse>(`/vendors${qs ? `?${qs}` : ""}`);
  },
  getVendor: (id: number) => api.get<VendorResponse>(`/vendors/${id}`),
  createVendor: (body: CreateVendorRequest) => api.post<VendorResponse>("/vendors", body),
  updateVendor: (id: number, body: UpdateVendorRequest) =>
    api.put<VendorResponse>(`/vendors/${id}`, body),
  getVendorDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/vendors/${id}/deletion-impact`),
  deleteVendor: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult | null>(`/vendors/${id}`, {
      ...withIfMatch(body.rowVersion),
      data: body,
    }),
  uploadVendorDocument: (file: File) => uploadBusinessDocument("vendors", file),
  discardVendorDocument: (claimToken: string) =>
    api.delete("/business-documents/vendors", { data: { claimToken } }),
  listOpportunities: (params: OpportunityListParams = {}) => {
    const q = new URLSearchParams();
    if (params.stage) q.append("stage", params.stage);
    if (params.customerId != null) q.append("customerId", String(params.customerId));
    if (params.ownerUserId != null) q.append("ownerUserId", String(params.ownerUserId));
    if (params.expectedCloseFrom) q.append("expectedCloseFrom", params.expectedCloseFrom);
    if (params.expectedCloseTo) q.append("expectedCloseTo", params.expectedCloseTo);
    if (params.minValue != null) q.append("minValue", String(params.minValue));
    if (params.maxValue != null) q.append("maxValue", String(params.maxValue));
    if (params.search) q.append("search", params.search);
    if (params.page) q.append("page", String(params.page));
    if (params.pageSize) q.append("pageSize", String(params.pageSize));
    if (params.quoteEligibleOnly != null)
      q.append("quoteEligibleOnly", String(params.quoteEligibleOnly));
    const qs = q.toString();
    return api.get<OpportunityListResponse>(`/opportunities${qs ? `?${qs}` : ""}`);
  },
  getOpportunityPipeline: (params: OpportunityPipelineParams = {}) => {
    const q = new URLSearchParams();
    if (params.ownerUserId != null) q.append("ownerUserId", String(params.ownerUserId));
    if (params.customerId != null) q.append("customerId", String(params.customerId));
    if (params.expectedCloseFrom) q.append("expectedCloseFrom", params.expectedCloseFrom);
    if (params.expectedCloseTo) q.append("expectedCloseTo", params.expectedCloseTo);
    if (params.minValue != null) q.append("minValue", String(params.minValue));
    if (params.maxValue != null) q.append("maxValue", String(params.maxValue));
    const qs = q.toString();
    return api.get<OpportunityPipelineResponse>(`/opportunities/pipeline${qs ? `?${qs}` : ""}`);
  },
  getOpportunity: (id: number) => api.get<OpportunityResponse>(`/opportunities/${id}`),
  listOpportunityOwnerOptions: () =>
    api.get<OpportunityOwnerOption[]>("/opportunities/owner-options"),
  getOpportunityHistory: (id: number) =>
    api.get<OpportunityHistoryItem[]>(`/opportunities/${id}/history`),
  createOpportunity: (body: CreateOpportunityRequest, key?: string) =>
    api.post<OpportunityResponse>("/opportunities", body, withIdempotencyKey(key)),
  updateOpportunity: (id: number, body: UpdateOpportunityRequest) =>
    api.put<OpportunityResponse>(`/opportunities/${id}`, body),
  changeOpportunityStage: (id: number, body: ChangeOpportunityStageRequest) =>
    api.patch<OpportunityResponse>(`/opportunities/${id}/stage`, body),
  startOpportunityDesign: (id: number, body: { name: string; note?: string | null }) =>
    api.post<OpportunityDesignStartResponse>(`/opportunities/${id}/design-project`, body),
  getOpportunityDeletionImpact: (id: number) =>
    api.get<DeletionImpactResponse>(`/opportunities/${id}/deletion-impact`),
  deleteOpportunity: (id: number, body: ConfirmDeletionRequest) =>
    api.delete<HardDeleteOperationResult>(`/opportunities/${id}`, { data: body }),
  addOpportunityActivity: (id: number, body: AddOpportunityActivityRequest) =>
    api.post<OpportunityActivityResponse>(`/opportunities/${id}/activities`, body),
};
