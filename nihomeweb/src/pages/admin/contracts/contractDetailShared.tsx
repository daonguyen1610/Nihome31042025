import type { VoDraft } from "./ContractAppendicesTab";
import { CheckCircle2, Clock, Pencil, Play, ShieldCheck, XCircle } from "lucide-react";
import { extractApiError } from "@/lib/apiError";
import { formatDate as formatDateInZone } from "@/lib/displaySettings";
import {
  type ContractAppendixStatus,
  type ContractPaymentMilestoneRequest,
  type ContractResponse,
  type ContractDirection,
  type ContractStatus,
  type ContractType,
  type PaymentMilestoneStatus,
} from "@/services/adminApi";
import { todayIsoDate } from "@/lib/displaySettings";

export const STATUS_STYLES: Record<ContractStatus, string> = {
  Draft: "border-border bg-muted/30 text-foreground",
  Signed: "border-sky-200 bg-sky-50 text-sky-700",
  InProgress: "border-amber-200 bg-amber-50 text-amber-800",
  OnHold: "border-orange-200 bg-orange-50 text-orange-700",
  Completed: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Cancelled: "border-zinc-200 bg-zinc-100 text-zinc-600",
};

export const VO_STATUS_STYLES: Record<ContractAppendixStatus, string> = {
  Draft: "border-border bg-muted/30 text-foreground",
  Submitted: "border-amber-200 bg-amber-50 text-amber-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
};

export const MILESTONE_STATUS_STYLES: Record<PaymentMilestoneStatus, string> = {
  Pending: "border-border bg-muted/30 text-foreground",
  Requested: "border-amber-200 bg-amber-50 text-amber-700",
  Paid: "border-emerald-200 bg-emerald-50 text-emerald-700",
};

export const formatDate = (value?: string | null): string => {
  if (!value) return "—";
  const iso = value.slice(0, 10);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(iso)) return iso;
  return formatDateInZone(iso);
};

export const formatBytes = (bytes: number | undefined | null): string => {
  if (!bytes || bytes <= 0) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
};

export const getErrorMessage = (err: unknown): string | undefined => {
  return extractApiError(err);
};

export interface MilestoneDraft {
  id?: number;
  order: number;
  name: string;
  percentValue: number;
  dueDate: string;
  actualPaymentDate: string;
  status: PaymentMilestoneStatus;
  responsibleAccountantUserId: number | null;
  requestedAt: string | null;
  note: string;
}

export interface ContractEditForm {
  contractNumber: string;
  customerId: number;
  direction: ContractDirection;
  type: ContractType;
  vendorId: number | null;
  operationalProjectId: number | null;
  ownerName: string;
  signedDate: string;
  startDate: string;
  endDate: string;
  value: number;
  scopeOfWork: string;
  note: string;
  milestones: MilestoneDraft[];
}

export const toIsoDate = (value?: string | null): string => value?.slice(0, 10) ?? "";

export const getLocalIsoDate = (): string => todayIsoDate();

export const toIsoTimestamp = (value: string): string | null => {
  if (!value) return null;
  const date = new Date(`${value}T00:00:00Z`);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
};

export const toEditForm = (contract: ContractResponse): ContractEditForm => ({
  contractNumber: contract.contractNumber,
  customerId: contract.customerId,
  direction: contract.direction,
  type: contract.type,
  vendorId: contract.vendorId ?? null,
  operationalProjectId: contract.operationalProjectId ?? null,
  ownerName: contract.ownerName ?? "",
  signedDate: toIsoDate(contract.signedDate),
  startDate: toIsoDate(contract.startDate),
  endDate: toIsoDate(contract.endDate),
  value: contract.value,
  scopeOfWork: contract.scopeOfWork ?? "",
  note: contract.note ?? "",
  milestones: contract.paymentMilestones.map((milestone) => ({
    id: milestone.id,
    order: milestone.order,
    name: milestone.name,
    percentValue: milestone.percentValue,
    dueDate: toIsoDate(milestone.dueDate),
    actualPaymentDate: toIsoDate(milestone.actualPaymentDate),
    status: milestone.status,
    responsibleAccountantUserId: milestone.responsibleAccountantUserId ?? null,
    requestedAt: milestone.requestedAt ?? null,
    note: milestone.note ?? "",
  })),
});

export const hasScheduleChanges = (
  contract: ContractResponse,
  milestones: MilestoneDraft[],
): boolean => {
  if (contract.paymentMilestones.length !== milestones.length) return true;
  return milestones.some((milestone, index) => {
    const persisted = contract.paymentMilestones[index];
    return (
      milestone.order !== persisted.order ||
      milestone.name.trim() !== persisted.name ||
      milestone.percentValue !== persisted.percentValue ||
      milestone.dueDate !== toIsoDate(persisted.dueDate) ||
      milestone.actualPaymentDate !== toIsoDate(persisted.actualPaymentDate) ||
      milestone.status !== persisted.status ||
      (milestone.note.trim() || null) !== (persisted.note?.trim() || null)
    );
  });
};

export const toMilestoneRequests = (
  milestones: MilestoneDraft[],
): ContractPaymentMilestoneRequest[] =>
  milestones.map((milestone, index) => ({
    id: milestone.id ?? null,
    order: index + 1,
    name: milestone.name.trim(),
    percentValue: milestone.percentValue,
    dueDate: toIsoTimestamp(milestone.dueDate),
    actualPaymentDate:
      milestone.status === "Paid" ? toIsoTimestamp(milestone.actualPaymentDate) : null,
    status: milestone.status,
    responsibleAccountantUserId: milestone.responsibleAccountantUserId,
    requestedAt: milestone.requestedAt,
    note: milestone.note.trim() || null,
  }));

export const AVAILABLE_TRANSITIONS: Record<ContractStatus, ContractStatus[]> = {
  Draft: ["Signed", "Cancelled"],
  Signed: ["InProgress", "Cancelled"],
  InProgress: ["OnHold", "Completed", "Cancelled"],
  OnHold: ["InProgress", "Cancelled"],
  Completed: [],
  Cancelled: [],
};

export const TRANSITION_ICON: Record<ContractStatus, JSX.Element> = {
  Signed: <CheckCircle2 className="h-4 w-4" />,
  InProgress: <Play className="h-4 w-4" />,
  OnHold: <Clock className="h-4 w-4" />,
  Completed: <ShieldCheck className="h-4 w-4" />,
  Cancelled: <XCircle className="h-4 w-4" />,
  Draft: <Pencil className="h-4 w-4" />,
};

export const TRANSITION_LABEL_KEY: Record<ContractStatus, string> = {
  Signed: "contracts.detail.transition.markSigned",
  // Note: InProgress action is contextual — "resume" from OnHold vs
  // "start" from Signed. Resolved at render time below.
  InProgress: "contracts.detail.transition.markInProgress",
  OnHold: "contracts.detail.transition.markOnHold",
  Completed: "contracts.detail.transition.markCompleted",
  Cancelled: "contracts.detail.transition.markCancelled",
  Draft: "",
};

export const blankVoDraft = (): VoDraft => ({
  title: "",
  reason: "",
  valueDelta: "",
  files: [],
  pendingFiles: [],
});

export const isOverdue = (
  dueDate: string | null | undefined,
  status: PaymentMilestoneStatus,
): boolean => {
  if (!dueDate || status === "Paid") return false;
  const d = new Date(dueDate).getTime();
  if (Number.isNaN(d)) return false;
  return d < Date.now();
};
