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
  createdAt: string;
  updatedAt: string;
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
