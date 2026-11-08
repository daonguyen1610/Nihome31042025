import api from "@/lib/api";
import { postIdempotent } from "./helpers";
export interface CmScheduleLine {
  taskId: number;
  designProjectId: number;
  nodeId: string;
  wbs: string;
  name: string;
  crmStart: string;
  crmEnd: string;
  plannedStart: string;
  plannedEnd: string;
  weight: number;
  isLeaf: boolean;
  isMilestone: boolean;
  predecessorIds: number[];
}
export interface CmScheduleReportPoint {
  date: string;
  crmPercent: number;
  planPercent: number;
  actualPercent: number | null;
  delayPercent: number | null;
  isDataComplete: boolean;
  snapshotId: number | null;
}
export interface CmScheduleReport {
  baselineId: number;
  period: "day" | "month" | "year";
  throughDate: string;
  points: CmScheduleReportPoint[];
}
export interface CmBaseline {
  id: number;
  version: number;
  crmRevisionId: number;
  status: "Draft" | "PendingApproval" | "Published" | "Rejected" | "Cancelled";
  curve: { day: string; crmPercent: number; planPercent: number }[];
  changeReason: string;
  weightBasisNote: string;
  workingDaysOfWeek: number[];
  nonWorkingDates: string[];
  items: CmScheduleLine[];
  siteManagerUserId: number;
  approverUserId: number;
  createdByUserId: number;
  rowVersion: string;
  publishedAt: string | null;
  reviewNote: string | null;
}
export interface CmSnapshot {
  id: number;
  baselineId: number;
  asOfDate: string;
  capturedAt: string;
  crmPercent: number;
  planPercent: number;
  actualPercent: number;
  delayPercent: number;
  thresholdPercent: number;
  isDelayed: boolean;
  explanation: string | null;
  recoveryPlan: string | null;
  recoveryDueDate: string | null;
  recoveryAt: string | null;
  reviewedAt: string | null;
  recoveryByUserId: number | null;
  rowVersion: string;
}
export interface CmActualInput {
  taskId: number;
  progressPercent: number;
  updatedAt: string;
  progressReportedAt: string | null;
  progressReportedByUserId: number | null;
}
export interface CmMailPreview {
  previewToken: string;
  emails: { toEmail: string; subject: string; body: string }[];
}
export interface CmEmailTemplate {
  subject: string;
  body: string;
}
export interface CmScheduleWorkspace {
  emailDeliveries: { id: number; baselineId: number; status: string; attemptCount: number }[];
  asOfDate: string;
  actualInputs: CmActualInput[];
  projectManagerUserId: number | null;
  crmRevisionId: number | null;
  crmVersion: number | null;
  thresholdPercent: number;
  proposed: CmScheduleLine[];
  members: { userId: number; name: string }[];
  unlinked: { id: number; taskCode: string; name: string; designProjectId: number }[];
  baselines: CmBaseline[];
  snapshots: CmSnapshot[];
}
const root = (projectId: number) => `/operational-projects/${projectId}/construction/schedule`;
export const cmScheduleApi = {
  workspace: (id: number) => api.get<CmScheduleWorkspace>(root(id)),
  create: (
    id: number,
    data: {
      changeReason: string;
      weightBasisNote: string;
      siteManagerUserId: number;
      workingDaysOfWeek: number[];
      nonWorkingDates: string[];
      items: CmScheduleLine[];
    },
  ) => postIdempotent<CmBaseline>(`${root(id)}/baselines`, data),
  report: (id: number, baselineId: number, period: "day" | "month" | "year", throughDate: string) =>
    api.get<CmScheduleReport>(`${root(id)}/baselines/${baselineId}/report`, {
      params: { period, throughDate },
    }),
  previewSubmit: (id: number, row: CmBaseline) =>
    api.get<CmMailPreview>(`${root(id)}/baselines/${row.id}/email-preview`),
  submit: (id: number, row: CmBaseline, previewToken: string) =>
    postIdempotent<CmBaseline>(`${root(id)}/baselines/${row.id}/submit`, {
      rowVersion: row.rowVersion,
      previewToken,
    }),
  cancel: (id: number, row: CmBaseline) =>
    postIdempotent<CmBaseline>(`${root(id)}/baselines/${row.id}/cancel`, {
      rowVersion: row.rowVersion,
    }),
  decide: (id: number, row: CmBaseline, approve: boolean, note: string) =>
    postIdempotent<CmBaseline>(`${root(id)}/baselines/${row.id}/decision`, {
      rowVersion: row.rowVersion,
      approve,
      note,
    }),
  updateActual: (id: number, input: CmActualInput, progressPercent: number) =>
    postIdempotent<CmScheduleWorkspace>(`${root(id)}/tasks/${input.taskId}/actual`, {
      expectedUpdatedAt: input.updatedAt,
      progressPercent,
    }),
  capture: (id: number) => postIdempotent<CmSnapshot>(`${root(id)}/snapshots`, {}),
  recover: (
    id: number,
    row: CmSnapshot,
    data: { explanation: string; recoveryPlan: string; dueDate: string },
  ) =>
    postIdempotent<CmSnapshot>(`${root(id)}/snapshots/${row.id}/recovery`, {
      ...data,
      rowVersion: row.rowVersion,
    }),
  review: (id: number, row: CmSnapshot) =>
    postIdempotent<CmSnapshot>(`${root(id)}/snapshots/${row.id}/review`, {
      rowVersion: row.rowVersion,
    }),
  template: () => api.get<CmEmailTemplate>("/site-settings/cm-schedule/email-template"),
  setTemplate: (data: CmEmailTemplate) =>
    api.put<CmEmailTemplate>("/site-settings/cm-schedule/email-template", data),
  templatePreview: (data: CmEmailTemplate) =>
    api.post<CmEmailTemplate>("/site-settings/cm-schedule/email-template/preview", data),
  resolveEmail: (id: number, emailId: number, decision: "Retry" | "ConfirmReceived") =>
    api.post(`/operational-projects/${id}/master-schedule/emails/${emailId}/resolve`, { decision }),
  policy: () => api.get<{ thresholdPercent: number }>("/site-settings/cm-schedule"),
  setPolicy: (thresholdPercent: number) =>
    api.put<{ thresholdPercent: number }>("/site-settings/cm-schedule", { thresholdPercent }),
};
