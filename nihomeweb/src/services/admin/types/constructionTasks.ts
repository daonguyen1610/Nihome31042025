export interface ConstructionDrawingResponse {
  ifcReleaseItemId: number;
  ifcReleaseId: number;
  releaseNumber: string;
  releaseDate?: string | null;
  shopDrawingId: number;
  drawingCode: string;
  title: string;
  constructionItem: string;
  revisionNumber: number;
  hasFile: boolean;
  fileName?: string | null;
  contentType?: string | null;
}

export interface ConstructionTaskDrawingsResponse {
  version: string;
  linked: ConstructionDrawingResponse[];
  available: ConstructionDrawingResponse[];
}

export type ConstructionTaskStatus =
  "Planned" | "InProgress" | "Completed" | "Cancelled" | "OnHold" | "WaitingForDepartment";

export interface ConstructionTaskDependencyResponse {
  id: number;
  predecessorTaskId: number;
  predecessorTaskCode?: string | null;
  predecessorTaskName?: string | null;
  predecessorStatus?: ConstructionTaskStatus | null;
}

export interface ConstructionTaskResponse {
  id: number;
  designProjectId: number;
  designProjectCode?: string | null;
  designProjectName?: string | null;
  taskCode: string;
  wbs?: string | null;
  name: string;
  description?: string | null;
  plannedStart: string; // ISO date (yyyy-MM-dd)
  plannedEnd: string;
  actualStart?: string | null;
  actualEnd?: string | null;
  progressPercent: number;
  progressReportedAt?: string | null;
  progressReportedByUserId?: number | null;
  ownerUserId?: number | null;
  ownerName?: string | null;
  projectScheduleNodeId?: string | null;
  status: ConstructionTaskStatus;
  isOverdue: boolean;
  predecessors: ConstructionTaskDependencyResponse[];
  createdAt: string;
  updatedAt: string;
}

export interface ConstructionTaskListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: ConstructionTaskResponse[];
  statusCounts: Partial<Record<ConstructionTaskStatus, number>>;
  overdueCount: number;
}

export interface ConstructionTaskListParams {
  designProjectId?: number;
  ownerUserId?: number;
  status?: string;
  search?: string;
  overdueOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface CreateConstructionTaskRequest {
  designProjectId: number;
  taskCode?: string | null;
  wbs?: string | null;
  name: string;
  description?: string | null;
  plannedStart: string;
  plannedEnd: string;
  ownerUserId?: number | null;
  projectScheduleNodeId?: string | null;
  predecessorTaskIds?: number[];
}

export interface UpdateConstructionTaskRequest {
  wbs?: string | null;
  name: string;
  description?: string | null;
  plannedStart: string;
  plannedEnd: string;
  actualStart?: string | null;
  actualEnd?: string | null;
  progressPercent: number;
  ownerUserId?: number | null;
  projectScheduleNodeId?: string | null;
  status: ConstructionTaskStatus;
}

export interface UpdateConstructionTaskProgressRequest {
  progressPercent: number;
  actualStart?: string | null;
  actualEnd?: string | null;
  status?: ConstructionTaskStatus | null;
}

export interface SetConstructionTaskPredecessorsRequest {
  predecessorTaskIds: number[];
}

export interface BulkDeleteConstructionTasksRequest {
  ids: number[];
}

export interface ConstructionTaskBulkDeleteFailure {
  id: number;
  message: string;
}

export interface ConstructionTaskBulkDeleteResponse {
  requested: number;
  deleted: number;
  failures: ConstructionTaskBulkDeleteFailure[];
}
