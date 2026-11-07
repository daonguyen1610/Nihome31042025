export type IfcReleaseStatus = "Draft" | "Released" | "Cancelled";

export interface IfcReleaseItemResponse {
  id: number;
  shopDrawingId: number;
  drawingCode?: string | null;
  title?: string | null;
  disciplineCode?: string | null;
  disciplineLabel?: string | null;
  status?: string | null;
}

export interface IfcReleaseRecipientResponse {
  id: number;
  name: string;
  recipientTypeCode: string;
  recipientTypeLabel?: string | null;
  acknowledgedAt?: string | null;
  isAcknowledged: boolean;
  acknowledgementNote?: string | null;
}

export interface IfcReleaseResponse {
  id: number;
  designProjectId: number;
  designProjectCode?: string | null;
  releaseNumber: string;
  title: string;
  releaseDate?: string | null;
  issuedByUserId?: number | null;
  issuedByName?: string | null;
  status: IfcReleaseStatus;
  note?: string | null;
  items: IfcReleaseItemResponse[];
  recipients: IfcReleaseRecipientResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface IfcReleaseListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: IfcReleaseResponse[];
  statusCounts: Partial<Record<IfcReleaseStatus, number>>;
}

export interface IfcReleaseListParams {
  designProjectId?: number;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateIfcReleaseRequest {
  designProjectId: number;
  title: string;
  note?: string | null;
}

export interface UpdateIfcReleaseRequest {
  title: string;
  note?: string | null;
}

export interface AddIfcReleaseItemsRequest {
  shopDrawingIds: number[];
}

export interface AddIfcReleaseRecipientRequest {
  name: string;
  recipientTypeCode: string;
}

export interface AcknowledgeIfcReleaseRecipientRequest {
  acknowledgementNote?: string | null;
}
