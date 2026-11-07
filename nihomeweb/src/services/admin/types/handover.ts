export type HandoverStatus = "Draft" | "ReadyForHandover" | "HandedOver" | "Reopened" | "Cancelled";

export interface HandoverChecklistItem {
  name: string;
  isCompleted: boolean;
  note?: string | null;
}

export interface HandoverReadiness {
  approvedRequiredAsBuiltCategories: number;
  requiredAsBuiltCategories: number;
  unresolvedPunchItems: number;
  approvedAcceptanceRecords: number;
  commissioningCompleted: boolean;
  checklistCompleted: boolean;
  isReady: boolean;
}

export interface HandoverStatusHistory {
  fromStatus?: HandoverStatus | null;
  toStatus: HandoverStatus;
  note?: string | null;
  changedByUserId: number;
  changedByName: string;
  changedAt: string;
}

export interface HandoverRecordResponse {
  id: number;
  designProjectId: number;
  designProjectName: string;
  handoverCode: string;
  title: string;
  description?: string | null;
  plannedHandoverDate: string;
  actualHandoverDate?: string | null;
  location?: string | null;
  responsibleUserId: number;
  responsibleUserName: string;
  commissioningCompleted: boolean;
  commissioningNotes?: string | null;
  checklistItems: HandoverChecklistItem[];
  documents: string[];
  signatories: string[];
  resolutionNote?: string | null;
  status: HandoverStatus;
  readiness: HandoverReadiness;
  statusHistory: HandoverStatusHistory[];
  submittedAt?: string | null;
  submittedByName?: string | null;
  handedOverAt?: string | null;
  handedOverByName?: string | null;
  reopenCount: number;
  createdAt: string;
  createdByUserId: number;
  updatedAt: string;
}

export interface HandoverRecordListResponse {
  items: HandoverRecordResponse[];
  total: number;
  page: number;
  pageSize: number;
  statusCounts: Partial<Record<HandoverStatus, number>>;
  readyCount: number;
}

export interface HandoverRecordListParams {
  designProjectId?: number;
  responsibleUserId?: number;
  status?: HandoverStatus;
  plannedFrom?: string;
  plannedTo?: string;
  search?: string;
  readyOnly?: boolean;
  sortBy?: "date" | "code" | "title" | "project" | "status" | "updatedAt";
  sortDirection?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export interface CreateHandoverRecordRequest {
  designProjectId: number;
  title: string;
  description?: string | null;
  plannedHandoverDate: string;
  location?: string | null;
  responsibleUserId: number;
  commissioningCompleted: boolean;
  commissioningNotes?: string | null;
  checklistItems: HandoverChecklistItem[];
  documents: string[];
  signatories: string[];
}

export type UpdateHandoverRecordRequest = Omit<CreateHandoverRecordRequest, "designProjectId">;

export interface TransitionHandoverStatusRequest {
  status: HandoverStatus;
  note?: string | null;
}
