export type ProjectReportAvailability = "Available" | "Unavailable";

export interface ProjectReportResponse {
  generatedAtUtc: string;
  asOfUtc: string;
  from?: string | null;
  to?: string | null;
  projectId?: number | null;
  projects: ProjectOperationalReportResponse[];
}

export interface ProjectOperationalReportResponse {
  project: ProjectReportIdentityResponse;
  design: ProjectReportDesignResponse;
  construction: ProjectReportConstructionResponse;
  acceptance: ProjectReportAcceptanceResponse;
  permits: ProjectReportPermitResponse;
  contractualFinance: ProjectReportContractualFinanceResponse;
  unavailableMetrics: ProjectReportUnavailableMetricResponse[];
}

export interface ProjectReportIdentityResponse {
  operationalProjectId: number;
  code: string;
  name: string;
  customerId: number;
  customerName: string;
  projectManagerUserId?: number | null;
  projectManagerName?: string | null;
  status: string;
  startDate?: string | null;
  endDate?: string | null;
  drillDown: ProjectReportDrillDownResponse;
}

export interface ProjectReportDesignResponse {
  availability: ProjectReportAvailability;
  reasonCode?: string | null;
  weightedProgressPercent?: number | null;
  rollupPolicyVersion: string;
  sources: ProjectReportDesignSourceResponse[];
}

export interface ProjectReportDesignSourceResponse {
  phaseId: number;
  weight: number;
  progressPercent: number;
  weightedValue: number;
}

export interface ProjectReportConstructionResponse {
  availability: ProjectReportAvailability;
  reasonCode?: string | null;
  totalCount: number;
  countsByStatus: Record<string, number>;
  overdueCount: number;
  overdueItems: ProjectReportConstructionTaskResponse[];
}

export interface ProjectReportConstructionTaskResponse {
  id: number;
  designProjectId: number;
  taskCode: string;
  name: string;
  status: string;
  plannedEnd: string;
  drillDown: ProjectReportDrillDownResponse;
}

export interface ProjectReportAcceptanceResponse {
  availability: ProjectReportAvailability;
  reasonCode?: string | null;
  totalCount: number;
  countsByStatus: Record<string, number>;
  countsByRevision: Record<string, number>;
  overdueCount: number;
}

export interface ProjectReportPermitResponse {
  availability: ProjectReportAvailability;
  reasonCode?: string | null;
  overdueCount: number;
  dueSoonCount: number;
  expiringCount: number;
  dueSoonWindowDays: number;
  items: ProjectReportPermitItemResponse[];
}

export interface ProjectReportPermitItemResponse {
  id: number;
  designProjectId: number;
  permitTypeCode: string;
  status: string;
  targetDeadline?: string | null;
  expiresAt?: string | null;
  isOverdue: boolean;
  isDueSoon: boolean;
  isExpiring: boolean;
  drillDown: ProjectReportDrillDownResponse;
}

export interface ProjectReportContractualFinanceResponse {
  availability: ProjectReportAvailability;
  label: string;
  quoteGrandTotal: number;
  quoteTotalsByStatus: Record<string, number>;
  contractBaseValue: number;
  approvedVariationOrderDelta: number;
  contractCurrentValue: number;
  milestoneLabel: string;
  milestoneScheduledValuesByStatus: Record<string, number>;
}

export interface ProjectReportUnavailableMetricResponse {
  metricCode: string;
  availability: ProjectReportAvailability;
  reasonCode: string;
}

export interface ProjectReportDrillDownResponse {
  route: string;
  query?: string | null;
}

export interface ProjectReportFilters {
  projectId?: number;
  from?: string;
  to?: string;
}

export interface ProjectReportExportParams extends ProjectReportFilters {
  format: "xlsx" | "pdf";
  language: "vi" | "en" | "zh" | "ja";
}
