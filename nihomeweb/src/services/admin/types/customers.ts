export type CustomerType = "Individual" | "Company";

export type CustomerRelationshipStatus = "Prospect" | "InProgress" | "Signed" | "Suspended";

export type CustomerActivityType =
  | "Call"
  | "Email"
  | "Meeting"
  | "Note"
  | "LegalRepresentativeAssigned"
  | "LegalRepresentativeCleared";

export interface CustomerContactResponse {
  id: number;
  fullName: string;
  position?: string;
  phone?: string;
  email?: string;
  isPrimary: boolean;
  isLegalRepresentative: boolean;
  legalRepresentativeSince?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CustomerActivityResponse {
  id: number;
  type: CustomerActivityType;
  occurredAt: string;
  content: string;
  createdByUserId: number;
  createdByName?: string;
  createdAt: string;
}

export type ContractCareActivityType = Extract<
  CustomerActivityType,
  "Call" | "Email" | "Meeting" | "Note"
>;

export interface ContractCareActivityResponse extends CustomerActivityResponse {
  contractId: number;
}

export interface ContractCareActivityListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: ContractCareActivityResponse[];
}

export interface CustomerDocumentResponse {
  id: number;
  customerId: number;
  filePath: string;
  originalFileName: string;
  fileSize: number;
  contentType: string;
  label?: string;
  createdAt: string;
  uploadedByUserId?: number;
  uploadedByName?: string;
}

export interface CustomerResponse {
  id: number;
  type: CustomerType;
  name: string;
  taxId?: string;
  address?: string;
  representativeName?: string;
  legalRepresentativeContactId?: number | null;
  sourceCode: string;
  relationshipStatus: CustomerRelationshipStatus;
  ownerUserId?: number;
  ownerName?: string;
  note?: string;
  createdAt: string;
  updatedAt: string;
  rowVersion: string;
  contacts: CustomerContactResponse[];
  activities: CustomerActivityResponse[];
}

export interface CustomerListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: CustomerResponse[];
}

export interface UpsertCustomerContactRequest {
  id?: number;
  fullName: string;
  position?: string;
  phone?: string;
  email?: string;
  isPrimary?: boolean;
  isLegalRepresentative?: boolean;
}

export interface CreateCustomerRequest {
  type: CustomerType;
  name: string;
  taxId?: string;
  address?: string;
  representativeName?: string;
  sourceCode: string;
  ownerUserId?: number | null;
  note?: string;
  primaryContact: UpsertCustomerContactRequest;
  duplicateOverrideReason?: string;
}

export interface UpdateCustomerRequest {
  rowVersion?: string;
  type: CustomerType;
  name: string;
  taxId?: string;
  address?: string;
  representativeName?: string;
  sourceCode: string;
  relationshipStatus: CustomerRelationshipStatus;
  ownerUserId?: number | null;
  note?: string;
  duplicateOverrideReason?: string;
}

export interface CreateCustomerActivityRequest {
  type: CustomerActivityType;
  content: string;
  occurredAt?: string;
}

export interface CustomerDuplicateDetail {
  field: "TaxId" | "Phone";
  value: string;
  existingCustomerId: number;
  existingCustomerName: string;
  message: string;
}

export interface CustomerListParams {
  type?: CustomerType;
  status?: CustomerRelationshipStatus;
  ownerUserId?: number;
  sourceCode?: string;
  search?: string;
  createdFrom?: string;
  createdTo?: string;
  page?: number;
  pageSize?: number;
}
