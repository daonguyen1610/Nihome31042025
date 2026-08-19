import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import {
  ArrowDown,
  ArrowLeft,
  ArrowUp,
  CheckCircle2,
  ChevronDown,
  Clock,
  FileText,
  History,
  Loader2,
  Pencil,
  Play,
  Plus,
  Save,
  Send,
  ShieldCheck,
  ShieldX,
  Trash2,
  Upload,
  XCircle,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { extractApiError } from "@/lib/apiError";
import { PageLoading, PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import {
  adminApi,
  PAYMENT_MILESTONE_STATUSES,
  type ContractAppendixResponse,
  type ContractAppendixStatus,
  type ContractAttachmentKind,
  type ContractAttachmentResponse,
  type ContractPaymentMilestoneRequest,
  type ContractResponse,
  type ContractStatus,
  type CustomerResponse,
  type ContractTimelineEvent,
  type PaymentMilestoneStatus,
  type UpsertContractRequest,
  type UpsertContractAppendixRequest,
} from "@/services/adminApi";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";

// -------- shared helpers --------

const STATUS_STYLES: Record<ContractStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Signed: "border-sky-200 bg-sky-50 text-sky-700",
  InProgress: "border-amber-200 bg-amber-50 text-amber-800",
  OnHold: "border-orange-200 bg-orange-50 text-orange-700",
  Completed: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Cancelled: "border-zinc-200 bg-zinc-100 text-zinc-600",
};

const VO_STATUS_STYLES: Record<ContractAppendixStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Submitted: "border-amber-200 bg-amber-50 text-amber-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
};

const MILESTONE_STATUS_STYLES: Record<PaymentMilestoneStatus, string> = {
  Pending: "border-slate-200 bg-slate-50 text-slate-700",
  Requested: "border-amber-200 bg-amber-50 text-amber-700",
  Paid: "border-emerald-200 bg-emerald-50 text-emerald-700",
};

const formatDate = (value?: string | null): string => {
  if (!value) return "—";
  const iso = value.slice(0, 10);
  const parts = iso.split("-");
  if (parts.length !== 3) return iso;
  const [yyyy, mm, dd] = parts;
  return `${dd}/${mm}/${yyyy}`;
};

const formatCurrency = (value: number, lang: string): string => {
  try {
    return new Intl.NumberFormat(lang === "vi" ? "vi-VN" : "en-US").format(value);
  } catch {
    return value.toString();
  }
};

const formatBytes = (bytes: number | undefined | null): string => {
  if (!bytes || bytes <= 0) return "—";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
};

const getErrorMessage = (err: unknown): string | undefined => {
  const parsed = extractApiError(err);
  return parsed?.message;
};

interface MilestoneDraft {
  order: number;
  name: string;
  percentValue: number;
  dueDate: string;
  status: PaymentMilestoneStatus;
  note: string;
}

interface ContractEditForm {
  contractNumber: string;
  customerId: number;
  ownerName: string;
  signedDate: string;
  startDate: string;
  endDate: string;
  value: number;
  scopeOfWork: string;
  note: string;
  milestones: MilestoneDraft[];
}

const toIsoDate = (value?: string | null): string => value?.slice(0, 10) ?? "";

const toIsoTimestamp = (value: string): string | null => {
  if (!value) return null;
  const date = new Date(`${value}T00:00:00Z`);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
};

const toEditForm = (contract: ContractResponse): ContractEditForm => ({
  contractNumber: contract.contractNumber,
  customerId: contract.customerId,
  ownerName: contract.ownerName ?? "",
  signedDate: toIsoDate(contract.signedDate),
  startDate: toIsoDate(contract.startDate),
  endDate: toIsoDate(contract.endDate),
  value: contract.value,
  scopeOfWork: contract.scopeOfWork ?? "",
  note: contract.note ?? "",
  milestones: contract.paymentMilestones.map((milestone) => ({
    order: milestone.order,
    name: milestone.name,
    percentValue: milestone.percentValue,
    dueDate: toIsoDate(milestone.dueDate),
    status: milestone.status,
    note: milestone.note ?? "",
  })),
});

// -------- header --------

const AVAILABLE_TRANSITIONS: Record<ContractStatus, ContractStatus[]> = {
  Draft: ["Signed", "Cancelled"],
  Signed: ["InProgress", "Cancelled"],
  InProgress: ["OnHold", "Completed", "Cancelled"],
  OnHold: ["InProgress", "Cancelled"],
  Completed: [],
  Cancelled: [],
};

const TRANSITION_ICON: Record<ContractStatus, JSX.Element> = {
  Signed: <CheckCircle2 className="h-4 w-4" />,
  InProgress: <Play className="h-4 w-4" />,
  OnHold: <Clock className="h-4 w-4" />,
  Completed: <ShieldCheck className="h-4 w-4" />,
  Cancelled: <XCircle className="h-4 w-4" />,
  Draft: <Pencil className="h-4 w-4" />,
};

const TRANSITION_LABEL_KEY: Record<ContractStatus, string> = {
  Signed: "contracts.detail.transition.markSigned",
  // Note: InProgress action is contextual — "resume" from OnHold vs
  // "start" from Signed. Resolved at render time below.
  InProgress: "contracts.detail.transition.markInProgress",
  OnHold: "contracts.detail.transition.markOnHold",
  Completed: "contracts.detail.transition.markCompleted",
  Cancelled: "contracts.detail.transition.markCancelled",
  Draft: "",
};

interface HeaderProps {
  contract: ContractResponse;
  canEdit: boolean;
  editing: boolean;
  saving: boolean;
  onEditInfo: () => void;
  onCancelEdit: () => void;
  onSave: () => void;
  onTransition: (next: ContractStatus) => void;
  transitionBusy: ContractStatus | null;
}

const ContractHeader = ({
  contract,
  canEdit,
  editing,
  saving,
  onEditInfo,
  onCancelEdit,
  onSave,
  onTransition,
  transitionBusy,
}: HeaderProps) => {
  const { t, lang } = useI18n();

  const transitions = AVAILABLE_TRANSITIONS[contract.status] ?? [];
  const label = (target: ContractStatus): string => {
    // Signed -> InProgress on the Signed row = "Move to In progress",
    // OnHold -> InProgress = "Resume". Same icon, clearer wording.
    if (target === "InProgress" && contract.status === "OnHold") {
      return t("contracts.detail.transition.markResume");
    }
    return t(TRANSITION_LABEL_KEY[target]);
  };

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm md:p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2 text-sm text-slate-500">
            <Link to="/admin/contracts" className="inline-flex items-center gap-1 hover:text-slate-800">
              <ArrowLeft className="h-4 w-4" />
              {t("contracts.detail.back")}
            </Link>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-2">
            <h1 className="truncate text-xl font-bold text-slate-900 md:text-2xl">
              {contract.contractNumber}
            </h1>
            <Badge className={cn("border capitalize", STATUS_STYLES[contract.status])}>
              {t(`contracts.status.${contract.status}`)}
            </Badge>
          </div>
          <p className="mt-1 text-sm text-slate-600">
            <span className="font-medium">{contract.customerName ?? "—"}</span>
            {contract.ownerName ? (
              <span className="ml-2 text-xs text-slate-400">• {contract.ownerName}</span>
            ) : null}
          </p>
        </div>
        <div className="flex shrink-0 flex-wrap gap-2">
          {canEdit && !editing ? (
            <Button variant="outline" size="sm" onClick={onEditInfo}>
              <Pencil className="mr-1 h-4 w-4" />
              {t("common.edit")}
            </Button>
          ) : null}
          {editing ? (
            <>
              <Button variant="outline" size="sm" onClick={onCancelEdit} disabled={saving}>
                {t("common.cancel")}
              </Button>
              <Button size="sm" onClick={onSave} disabled={saving}>
                {saving ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Save className="mr-1 h-4 w-4" />}
                {t("common.save")}
              </Button>
            </>
          ) : null}
        </div>
      </div>

      <div className="mt-4 grid gap-3 sm:grid-cols-3">
        <div className="rounded-lg bg-slate-50 p-3">
          <div className="text-xs text-slate-500">{t("contracts.detail.header.baseValue")}</div>
          <div className="mt-1 text-lg font-semibold text-slate-800">
            {formatCurrency(contract.value, lang)} ₫
          </div>
        </div>
        <div className="rounded-lg bg-slate-50 p-3">
          <div className="text-xs text-slate-500">{t("contracts.detail.header.voTotal")}</div>
          <div
            className={cn(
              "mt-1 text-lg font-semibold",
              contract.approvedVoTotal > 0 ? "text-emerald-700" : contract.approvedVoTotal < 0 ? "text-rose-700" : "text-slate-800",
            )}
          >
            {contract.approvedVoTotal > 0 ? "+" : ""}
            {formatCurrency(contract.approvedVoTotal, lang)} ₫
          </div>
        </div>
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-3">
          <div className="text-xs text-emerald-700">{t("contracts.detail.header.currentValue")}</div>
          <div className="mt-1 text-lg font-bold text-emerald-900">
            {formatCurrency(contract.currentValue, lang)} ₫
          </div>
        </div>
      </div>

      {!editing && transitions.length > 0 ? (
        <div className="mt-4 flex flex-wrap gap-2">
          {transitions.map((target) => (
            <Button
              key={target}
              variant={target === "Cancelled" ? "outline" : "default"}
              size="sm"
              disabled={transitionBusy !== null}
              onClick={() => onTransition(target)}
              className={cn(
                target === "Cancelled" && "border-rose-200 text-rose-700 hover:bg-rose-50",
              )}
            >
              {transitionBusy === target ? (
                <Loader2 className="mr-1 h-4 w-4 animate-spin" />
              ) : (
                <span className="mr-1">{TRANSITION_ICON[target]}</span>
              )}
              {label(target)}
            </Button>
          ))}
        </div>
      ) : null}
    </div>
  );
};

// -------- Info tab (read-only) --------

const InfoTab = ({ contract }: { contract: ContractResponse }) => {
  const { t, lang } = useI18n();

  const rows: [string, React.ReactNode][] = [
    [t("contracts.field.number"), contract.contractNumber],
    [t("contracts.field.customer"), contract.customerName ?? "—"],
    [t("contracts.field.owner"), contract.ownerName ?? "—"],
    [t("contracts.field.status"), t(`contracts.status.${contract.status}`)],
    [t("contracts.field.signedDate"), formatDate(contract.signedDate)],
    [t("contracts.field.startDate"), formatDate(contract.startDate)],
    [t("contracts.field.endDate"), formatDate(contract.endDate)],
    [t("contracts.field.value"), `${formatCurrency(contract.value, lang)} ₫`],
  ];

  return (
    <div className="space-y-4">
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <dl className="grid gap-3 sm:grid-cols-2">
          {rows.map(([label, value]) => (
            <div key={label}>
              <dt className="text-xs font-medium uppercase tracking-wide text-slate-500">{label}</dt>
              <dd className="mt-0.5 text-sm text-slate-800">{value}</dd>
            </div>
          ))}
        </dl>
      </div>
      {contract.scopeOfWork ? (
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-xs font-medium uppercase tracking-wide text-slate-500">
            {t("contracts.field.scope")}
          </div>
          <p className="mt-2 whitespace-pre-wrap text-sm text-slate-800">{contract.scopeOfWork}</p>
        </div>
      ) : null}
      {contract.note ? (
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="text-xs font-medium uppercase tracking-wide text-slate-500">
            {t("contracts.field.note")}
          </div>
          <p className="mt-2 whitespace-pre-wrap text-sm text-slate-800">{contract.note}</p>
        </div>
      ) : null}
    </div>
  );
};

interface EditInfoTabProps {
  form: ContractEditForm;
  customers: CustomerResponse[];
  error: string | null;
  onChange: (next: ContractEditForm) => void;
}

const EditInfoTab = ({ form, customers, error, onChange }: EditInfoTabProps) => {
  const { t, lang } = useI18n();

  return (
    <div className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-number">{t("contracts.field.number")}</Label>
          <Input
            id="contract-detail-number"
            value={form.contractNumber}
            onChange={(event) => onChange({ ...form, contractNumber: event.target.value })}
            className="font-mono"
          />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-customer">{t("contracts.field.customer")} *</Label>
          <Select
            value={String(form.customerId)}
            onValueChange={(value) => {
              const customerId = Number(value);
              const customer = customers.find((item) => item.id === customerId);
              onChange({ ...form, customerId, ownerName: customer?.ownerName ?? "" });
            }}
          >
            <SelectTrigger id="contract-detail-customer"><SelectValue /></SelectTrigger>
            <SelectContent className="max-h-72">
              {customers.map((customer) => (
                <SelectItem key={customer.id} value={String(customer.id)}>{customer.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="space-y-1.5 rounded-md border bg-slate-50 px-3 py-2.5">
        <Label>{t("contracts.field.owner")}</Label>
        <p className="text-sm font-medium">{form.ownerName || t("contracts.ownerCurrentUserFallback")}</p>
        <p className="text-xs text-slate-500">{t("contracts.ownerInheritedHint")}</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-signed">{t("contracts.field.signedDate")}</Label>
          <Input id="contract-detail-signed" type="date" value={form.signedDate} onChange={(event) => onChange({ ...form, signedDate: event.target.value })} />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-start">{t("contracts.field.startDate")}</Label>
          <Input id="contract-detail-start" type="date" value={form.startDate} onChange={(event) => onChange({ ...form, startDate: event.target.value })} />
        </div>
        <div className="space-y-1.5">
          <Label htmlFor="contract-detail-end">{t("contracts.field.endDate")}</Label>
          <Input id="contract-detail-end" type="date" value={form.endDate} onChange={(event) => onChange({ ...form, endDate: event.target.value })} />
        </div>
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="contract-detail-value">{t("contracts.field.value")} *</Label>
        <Input id="contract-detail-value" type="number" min={0} value={form.value} onChange={(event) => onChange({ ...form, value: Number(event.target.value) || 0 })} />
        <p className="text-xs text-slate-500">{formatCurrency(form.value, lang)} ₫</p>
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="contract-detail-scope">{t("contracts.field.scope")}</Label>
        <Textarea id="contract-detail-scope" rows={4} value={form.scopeOfWork} onChange={(event) => onChange({ ...form, scopeOfWork: event.target.value })} />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="contract-detail-note">{t("contracts.field.note")}</Label>
        <Textarea id="contract-detail-note" rows={3} value={form.note} onChange={(event) => onChange({ ...form, note: event.target.value })} />
      </div>
      {error ? <p className="text-sm text-destructive">{error}</p> : null}
    </div>
  );
};

// -------- Schedule tab --------

interface ScheduleTabProps {
  contract: ContractResponse;
  onMilestoneStatus: (milestoneId: number, next: PaymentMilestoneStatus) => Promise<void>;
  busyMilestoneId: number | null;
}

const isOverdue = (
  dueDate: string | null | undefined,
  status: PaymentMilestoneStatus,
): boolean => {
  if (!dueDate || status === "Paid") return false;
  const d = new Date(dueDate).getTime();
  if (Number.isNaN(d)) return false;
  return d < Date.now();
};

const ScheduleTab = ({ contract, onMilestoneStatus, busyMilestoneId }: ScheduleTabProps) => {
  const { t, lang } = useI18n();
  const milestones = contract.paymentMilestones;

  if (milestones.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-slate-300 p-6 text-center text-sm text-slate-500">
        {t("contracts.milestonesEmpty")}
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {milestones.map((m) => {
        const overdue = isOverdue(m.dueDate, m.status);
        const busy = busyMilestoneId === m.id;
        return (
          <div
            key={m.id}
            className={cn(
              "rounded-lg border bg-white p-4 shadow-sm",
              overdue ? "border-rose-300 ring-1 ring-rose-100" : "border-slate-200",
            )}
          >
            <div className="flex flex-wrap items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-semibold text-slate-500">#{m.order}</span>
                  <span className="truncate text-base font-semibold text-slate-900">{m.name}</span>
                </div>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-500">
                  <span>
                    {m.percentValue}% • {formatCurrency(m.amount, lang)} ₫
                  </span>
                  <span>• {t("contracts.milestone.dueDate")}: {formatDate(m.dueDate)}</span>
                  <Badge className={cn("border", MILESTONE_STATUS_STYLES[m.status])}>
                    {t(`contracts.milestoneStatus.${m.status}`)}
                  </Badge>
                  {overdue ? (
                    <Badge className="border border-rose-300 bg-rose-100 text-rose-700">
                      {t("contracts.schedule.overdue")}
                    </Badge>
                  ) : null}
                </div>
              </div>
              <div className="flex flex-wrap gap-2">
                {m.status === "Pending" ? (
                  <Button size="sm" variant="outline" disabled={busy} onClick={() => onMilestoneStatus(m.id, "Requested")}>
                    {busy ? <Loader2 className="mr-1 h-3 w-3 animate-spin" /> : <Send className="mr-1 h-3 w-3" />}
                    {t("contracts.schedule.markRequested")}
                  </Button>
                ) : null}
                {(m.status === "Pending" || m.status === "Requested") ? (
                  <Button size="sm" disabled={busy} onClick={() => onMilestoneStatus(m.id, "Paid")}>
                    {busy ? <Loader2 className="mr-1 h-3 w-3 animate-spin" /> : <CheckCircle2 className="mr-1 h-3 w-3" />}
                    {t("contracts.schedule.markPaid")}
                  </Button>
                ) : null}
                {m.status !== "Pending" ? (
                  <Button size="sm" variant="ghost" disabled={busy} onClick={() => onMilestoneStatus(m.id, "Pending")}>
                    {t("contracts.schedule.revertPending")}
                  </Button>
                ) : null}
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
};

interface EditScheduleTabProps {
  value: number;
  milestones: MilestoneDraft[];
  onChange: (milestones: MilestoneDraft[]) => void;
}

const EditScheduleTab = ({ value, milestones, onChange }: EditScheduleTabProps) => {
  const { t, lang } = useI18n();
  const total = milestones.reduce((sum, milestone) => sum + milestone.percentValue, 0);
  const totalValid = milestones.length === 0 || Math.abs(total - 100) <= 0.01;
  const patch = (index: number, next: Partial<MilestoneDraft>) =>
    onChange(milestones.map((milestone, current) => current === index ? { ...milestone, ...next } : milestone));
  const move = (index: number, direction: -1 | 1) => {
    const target = index + direction;
    if (target < 0 || target >= milestones.length) return;
    const next = [...milestones];
    [next[index], next[target]] = [next[target], next[index]];
    onChange(next.map((milestone, current) => ({ ...milestone, order: current + 1 })));
  };

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <Button type="button" size="sm" variant="outline" onClick={() => onChange([...milestones, { order: milestones.length + 1, name: "", percentValue: 0, dueDate: "", status: "Pending", note: "" }])}>
          <Plus className="mr-1 h-4 w-4" />
          {t("contracts.addMilestone")}
        </Button>
      </div>
      {milestones.length === 0 ? (
        <div className="rounded-lg border border-dashed border-slate-300 p-6 text-center text-sm text-slate-500">{t("contracts.milestonesEmpty")}</div>
      ) : milestones.map((milestone, index) => {
        const amount = Math.round(value * milestone.percentValue) / 100;
        return (
          <div key={index} className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm" data-testid={`contract-detail-milestone-${index}`}>
            <div className="flex items-center justify-between gap-2">
              <span className="text-sm font-semibold text-slate-500">#{index + 1}</span>
              <div className="flex gap-1">
                <Button type="button" size="icon" variant="ghost" className="h-8 w-8" disabled={index === 0} onClick={() => move(index, -1)} aria-label={t("workflow.moveUp")}><ArrowUp className="h-4 w-4" /></Button>
                <Button type="button" size="icon" variant="ghost" className="h-8 w-8" disabled={index === milestones.length - 1} onClick={() => move(index, 1)} aria-label={t("workflow.moveDown")}><ArrowDown className="h-4 w-4" /></Button>
                <Button
                  type="button"
                  size="icon"
                  variant="ghost"
                  className="h-8 w-8 text-destructive hover:text-destructive"
                  onClick={() => onChange(
                    milestones
                      .filter((_, current) => current !== index)
                      .map((item, current) => ({ ...item, order: current + 1 })),
                  )}
                  aria-label={t("common.delete")}
                >
                  <Trash2 className="h-4 w-4" />
                </Button>
              </div>
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5"><Label>{t("contracts.milestone.name")} *</Label><Input value={milestone.name} onChange={(event) => patch(index, { name: event.target.value })} /></div>
              <div className="space-y-1.5"><Label>{t("contracts.milestone.percent")} *</Label><Input type="number" min={0} max={100} step="0.01" value={milestone.percentValue} onChange={(event) => patch(index, { percentValue: Number(event.target.value) || 0 })} /><p className="text-xs text-slate-500">≈ {formatCurrency(amount, lang)} ₫</p></div>
              <div className="space-y-1.5"><Label>{t("contracts.milestone.dueDate")}</Label><Input type="date" value={milestone.dueDate} onChange={(event) => patch(index, { dueDate: event.target.value })} /></div>
              <div className="space-y-1.5">
                <Label>{t("contracts.milestone.status")}</Label>
                <Select
                  value={milestone.status}
                  onValueChange={(status) => patch(index, { status: status as PaymentMilestoneStatus })}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {PAYMENT_MILESTONE_STATUSES.map((status) => (
                      <SelectItem key={status} value={status}>
                        {t(`contracts.milestoneStatus.${status}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>
        );
      })}
      {milestones.length > 0 ? (
        <div className={cn("flex items-center justify-between rounded px-3 py-2 text-sm", totalValid ? "bg-emerald-50 text-emerald-700" : "bg-rose-50 text-rose-700")}>
          <span>{t("contracts.milestonesSum")}</span><span className="font-semibold">{total.toFixed(2)}% / 100%</span>
        </div>
      ) : null}
    </div>
  );
};

// -------- VO (appendix) tab --------

interface VoDraft {
  id?: number;
  title: string;
  reason: string;
  valueDelta: string;
  filePath?: string | null;
  originalFileName?: string | null;
  fileSize?: number | null;
  contentType?: string | null;
}

const blankVoDraft = (): VoDraft => ({
  title: "",
  reason: "",
  valueDelta: "",
});

interface VoTabProps {
  contract: ContractResponse;
  rows: ContractAppendixResponse[];
  refresh: () => Promise<void>;
}

const VoTab = ({ contract, rows, refresh }: VoTabProps) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const [draft, setDraft] = useState<VoDraft | null>(null);
  const [saving, setSaving] = useState(false);
  const [uploadBusy, setUploadBusy] = useState(false);
  const [pendingActionId, setPendingActionId] = useState<number | null>(null);
  const [rejectingVo, setRejectingVo] = useState<ContractAppendixResponse | null>(null);
  const [rejectNote, setRejectNote] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

  const deletableIds = useMemo(
    () => rows.map((row) => row.id),
    [rows],
  );
  const bulk = useBulkSelection<number>({
    visibleIds: deletableIds,
    deleteOne: (id) => adminApi.deleteContractAppendix(contract.id, id),
    onAfter: () => refresh(),
  });

  const openCreate = () => setDraft(blankVoDraft());
  const openEdit = (vo: ContractAppendixResponse) =>
    setDraft({
      id: vo.id,
      title: vo.title,
      reason: vo.reason,
      valueDelta: String(vo.valueDelta),
      filePath: vo.filePath,
      originalFileName: vo.originalFileName,
      fileSize: vo.fileSize,
      contentType: vo.contentType,
    });

  const handleFile = async (file: File) => {
    setUploadBusy(true);
    try {
      const { data } = await adminApi.uploadContractAppendixFile(contract.id, file);
      setDraft((prev) =>
        prev ? {
          ...prev,
          filePath: data.filePath,
          originalFileName: data.originalFileName,
          fileSize: data.fileSize,
          contentType: data.contentType,
        } : prev,
      );
      toast({ title: t("contracts.appendix.file") + " ✓" });
    } catch (err) {
      toast({
        variant: "destructive",
        title: getErrorMessage(err) ?? String(err),
      });
    } finally {
      setUploadBusy(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  };

  const submitDraft = async () => {
    if (!draft) return;
    const delta = Number(draft.valueDelta);
    if (!Number.isFinite(delta) || delta === 0) {
      toast({ variant: "destructive", title: t("contracts.appendix.mustBeNonZero") });
      return;
    }
    if (!draft.title.trim() || !draft.reason.trim()) {
      toast({ variant: "destructive", title: t("contracts.documents.missingFields") });
      return;
    }
    setSaving(true);
    try {
      const body: UpsertContractAppendixRequest = {
        title: draft.title.trim(),
        reason: draft.reason.trim(),
        valueDelta: delta,
        filePath: draft.filePath ?? null,
        originalFileName: draft.originalFileName ?? null,
        fileSize: draft.fileSize ?? null,
        contentType: draft.contentType ?? null,
      };
      if (draft.id) {
        await adminApi.updateContractAppendix(contract.id, draft.id, body);
      } else {
        await adminApi.createContractAppendix(contract.id, body);
      }
      setDraft(null);
      await refresh();
    } catch (err) {
      toast({ variant: "destructive", title: getErrorMessage(err) ?? String(err) });
    } finally {
      setSaving(false);
    }
  };

  const runAction = async (vo: ContractAppendixResponse, kind: "submit" | "approve" | "delete") => {
    setPendingActionId(vo.id);
    try {
      if (kind === "submit") await adminApi.submitContractAppendix(contract.id, vo.id);
      else if (kind === "approve") await adminApi.approveContractAppendix(contract.id, vo.id);
      else {
        if (!window.confirm(t("contracts.appendix.confirmDelete"))) return;
        await adminApi.deleteContractAppendix(contract.id, vo.id);
      }
      await refresh();
    } catch (err) {
      toast({ variant: "destructive", title: getErrorMessage(err) ?? String(err) });
    } finally {
      setPendingActionId(null);
    }
  };

  const openReject = (vo: ContractAppendixResponse) => {
    setRejectingVo(vo);
    setRejectNote("");
  };

  const confirmReject = async () => {
    if (!rejectingVo) return;
    if (!rejectNote.trim()) {
      toast({ variant: "destructive", title: t("contracts.appendix.rejectReasonRequired") });
      return;
    }
    setPendingActionId(rejectingVo.id);
    try {
      await adminApi.rejectContractAppendix(contract.id, rejectingVo.id, rejectNote.trim());
      setRejectingVo(null);
      setRejectNote("");
      await refresh();
    } catch (err) {
      toast({ variant: "destructive", title: getErrorMessage(err) ?? String(err) });
    } finally {
      setPendingActionId(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        {deletableIds.length > 0 ? (
          <label className="inline-flex items-center gap-2 text-xs text-slate-600">
            <Checkbox
              checked={bulk.allVisibleSelected ? true : bulk.someVisibleSelected ? "indeterminate" : false}
              onCheckedChange={(v) => bulk.toggleAllVisible(v === true)}
              aria-label={t("common.selectAll")}
            />
            {t("common.selectAll")}
          </label>
        ) : (
          <span />
        )}
        <Button size="sm" onClick={openCreate}>
          <Plus className="mr-1 h-4 w-4" />
          {t("contracts.appendix.new")}
        </Button>
      </div>

      <BulkActionBar
        selectedCount={bulk.selectedIds.size}
        bulkDeleting={bulk.bulkDeleting}
        onClear={bulk.clearSelection}
        onBulkDelete={bulk.handleBulkDelete}
      />

      {rows.length === 0 ? (
        <div className="rounded-lg border border-dashed border-slate-300 p-6 text-center text-sm text-slate-500">
          {t("contracts.appendix.empty")}
        </div>
      ) : (
        <div className="space-y-3">
          {rows.map((vo) => {
            const editable = vo.status === "Draft" || vo.status === "Rejected";
            const canSubmit = vo.status === "Draft";
            const canDecide = vo.status === "Submitted";
            const canDelete = true;
            return (
              <div key={vo.id} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
                <div className="flex flex-wrap items-start justify-between gap-2">
                  <div className="flex min-w-0 gap-3">
                    {canDelete ? (
                      <div className="pt-1" onClick={(e) => e.stopPropagation()}>
                        <Checkbox
                          checked={bulk.selectedIds.has(vo.id)}
                          onCheckedChange={(v) => bulk.toggleOne(vo.id, v === true)}
                          aria-label={`${t("common.selectAll")} · VO#${vo.voNumber}`}
                        />
                      </div>
                    ) : null}
                    <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="text-sm font-semibold text-slate-500">
                        {t("contracts.appendix.number")} #{vo.voNumber}
                      </span>
                      <span className="truncate text-base font-semibold text-slate-900">{vo.title}</span>
                      <Badge className={cn("border", VO_STATUS_STYLES[vo.status])}>
                        {t(`contracts.appendix.status.${vo.status}`)}
                      </Badge>
                    </div>
                    <p className="mt-1 whitespace-pre-wrap text-sm text-slate-600">{vo.reason}</p>
                    <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-500">
                      <span
                        className={cn(
                          "font-semibold",
                          vo.valueDelta > 0 ? "text-emerald-700" : "text-rose-700",
                        )}
                      >
                        {vo.valueDelta > 0 ? "+" : ""}
                        {formatCurrency(vo.valueDelta, lang)} ₫
                        {" "}
                        ({t(vo.valueDelta > 0 ? "contracts.appendix.deltaPositive" : "contracts.appendix.deltaNegative")})
                      </span>
                      {vo.submittedByName ? (
                        <span>
                          {t("contracts.appendix.submittedBy")}: {vo.submittedByName} • {formatDate(vo.submittedAt)}
                        </span>
                      ) : null}
                      {vo.decidedByName ? (
                        <span>
                          {t("contracts.appendix.decidedBy")}: {vo.decidedByName} • {formatDate(vo.decidedAt)}
                        </span>
                      ) : null}
                    </div>
                    {vo.decisionNote ? (
                      <p className="mt-1 text-xs italic text-slate-500">
                        {t("contracts.appendix.decisionNote")}: {vo.decisionNote}
                      </p>
                    ) : null}
                    {vo.filePath ? (
                      <div className="mt-2">
                        <AdminFilePreview url={vo.filePath} fileName={vo.originalFileName} showLabel label={vo.originalFileName ?? t("common.previewFile")} variant="ghost" />
                      </div>
                    ) : null}
                    </div>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {editable ? (
                      <Button size="sm" variant="outline" onClick={() => openEdit(vo)}>
                        <Pencil className="mr-1 h-3 w-3" />
                        {t("common.edit")}
                      </Button>
                    ) : null}
                    {canSubmit ? (
                      <Button
                        size="sm"
                        disabled={pendingActionId === vo.id}
                        onClick={() => runAction(vo, "submit")}
                      >
                        {pendingActionId === vo.id ? (
                          <Loader2 className="mr-1 h-3 w-3 animate-spin" />
                        ) : (
                          <Send className="mr-1 h-3 w-3" />
                        )}
                        {t("contracts.appendix.submit")}
                      </Button>
                    ) : null}
                    {canDecide ? (
                      <>
                        <Button
                          size="sm"
                          disabled={pendingActionId === vo.id}
                          onClick={() => runAction(vo, "approve")}
                          className="bg-emerald-600 hover:bg-emerald-700"
                        >
                          <ShieldCheck className="mr-1 h-3 w-3" />
                          {t("contracts.appendix.approve")}
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={pendingActionId === vo.id}
                          onClick={() => openReject(vo)}
                          className="border-rose-200 text-rose-700 hover:bg-rose-50"
                        >
                          <ShieldX className="mr-1 h-3 w-3" />
                          {t("contracts.appendix.reject")}
                        </Button>
                      </>
                    ) : null}
                    {canDelete ? (
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={pendingActionId === vo.id}
                        onClick={() => runAction(vo, "delete")}
                        className="text-rose-700 hover:bg-rose-50"
                      >
                        <Trash2 className="h-3 w-3" />
                      </Button>
                    ) : null}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}

      <Dialog open={draft !== null} onOpenChange={(open) => (!open ? setDraft(null) : null)}>
        <DialogContent className="w-[95vw] max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {draft?.id ? t("common.edit") : t("contracts.appendix.new")}
            </DialogTitle>
            <DialogDescription>
              {t("contracts.appendix.mustBeNonZero")}
            </DialogDescription>
          </DialogHeader>
          {draft ? (
            <div className="space-y-3">
              <div>
                <Label>{t("contracts.appendix.title")} *</Label>
                <Input
                  value={draft.title}
                  onChange={(e) => setDraft({ ...draft, title: e.target.value })}
                />
              </div>
              <div>
                <Label>{t("contracts.appendix.reason")} *</Label>
                <Textarea
                  rows={3}
                  value={draft.reason}
                  onChange={(e) => setDraft({ ...draft, reason: e.target.value })}
                />
              </div>
              <div>
                <Label>{t("contracts.appendix.valueDelta")} (₫) *</Label>
                <Input
                  type="number"
                  step="1"
                  value={draft.valueDelta}
                  onChange={(e) => setDraft({ ...draft, valueDelta: e.target.value })}
                  placeholder="45000000"
                />
              </div>
              <div>
                <Label>{t("contracts.appendix.file")}</Label>
                <div className="mt-1 flex flex-wrap items-center gap-2">
                  <input
                    ref={fileInputRef}
                    type="file"
                    className="hidden"
                    onChange={(e) => {
                      const f = e.target.files?.[0];
                      if (f) void handleFile(f);
                    }}
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    disabled={uploadBusy}
                    onClick={() => fileInputRef.current?.click()}
                  >
                    {uploadBusy ? <Loader2 className="mr-1 h-3 w-3 animate-spin" /> : <Upload className="mr-1 h-3 w-3" />}
                    {t("contracts.documents.upload")}
                  </Button>
                  {draft.originalFileName ? (
                    <span className="text-xs text-slate-500">
                      {draft.originalFileName} ({formatBytes(draft.fileSize)})
                    </span>
                  ) : null}
                </div>
              </div>
            </div>
          ) : null}
          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="outline" onClick={() => setDraft(null)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={submitDraft} disabled={saving}>
              {saving ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Save className="mr-1 h-4 w-4" />}
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={rejectingVo !== null} onOpenChange={(open) => (!open ? setRejectingVo(null) : null)}>
        <DialogContent className="w-[95vw] max-w-md">
          <DialogHeader>
            <DialogTitle>{t("contracts.appendix.reject")}</DialogTitle>
            <DialogDescription>
              {t("contracts.appendix.rejectReasonRequired")}
            </DialogDescription>
          </DialogHeader>
          <Textarea
            rows={3}
            value={rejectNote}
            onChange={(e) => setRejectNote(e.target.value)}
            placeholder={t("contracts.appendix.rejectReasonRequired")}
          />
          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="outline" onClick={() => setRejectingVo(null)}>
              {t("common.cancel")}
            </Button>
            <Button
              onClick={confirmReject}
              className="bg-rose-600 hover:bg-rose-700"
              disabled={pendingActionId !== null}
            >
              {t("contracts.appendix.reject")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
};

// -------- Documents tab --------

interface DocumentsTabProps {
  contract: ContractResponse;
  rows: ContractAttachmentResponse[];
  refresh: () => Promise<void>;
}

const DocumentsTab = ({ contract, rows, refresh }: DocumentsTabProps) => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [uploadKind, setUploadKind] = useState<ContractAttachmentKind>("Supporting");
  const [label, setLabel] = useState("");
  const [uploading, setUploading] = useState(false);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const visibleIds = useMemo(() => rows.map((a) => a.id), [rows]);
  const bulk = useBulkSelection<number>({
    visibleIds,
    deleteOne: (id) => adminApi.deleteContractAttachment(contract.id, id),
    onAfter: () => refresh(),
  });

  const handleFile = async (file: File) => {
    setUploading(true);
    try {
      await adminApi.uploadContractAttachment(contract.id, file, uploadKind, label.trim() || undefined);
      setLabel("");
      if (fileInputRef.current) fileInputRef.current.value = "";
      await refresh();
    } catch (err) {
      toast({ variant: "destructive", title: getErrorMessage(err) ?? String(err) });
    } finally {
      setUploading(false);
    }
  };

  const handleDelete = async (id: number) => {
    if (!window.confirm(t("contracts.documents.confirmDelete"))) return;
    setDeletingId(id);
    try {
      await adminApi.deleteContractAttachment(contract.id, id);
      await refresh();
    } catch (err) {
      toast({ variant: "destructive", title: getErrorMessage(err) ?? String(err) });
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div className="space-y-4">
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
        <div className="grid gap-3 md:grid-cols-3">
          <div>
            <Label>{t("contracts.documents.typeLabel")}</Label>
            <Select value={uploadKind} onValueChange={(v) => setUploadKind(v as ContractAttachmentKind)}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="SignedScan">{t("contracts.documents.kind.SignedScan")}</SelectItem>
                <SelectItem value="Supporting">{t("contracts.documents.kind.Supporting")}</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label>{t("contracts.documents.label")}</Label>
            <Input value={label} onChange={(e) => setLabel(e.target.value)} placeholder="..." />
          </div>
          <div className="flex items-end">
            <input
              ref={fileInputRef}
              type="file"
              className="hidden"
              onChange={(e) => {
                const f = e.target.files?.[0];
                if (f) void handleFile(f);
              }}
            />
            <Button
              className="w-full"
              disabled={uploading}
              onClick={() => fileInputRef.current?.click()}
            >
              {uploading ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Upload className="mr-1 h-4 w-4" />}
              {t("contracts.documents.upload")}
            </Button>
          </div>
        </div>
      </div>

      {rows.length === 0 ? (
        <div className="rounded-lg border border-dashed border-slate-300 p-6 text-center text-sm text-slate-500">
          {t("contracts.documents.empty")}
        </div>
      ) : (
        <>
          <div className="flex items-center gap-3">
            <label className="inline-flex items-center gap-2 text-xs text-slate-600">
              <Checkbox
                checked={bulk.allVisibleSelected ? true : bulk.someVisibleSelected ? "indeterminate" : false}
                onCheckedChange={(v) => bulk.toggleAllVisible(v === true)}
                aria-label={t("common.selectAll")}
              />
              {t("common.selectAll")}
            </label>
          </div>
          <BulkActionBar
            selectedCount={bulk.selectedIds.size}
            bulkDeleting={bulk.bulkDeleting}
            onClear={bulk.clearSelection}
            onBulkDelete={bulk.handleBulkDelete}
          />
          <div className="space-y-2">
            {rows.map((att) => (
              <div key={att.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
                <div className="flex min-w-0 gap-3">
                  <div className="pt-1" onClick={(e) => e.stopPropagation()}>
                    <Checkbox
                      checked={bulk.selectedIds.has(att.id)}
                      onCheckedChange={(v) => bulk.toggleOne(att.id, v === true)}
                      aria-label={`${t("common.selectAll")} · ${att.originalFileName}`}
                    />
                  </div>
                  <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge
                    className={cn(
                      "border",
                      att.kind === "SignedScan"
                        ? "border-emerald-200 bg-emerald-50 text-emerald-700"
                        : "border-slate-200 bg-slate-50 text-slate-700",
                    )}
                  >
                    {t(`contracts.documents.kind.${att.kind}`)}
                  </Badge>
                  <span className="truncate text-sm font-medium text-slate-800">{att.originalFileName}</span>
                </div>
                <div className="mt-1 flex flex-wrap gap-x-3 text-xs text-slate-500">
                  <span>{formatBytes(att.fileSize)}</span>
                  <span>{formatDate(att.createdAt)}</span>
                  {att.uploadedByName ? <span>{t("contracts.documents.uploadedBy")}: {att.uploadedByName}</span> : null}
                  {att.label ? <span>“{att.label}”</span> : null}
                </div>
                  </div>
                </div>
              <div className="flex gap-2">
                  <AdminFilePreview url={att.filePath} fileName={att.originalFileName} contentType={att.contentType} showLabel />
                <Button
                  size="sm"
                  variant="ghost"
                  disabled={deletingId === att.id}
                  onClick={() => handleDelete(att.id)}
                  className="text-rose-700 hover:bg-rose-50"
                >
                  {deletingId === att.id ? <Loader2 className="h-3 w-3 animate-spin" /> : <Trash2 className="h-3 w-3" />}
                </Button>
              </div>
            </div>
          ))}
          </div>
        </>
      )}
    </div>
  );
};

// -------- Timeline tab --------

const TimelineTab = ({ events }: { events: ContractTimelineEvent[] }) => {
  const { t } = useI18n();
  if (events.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-slate-300 p-6 text-center text-sm text-slate-500">
        {t("contracts.timeline.empty")}
      </div>
    );
  }
  return (
    <ol className="space-y-3">
      {events.map((ev) => (
        <li key={ev.id} className="flex gap-3 rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <div className="mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-slate-100 text-slate-600">
            <History className="h-4 w-4" />
          </div>
          <div className="min-w-0">
            <div className="flex flex-wrap items-baseline gap-x-2 text-sm">
              <span className="font-semibold text-slate-800">{ev.action}</span>
              <span className="text-xs text-slate-500">{new Date(ev.occurredAt).toLocaleString()}</span>
            </div>
            {ev.message ? <p className="mt-0.5 text-sm text-slate-700">{ev.message}</p> : null}
            {ev.userName ? (
              <p className="mt-1 text-xs text-slate-400">
                {t("contracts.timeline.by")}: {ev.userName}
              </p>
            ) : null}
          </div>
        </li>
      ))}
    </ol>
  );
};

// -------- main page --------

type TabId = "info" | "schedule" | "appendices" | "documents" | "timeline";

const ContractDetail = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const params = useParams<{ id: string }>();
  const [searchParams, setSearchParams] = useSearchParams();
  const idNum = Number(params.id);
  const canManage = has(ADMIN_PERMS.contractsManage);

  const [contract, setContract] = useState<ContractResponse | null>(null);
  const [appendices, setAppendices] = useState<ContractAppendixResponse[]>([]);
  const [attachments, setAttachments] = useState<ContractAttachmentResponse[]>([]);
  const [timeline, setTimeline] = useState<ContractTimelineEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [tab, setTab] = useState<TabId>("info");
  const [transitionBusy, setTransitionBusy] = useState<ContractStatus | null>(null);
  const [busyMilestoneId, setBusyMilestoneId] = useState<number | null>(null);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState<ContractEditForm | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);

  const load = useCallback(async () => {
    if (!Number.isFinite(idNum)) return;
    setLoading(true);
    setLoadError(null);
    try {
      const [c, vos, atts, tl] = await Promise.all([
        adminApi.getContract(idNum),
        adminApi.listContractAppendices(idNum).catch(() => ({ data: [] as ContractAppendixResponse[] })),
        adminApi.listContractAttachments(idNum).catch(() => ({ data: [] as ContractAttachmentResponse[] })),
        adminApi.getContractTimeline(idNum).catch(() => ({ data: [] as ContractTimelineEvent[] })),
      ]);
      setContract(c.data);
      setForm(toEditForm(c.data));
      setAppendices(vos.data);
      setAttachments(atts.data);
      setTimeline(tl.data);
    } catch (err) {
      setLoadError(getErrorMessage(err) ?? String(err));
    } finally {
      setLoading(false);
    }
  }, [idNum]);

  useEffect(() => {
    void load();
  }, [load]);

  const refreshContract = useCallback(async () => {
    if (!Number.isFinite(idNum)) return;
    const [c, vos, atts, tl] = await Promise.all([
      adminApi.getContract(idNum),
      adminApi.listContractAppendices(idNum),
      adminApi.listContractAttachments(idNum),
      adminApi.getContractTimeline(idNum),
    ]);
    setContract(c.data);
    setAppendices(vos.data);
    setAttachments(atts.data);
    setTimeline(tl.data);
  }, [idNum]);

  const handleTransition = useCallback(
    async (next: ContractStatus) => {
      if (!contract) return;
      // Client-side gate mirrors the server rule so we can point users at
      // the right tab before they get a 400.
      if (next === "InProgress" && contract.status === "Signed" && !contract.hasSignedScan) {
        toast({
          variant: "destructive",
          title: t("contracts.detail.transition.needScan"),
        });
        setTab("documents");
        return;
      }
      if (next === "Completed") {
        const anyUnpaid = contract.paymentMilestones.some((m) => m.status !== "Paid");
        if (contract.paymentMilestones.length === 0 || anyUnpaid) {
          toast({ variant: "destructive", title: t("contracts.detail.transition.needAllPaid") });
          setTab("schedule");
          return;
        }
      }
      if (next === "Cancelled" && !window.confirm(t("contracts.detail.transition.markCancelled") + "?")) {
        return;
      }
      setTransitionBusy(next);
      try {
        await adminApi.transitionContract(contract.id, next);
        await refreshContract();
      } catch (err) {
        toast({ variant: "destructive", title: getErrorMessage(err) ?? String(err) });
      } finally {
        setTransitionBusy(null);
      }
    },
    [contract, refreshContract, t, toast],
  );

  const handleMilestoneStatus = useCallback(
    async (milestoneId: number, nextStatus: PaymentMilestoneStatus) => {
      if (!contract) return;
      setBusyMilestoneId(milestoneId);
      try {
        await adminApi.updateMilestoneStatus(contract.id, milestoneId, nextStatus);
        await refreshContract();
      } catch (err) {
        toast({ variant: "destructive", title: getErrorMessage(err) ?? String(err) });
      } finally {
        setBusyMilestoneId(null);
      }
    },
    [contract, refreshContract, toast],
  );

  const beginEdit = useCallback(async () => {
    if (!contract) return;
    setForm(toEditForm(contract));
    setFormError(null);
    setTab("info");
    if (customers.length === 0) {
      try {
        const { data } = await adminApi.listCustomers({ pageSize: 200 });
        setCustomers(data.items);
      } catch (err) {
        toast({ variant: "destructive", title: getErrorMessage(err) ?? t("common.error") });
        return;
      }
    }
    setEditing(true);
  }, [contract, customers.length, t, toast]);

  useEffect(() => {
    if (searchParams.get("edit") !== "true" || !contract || !canManage || editing) return;
    const next = new URLSearchParams(searchParams);
    next.delete("edit");
    setSearchParams(next, { replace: true });
    void beginEdit();
  }, [beginEdit, canManage, contract, editing, searchParams, setSearchParams]);

  const cancelEdit = useCallback(() => {
    if (contract) setForm(toEditForm(contract));
    setFormError(null);
    setEditing(false);
  }, [contract]);

  const saveEdit = useCallback(async () => {
    if (!contract || !form) return;
    setFormError(null);
    if (form.signedDate && form.startDate && form.startDate < form.signedDate) {
      setFormError(t("form.invalidDateRange"));
      setTab("info");
      return;
    }
    if (form.startDate && form.endDate && form.endDate < form.startDate) {
      setFormError(t("form.invalidDateRange"));
      setTab("info");
      return;
    }
    const milestoneTotal = form.milestones.reduce((sum, milestone) => sum + milestone.percentValue, 0);
    if (form.milestones.length > 0 && Math.abs(milestoneTotal - 100) > 0.01) {
      setFormError(t("contracts.milestoneSumInvalid"));
      setTab("schedule");
      return;
    }
    if (form.milestones.some((milestone) => !milestone.name.trim())) {
      setFormError(t("contracts.milestoneNameRequired"));
      setTab("schedule");
      return;
    }
    const paymentMilestones: ContractPaymentMilestoneRequest[] = form.milestones.map((milestone, index) => ({
      order: index + 1,
      name: milestone.name.trim(),
      percentValue: milestone.percentValue,
      dueDate: toIsoTimestamp(milestone.dueDate),
      status: milestone.status,
      note: milestone.note.trim() || null,
    }));
    const payload: UpsertContractRequest = {
      contractNumber: form.contractNumber.trim() || null,
      customerId: form.customerId,
      opportunityId: contract.opportunityId,
      quoteId: contract.quoteId,
      status: contract.status,
      signedDate: toIsoTimestamp(form.signedDate),
      startDate: toIsoTimestamp(form.startDate),
      endDate: toIsoTimestamp(form.endDate),
      value: Number.isFinite(form.value) ? Math.max(0, form.value) : 0,
      scopeOfWork: form.scopeOfWork.trim() || null,
      note: form.note.trim() || null,
      paymentMilestones,
    };
    setSaving(true);
    try {
      const { data } = await adminApi.updateContract(contract.id, payload);
      setContract(data);
      setForm(toEditForm(data));
      setEditing(false);
      toast({ title: t("form.updated") });
      await refreshContract();
    } catch (err) {
      setFormError(getErrorMessage(err) ?? t("common.error"));
    } finally {
      setSaving(false);
    }
  }, [contract, form, refreshContract, t, toast]);

  if (!Number.isFinite(idNum)) {
    return (
      <AdminLayout>
        <PageError message={t("contracts.detail.notFound")} />
      </AdminLayout>
    );
  }

  if (loading) {
    return (
      <AdminLayout>
        <PageLoading />
      </AdminLayout>
    );
  }

  if (loadError || !contract) {
    return (
      <AdminLayout>
        <PageError message={loadError ?? t("contracts.detail.notFound")} />
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="space-y-4">
        <ContractHeader
          contract={contract}
          canEdit={canManage}
          editing={editing}
          saving={saving}
          onEditInfo={() => void beginEdit()}
          onCancelEdit={cancelEdit}
          onSave={() => void saveEdit()}
          onTransition={handleTransition}
          transitionBusy={transitionBusy}
        />

        <Tabs value={tab} onValueChange={(v) => setTab(v as TabId)}>
          {/* Horizontal scroll on narrow viewports keeps the 5 tabs in one
              readable row instead of wrapping onto two rows with awkward
              orphan groups. The container hides the scrollbar visually but
              stays swipeable on touch devices. */}
          <div className="overflow-x-auto pb-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
            <TabsList className="inline-flex w-max min-w-full justify-start">
              <TabsTrigger value="info">{t("contracts.detail.tab.info")}</TabsTrigger>
              <TabsTrigger value="schedule">
                {t("contracts.detail.tab.schedule")}
                {contract.paymentMilestones.length > 0 ? (
                  <span className="ml-1 text-xs text-slate-400">({contract.paymentMilestones.length})</span>
                ) : null}
              </TabsTrigger>
              <TabsTrigger value="appendices">
                {t("contracts.detail.tab.appendices")}
                {appendices.length > 0 ? (
                  <span className="ml-1 text-xs text-slate-400">({appendices.length})</span>
                ) : null}
              </TabsTrigger>
              <TabsTrigger value="documents">
                {t("contracts.detail.tab.documents")}
                {attachments.length > 0 ? (
                  <span className="ml-1 text-xs text-slate-400">({attachments.length})</span>
                ) : null}
              </TabsTrigger>
              <TabsTrigger value="timeline">{t("contracts.detail.tab.timeline")}</TabsTrigger>
            </TabsList>
          </div>

          <TabsContent value="info" className="mt-4">
            {editing && form ? (
              <div data-testid="contract-inline-edit-form">
                <EditInfoTab form={form} customers={customers} error={formError} onChange={setForm} />
              </div>
            ) : <InfoTab contract={contract} />}
          </TabsContent>
          <TabsContent value="schedule" className="mt-4">
            {editing && form ? (
              <div className="space-y-3">
                <EditScheduleTab value={form.value} milestones={form.milestones} onChange={(milestones) => setForm({ ...form, milestones })} />
                {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
              </div>
            ) : (
              <ScheduleTab contract={contract} onMilestoneStatus={handleMilestoneStatus} busyMilestoneId={busyMilestoneId} />
            )}
          </TabsContent>
          <TabsContent value="appendices" className="mt-4">
            <VoTab contract={contract} rows={appendices} refresh={refreshContract} />
          </TabsContent>
          <TabsContent value="documents" className="mt-4">
            <DocumentsTab contract={contract} rows={attachments} refresh={refreshContract} />
          </TabsContent>
          <TabsContent value="timeline" className="mt-4">
            <TimelineTab events={timeline} />
          </TabsContent>
        </Tabs>
      </div>
    </AdminLayout>
  );
};

export default ContractDetail;
