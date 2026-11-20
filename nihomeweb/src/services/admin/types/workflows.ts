export interface WorkflowStep {
  order: number;
  name: string;
  approverRoleCode: string;
  slaHours: number;
  requireAllApprovers: boolean;
  conditionExpression?: string | null;
}

export interface WorkflowConfig {
  id: number;
  module: string;
  action: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
  steps: WorkflowStep[];
  /** Steps of an applied workflow (quote approval) that nobody can approve today. */
  stepIssues?: WorkflowStepIssue[];
  createdAt: string;
  updatedAt: string;
}

export interface WorkflowStepIssue {
  order: number;
  roleCode: string;
  roleLabelKey?: string | null;
  roleName: string;
  issue: "RoleMissing" | "RoleInactive" | "RoleCannotApprove" | "NoEligibleUser";
}

export interface UpsertWorkflowConfigRequest {
  module: string;
  action: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  sortOrder: number;
  steps: WorkflowStep[];
}
