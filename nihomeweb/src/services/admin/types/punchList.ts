export type PunchStatus = "Open" | "InProgress" | "Fixed" | "Verified" | "Cancelled";

export type PunchSeverity = "Low" | "Medium" | "High" | "Critical";

export type PunchRootCause =
  "Unclassified" | "Design" | "Construction" | "Material" | "ClientChange" | "Other";

export interface PunchItemResponse {
  id: number;
  designProjectId: number;
  designProjectCode?: string | null;
  designProjectName?: string | null;
  punchCode: string;
  title: string;
  description?: string | null;
  location?: string | null;
  severity: PunchSeverity;
  status: PunchStatus;
  rootCause: PunchRootCause;
  responsibleDesignUserId?: number | null;
  responsibleDesignUserName?: string | null;
  rootCauseNote?: string | null;
  rootCauseConfirmedAt?: string | null;
  rootCauseConfirmedByUserId?: number | null;
  rootCauseConfirmedByName?: string | null;
  assigneeUserId?: number | null;
  assigneeName?: string | null;
  deadline?: string | null;
  resolutionNote?: string | null;
  reopenCount: number;
  verifiedAt?: string | null;
  verifiedByUserId?: number | null;
  verifiedByName?: string | null;
  note?: string | null;
  isOverdue: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface PunchItemListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: PunchItemResponse[];
  statusCounts: Partial<Record<PunchStatus, number>>;
  overdueCount: number;
}

export interface PunchItemListParams {
  designProjectId?: number;
  assigneeUserId?: number;
  status?: string;
  severity?: string;
  openOnly?: boolean;
  overdueOnly?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CreatePunchItemRequest {
  designProjectId: number;
  punchCode?: string | null;
  title: string;
  description?: string | null;
  location?: string | null;
  severity: PunchSeverity;
  assigneeUserId?: number | null;
  deadline?: string | null;
  note?: string | null;
}

export interface UpdatePunchItemRequest {
  title: string;
  description?: string | null;
  location?: string | null;
  severity: PunchSeverity;
  assigneeUserId?: number | null;
  deadline?: string | null;
  resolutionNote?: string | null;
  note?: string | null;
}

export interface TransitionPunchStatusRequest {
  status: PunchStatus;
  resolutionNote?: string | null;
  rootCause?: PunchRootCause | null;
  responsibleDesignUserId?: number | null;
  rootCauseNote?: string | null;
}

export interface BulkDeletePunchItemsRequest {
  ids: number[];
}

export interface PunchItemBulkDeleteFailure {
  id: number;
  message: string;
}

export interface PunchItemBulkDeleteResponse {
  requested: number;
  deleted: number;
  failures: PunchItemBulkDeleteFailure[];
}
