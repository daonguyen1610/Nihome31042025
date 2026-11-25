import api from "@/lib/api";

export interface MyWorkItem {
  source: "WorkTask" | "DesignTask";
  id: number;
  code: string;
  title: string;
  containerCode?: string | null;
  containerName?: string | null;
  isInternal: boolean;
  relation: "Assigned" | "Created";
  assigneeName?: string | null;
  status: string;
  isOpen: boolean;
  progressPercent: number;
  plannedStart?: string | null;
  plannedEnd?: string | null;
  isToday: boolean;
  isDueSoon: boolean;
  isOverdue: boolean;
  overdueDays?: number | null;
  isPriority: boolean;
  linkUrl: string;
}

export interface MyWorkResponse {
  today: string;
  dueSoonDays: number;
  items: MyWorkItem[];
}

export interface DailyWorkReportItem {
  sourceType?: string | null;
  sourceId?: number | null;
  title: string;
  progressPercent: number;
  note?: string | null;
}

export interface DailyWorkReport {
  id: number;
  userId: number;
  userName: string;
  reportDate: string;
  proposals?: string | null;
  tomorrowPlan: string;
  submittedAt?: string | null;
  updatedAt?: string | null;
  reviewComment?: string | null;
  reviewedByName?: string | null;
  reviewedAt?: string | null;
  canEdit: boolean;
  canReview: boolean;
  rowVersion: string;
  items: DailyWorkReportItem[];
}

export interface DepartmentDailyReports {
  reportDate: string;
  departmentRoleGroupId: number;
  departmentLabelKey: string;
  reports: DailyWorkReport[];
  missingMembers: { userId: number; name: string }[];
}

export const myWorkApi = {
  get: (params: { from?: string; to?: string } = {}) =>
    api.get<MyWorkResponse>("/my-work", { params }),
  todayReport: () => api.get<DailyWorkReport>("/my-work/daily-reports/today"),
  myReports: () => api.get<DailyWorkReport[]>("/my-work/daily-reports/mine"),
  submitToday: (body: {
    items: DailyWorkReportItem[];
    proposals?: string | null;
    tomorrowPlan: string;
    rowVersion?: string;
  }) => api.put<DailyWorkReport>("/my-work/daily-reports/today", body),
  departmentReports: (params: { date?: string; departmentId?: number }) =>
    api.get<DepartmentDailyReports>("/my-work/daily-reports/department", { params }),
  review: (id: number, comment: string, rowVersion: string) =>
    api.post<DailyWorkReport>(`/my-work/daily-reports/${id}/review`, { comment, rowVersion }),
};
