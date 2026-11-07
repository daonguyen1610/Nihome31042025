export type DesignProjectStage = "Concept" | "BasicDesign" | "ShopDrawing" | "Completed";

export type DesignProjectStatus = "Active" | "OnHold" | "Completed" | "Cancelled";

export const DESIGN_PROJECT_STAGES: DesignProjectStage[] = [
  "Concept",
  "BasicDesign",
  "ShopDrawing",
  "Completed",
];

export const DESIGN_PROJECT_STATUSES: DesignProjectStatus[] = [
  "Active",
  "OnHold",
  "Completed",
  "Cancelled",
];

export interface DesignProjectResponse {
  id: number;
  projectCode: string;
  name: string;
  customerId: number;
  customerName?: string | null;
  operationalProjectId?: number | null;
  contractId?: number | null;
  contractNumber?: string | null;
  designManagerUserId?: number | null;
  designManagerName?: string | null;
  projectManagerUserId?: number | null;
  projectManagerName?: string | null;
  designLeadUserId?: number | null;
  designLeadName?: string | null;
  startDate?: string | null;
  deadline?: string | null;
  currentStage: DesignProjectStage;
  status: DesignProjectStatus;
  note?: string | null;
  createdAt: string;
  updatedAt: string;
  effectivePermissions?: DesignProjectMemberPermissionsResponse | null;
}

export interface DesignProjectListItemResponse {
  id: number;
  projectCode: string;
  name: string;
  customerId: number;
  customerName?: string | null;
  operationalProjectId?: number | null;
  contractId?: number | null;
  contractNumber?: string | null;
  designManagerUserId?: number | null;
  designManagerName?: string | null;
  projectManagerUserId?: number | null;
  projectManagerName?: string | null;
  designLeadUserId?: number | null;
  designLeadName?: string | null;
  startDate?: string | null;
  deadline?: string | null;
  currentStage: DesignProjectStage;
  status: DesignProjectStatus;
  updatedAt: string;
}

export interface DesignProjectListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: DesignProjectListItemResponse[];
}

export interface DesignProjectListParams {
  customerId?: number;
  contractId?: number;
  designManagerUserId?: number;
  projectManagerUserId?: number;
  designLeadUserId?: number;
  /** Comma-separated `DesignProjectStage` values. */
  stage?: string;
  /** Comma-separated `DesignProjectStatus` values. */
  status?: string;
  deadlineFrom?: string;
  deadlineTo?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateDesignProjectRequest {
  name: string;
  customerId: number;
  operationalProjectId?: number | null;
  contractId?: number | null;
  designManagerUserId?: number | null;
  projectManagerUserId?: number | null;
  designLeadUserId?: number | null;
  startDate?: string | null;
  deadline?: string | null;
  note?: string | null;
}

export interface UpdateDesignProjectRequest extends CreateDesignProjectRequest {
  status?: DesignProjectStatus;
}

export type DesignSchedulePhaseCode = "Concept" | "BasicDesign" | "ShopDrawing";

export type DesignScheduleStatus =
  "NotStarted" | "InProgress" | "Completed" | "OnHold" | "WaitingForDepartment";

export const DESIGN_SCHEDULE_PHASE_CODES: DesignSchedulePhaseCode[] = [
  "Concept",
  "BasicDesign",
  "ShopDrawing",
];

export const DESIGN_SCHEDULE_STATUSES: DesignScheduleStatus[] = [
  "NotStarted",
  "InProgress",
  "Completed",
  "OnHold",
  "WaitingForDepartment",
];

export interface InitializeDesignSchedulePhaseRequest {
  code: DesignSchedulePhaseCode;
  weight: number;
}

export interface InitializeDesignScheduleRequest {
  designProjectId?: number;
  phases: InitializeDesignSchedulePhaseRequest[];
}

export interface UpsertDesignSchedulePhaseRequest {
  plannedStart: string;
  plannedEnd: string;
  actualStart?: string | null;
  actualEnd?: string | null;
  status: DesignScheduleStatus;
  progressPercent: number;
  weight: number;
  rowVersion?: string;
}

export interface UpsertDesignScheduleTaskRequest extends UpsertDesignSchedulePhaseRequest {
  code: string;
  name: string;
  description?: string | null;
  departmentCode: string;
  disciplineCode?: string | null;
  assigneeMemberId: number;
  approverMemberId: number;
  parentTaskId?: number | null;
  projectScheduleNodeId?: string | null;
  resourceSummary?: string | null;
  vendorId?: number | null;
  downstreamContractId?: number | null;
  isCritical: boolean;
  isMilestone: boolean;
  predecessorTaskIds: number[];
  ccMemberIds: number[];
}

export interface DesignScheduleQuery {
  designProjectId?: number;
  phase?: DesignSchedulePhaseCode;
  assigneeMemberId?: number;
  departmentCode?: string;
  disciplineCode?: string;
  status?: DesignScheduleStatus;
  plannedFrom?: string;
  plannedTo?: string;
  overdueOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export interface DesignSchedulePhaseResponse {
  id: number;
  code: DesignSchedulePhaseCode;
  plannedStart: string;
  plannedEnd: string;
  actualStart?: string | null;
  actualEnd?: string | null;
  status: DesignScheduleStatus;
  progressPercent: number;
  weight: number;
  overdue: boolean;
  baselineReady: boolean;
  rolledUpProgressPercent?: number | null;
  rowVersion: string;
}

export interface DesignScheduleTaskResponse {
  id: number;
  phaseId: number;
  phaseCode: DesignSchedulePhaseCode;
  code: string;
  name: string;
  description?: string | null;
  departmentCode: string;
  disciplineCode?: string | null;
  assigneeMemberId: number;
  approverMemberId: number;
  parentTaskId?: number | null;
  projectScheduleNodeId?: string | null;
  resourceSummary?: string | null;
  vendorId?: number | null;
  vendorName?: string | null;
  downstreamContractId?: number | null;
  downstreamContractNumber?: string | null;
  isCritical: boolean;
  isMilestone: boolean;
  plannedStart: string;
  plannedEnd: string;
  actualStart?: string | null;
  actualEnd?: string | null;
  status: DesignScheduleStatus;
  progressPercent: number;
  weight: number;
  overdue: boolean;
  predecessorTaskIds: number[];
  successorTaskIds: number[];
  ccMemberIds: number[];
  approvalStatus: "NotSubmitted" | "Pending" | "Approved" | "Rejected";
  submittedAt?: string | null;
  submittedByUserId?: number | null;
  reviewedAt?: string | null;
  reviewedByUserId?: number | null;
  reviewComment?: string | null;
  rowVersion: string;
}

export interface DesignScheduleOutsourceOptionResponse {
  vendorId: number;
  vendorCode: string;
  vendorName: string;
  contractId: number;
  contractNumber: string;
  contractType: "Design" | "Subcontract";
}

export type DesignReviewStage = "Concept" | "BasicDesign" | "ShopDrawing";

export type DesignReviewStatus =
  | "Draft"
  | "InternalReview"
  | "ReadyForClient"
  | "SubmittedToClient"
  | "ClientChangesRequested"
  | "ClientApproved"
  | "Rejected";

export interface DesignReviewResponse {
  id: number;
  designProjectId: number;
  stage: DesignReviewStage;
  revisionNumber: number;
  conceptRevisionId?: number | null;
  conceptOptionId?: number | null;
  clientAcceptanceDocumentId?: number | null;
  clientAcceptanceSignedBy?: string | null;
  clientAcceptanceSignedAt?: string | null;
  title: string;
  status: DesignReviewStatus;
  designManagerUserId: number;
  designManagerName?: string | null;
  internalSummary?: string | null;
  internalSubmittedAt?: string | null;
  clientSubmittedAt?: string | null;
  clientDecisionNote?: string | null;
  clientDecidedAt?: string | null;
  canManage: boolean;
  canComment: boolean;
  recipients: {
    userId: number;
    name: string;
    roleCode?: string;
    roleName?: string;
    roleLabelKey?: string | null;
    departmentId?: number | null;
    departmentLabelKey?: string | null;
  }[];
  comments: {
    id: number;
    projectDocumentId?: number | null;
    authorUserId: number;
    authorName: string;
    body: string;
    createdAt: string;
  }[];
  projectDocumentIds: number[];
  documents: {
    projectDocumentId: number;
    originalFileName: string;
    sourceEntityType: string;
    sourceRecordId: number;
    sha256: string;
  }[];
  rowVersion: string;
}

export interface DesignScheduleTaskRollupSourceResponse {
  taskId: number;
  weight: number;
  progressPercent: number;
  weightedValue: number;
}

export interface DesignSchedulePhaseRollupResponse {
  phaseId: number;
  weight: number;
  baselineReady: boolean;
  progressPercent?: number | null;
  weightedValue?: number | null;
  taskSources: DesignScheduleTaskRollupSourceResponse[];
}

export interface PagedDesignScheduleTasksResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  items: DesignScheduleTaskResponse[];
}

export interface DesignScheduleDepartmentResponse {
  code: string;
  labelKey: string;
}

export interface DesignScheduleResponse {
  operationalProjectId: number;
  designProjectId: number;
  canManage: boolean;
  baselineReady: boolean;
  progressPercent?: number | null;
  rollupPolicyVersion: string;
  phases: DesignSchedulePhaseResponse[];
  departments: DesignScheduleDepartmentResponse[];
  rollupSources: DesignSchedulePhaseRollupResponse[];
  tasks: PagedDesignScheduleTasksResponse;
}

export type ProjectAssignmentStatus =
  "Planned" | "InProgress" | "Completed" | "Cancelled" | "Reassigned";

export interface ProjectRoleDefinitionResponse {
  code: string;
  raci: string;
  canManageTeam: boolean;
  canApproveDesign: boolean;
  allowedScopes: string[];
}

export interface ProjectMemberRoleResponse {
  roleCode: string;
  scope: string;
  scopeValue?: string | null;
  startedAt: string;
  endedAt?: string | null;
}

export interface OperationalProjectMemberResponse {
  roleCode: string;
  roleName: string;
  roleLabelKey?: string | null;
  departmentId?: number | null;
  departmentLabelKey?: string | null;
  designProjectId?: number | null;
  id: number;
  userId: number;
  userName: string;
  email: string;
  position: string;
  reportsToMemberId?: number | null;
  reportsToName?: string | null;
  startedAt: string;
  endedAt?: string | null;
  isActive: boolean;
  isUserActive?: boolean;
  source: string;
  sourceReference?: string | null;
  roles: ProjectMemberRoleResponse[];
  designPermissions?: DesignProjectMemberPermissionsResponse | null;
  rowVersion: string;
}

export interface DesignProjectMemberPermissionsResponse {
  canView: boolean;
  canCreate: boolean;
  canUpdate: boolean;
  canDelete: boolean;
}

export type UpdateDesignProjectMemberPermissionsRequest = DesignProjectMemberPermissionsResponse;

export interface OperationalProjectAssignmentResponse {
  id: number;
  previousAssignmentId?: number | null;
  parentAssignmentId?: number | null;
  workKey: string;
  kpiIdentity: string;
  title: string;
  module: string;
  discipline?: string | null;
  parallelGroup?: string | null;
  assigneeMemberId: number;
  assigneeName: string;
  managerMemberId?: number | null;
  managerName?: string | null;
  status: ProjectAssignmentStatus;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  completedAt?: string | null;
  note?: string | null;
  rowVersion: string;
}

export interface OperationalProjectAssignmentAttachmentResponse {
  id: number;
  assignmentId: number;
  originalFileName: string;
  fileSize: number;
  createdAt: string;
}

export interface OperationalProjectAssignmentCommentResponse {
  id: number;
  assignmentId: number;
  body: string;
  createdByUserId: number;
  createdByName: string;
  createdAt: string;
}

export interface OperationalProjectTeamResponse {
  operationalProjectId: number;
  canManage: boolean;
  roleDefinitions: ProjectRoleDefinitionResponse[];
  moduleOptions: string[];
  disciplineOptions: string[];
  members: OperationalProjectMemberResponse[];
  assignments: OperationalProjectAssignmentResponse[];
}

export interface OperationalProjectTeamHistoryResponse {
  id: number;
  entityType: string;
  entityId: number;
  action: string;
  snapshotJson: string;
  changedAt: string;
  changedByUserId: number;
  changedByName: string;
  oldCode?: string | null;
  newCode?: string | null;
  reason?: string | null;
}

export interface ProjectMemberCandidateResponse {
  userId: number;
  name: string;
  email: string;
  roleCode: string;
  roleName: string;
  roleLabelKey?: string | null;
  departmentId?: number | null;
  departmentLabelKey?: string | null;
}

export interface ProjectMemberRoleRequest {
  roleCode: string;
  scope: string;
  scopeValue?: string | null;
}

export interface UpsertOperationalProjectMemberRequest {
  designProjectId?: number | null;
  userId: number;
  position: string;
  reportsToMemberId?: number | null;
  startedAt: string;
  endedAt?: string | null;
  roles: ProjectMemberRoleRequest[];
  rowVersion?: string;
}

export interface UpsertOperationalProjectAssignmentRequest {
  designProjectId?: number | null;
  parentAssignmentId?: number | null;
  workKey: string;
  title: string;
  module: string;
  discipline?: string | null;
  parallelGroup?: string | null;
  assigneeMemberId: number;
  managerMemberId?: number | null;
  status: ProjectAssignmentStatus;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  note?: string | null;
  rowVersion?: string;
}
