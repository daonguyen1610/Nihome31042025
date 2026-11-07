export type SiteDiaryStatus = "Draft" | "Submitted" | "Confirmed";

export interface SiteDiaryResponse {
  id: number;
  designProjectId: number;
  designProjectCode?: string | null;
  designProjectName?: string | null;
  diaryDate: string;
  weatherCode: string;
  weatherLabel?: string | null;
  weatherNote?: string | null;
  headcountLabor: number;
  headcountEngineers: number;
  headcountSupervisors: number;
  headcountSubcontractors: number;
  headcountTotal: number;
  machinesSummary?: string | null;
  materialsReceived?: string | null;
  workPerformed: string;
  incidents?: string | null;
  note?: string | null;
  status: SiteDiaryStatus;
  submittedAt?: string | null;
  submittedByUserId?: number | null;
  submittedByName?: string | null;
  confirmedAt?: string | null;
  confirmedByUserId?: number | null;
  confirmedByName?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface SiteDiaryListResponse {
  total: number;
  page: number;
  pageSize: number;
  items: SiteDiaryResponse[];
  statusCounts: Partial<Record<SiteDiaryStatus, number>>;
}

export interface SiteDiaryListParams {
  designProjectId?: number;
  weatherCode?: string;
  status?: string;
  dateFrom?: string;
  dateTo?: string;
  search?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateSiteDiaryRequest {
  designProjectId: number;
  diaryDate: string;
  weatherCode: string;
  weatherNote?: string | null;
  headcountLabor: number;
  headcountEngineers: number;
  headcountSupervisors: number;
  headcountSubcontractors: number;
  machinesSummary?: string | null;
  materialsReceived?: string | null;
  workPerformed: string;
  incidents?: string | null;
  note?: string | null;
}

export interface UpdateSiteDiaryRequest {
  diaryDate: string;
  weatherCode: string;
  weatherNote?: string | null;
  headcountLabor: number;
  headcountEngineers: number;
  headcountSupervisors: number;
  headcountSubcontractors: number;
  machinesSummary?: string | null;
  materialsReceived?: string | null;
  workPerformed: string;
  incidents?: string | null;
  note?: string | null;
}

export interface BulkDeleteSiteDiariesRequest {
  ids: number[];
}

export interface SiteDiaryBulkDeleteFailure {
  id: number;
  message: string;
}

export interface SiteDiaryBulkDeleteResponse {
  requested: number;
  deleted: number;
  failures: SiteDiaryBulkDeleteFailure[];
}
