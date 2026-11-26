import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useRequestGuard } from "@/hooks/useRequestGuard";
import { collectLookupPages } from "@/lib/pagination";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { FieldScope } from "@/components/ui/field-scope";
import { useConfirm } from "@/components/ui/confirmation";
import { personOption, type PersonIdentity } from "@/lib/people";
import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  BadgeCheck,
  Building2,
  CheckCircle2,
  CircleDashed,
  ClipboardCheck,
  Clock,
  Droplets,
  Eye,
  FileCheck2,
  FilePlus2,
  FileText,
  FileUp,
  Filter,
  Flame,
  Footprints,
  History,
  Leaf,
  ListChecks,
  Pencil,
  Plus,
  RefreshCcw,
  Search,
  Send,
  ShieldAlert,
  ShieldCheck,
  Trash2,
  XCircle,
  Zap,
  type LucideIcon,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminDocumentUpload from "@/components/admin/AdminDocumentUpload";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import LegalActivationWizard from "@/components/admin/LegalActivationWizard";
import LegalChecklistTemplates from "@/components/admin/LegalChecklistTemplates";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { newIdempotencyKey } from "@/lib/api";
import { isManagedDocumentPath } from "@/lib/url";
import { PageLoading, PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SearchableSelect } from "@/components/ui/searchable-select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import {
  adminApi,
  PERMIT_STATUSES,
  type CreatePermitChecklistItemRequest,
  type DepartmentWorkTaskResponse,
  type OperationalProjectListItemResponse,
  type MasterDataOption,
  type PermitChecklistItemResponse,
  type PermitChecklistHistoryResponse,
  type PermitChecklistListParams,
  type PermitChecklistRiskSummary,
  type PermitDocumentKind,
  type PermitStatus,
  type UpdatePermitChecklistItemRequest,
} from "@/services/adminApi";
import { formatDate as formatDateInZone } from "@/lib/displaySettings";

// ------------------------------- helpers -------------------------------

const PERMIT_STATUS_ICON: Record<PermitStatus, LucideIcon> = {
  NotStarted: CircleDashed,
  Preparing: Pencil,
  Submitted: Send,
  UnderReview: Clock,
  NeedMoreDocs: FilePlus2,
  Issued: BadgeCheck,
  Rejected: XCircle,
  Expired: ShieldAlert,
};

const permitTypeIcon = (code?: string | null): LucideIcon => {
  const value = (code ?? "").toLowerCase();
  if (value.includes("pccc") || value.includes("fire")) return Flame;
  if (value.includes("dien") || value.includes("electric")) return Zap;
  if (value.includes("moi-truong") || value.includes("environment")) return Leaf;
  if (value.includes("nuoc") || value.includes("water")) return Droplets;
  if (value.includes("via-he")) return Footprints;
  if (value.includes("an-toan") || value.includes("safety")) return ShieldCheck;
  if (value.includes("hoan-cong") || value.includes("completion")) return ClipboardCheck;
  if (value.includes("gpxd") || value.includes("xay-dung") || value.includes("building"))
    return Building2;
  return FileText;
};

const STATUS_BADGE: Record<PermitStatus, string> = {
  NotStarted: "border-border bg-muted/30 text-foreground",
  Preparing: "border-info/30 bg-info-soft text-info-strong",
  Submitted: "border-info/40 bg-info-soft text-info-strong",
  UnderReview: "border-warning/40 bg-warning-soft text-warning-strong",
  NeedMoreDocs: "border-warning/40 bg-warning-soft text-warning-strong",
  Issued: "border-success/30 bg-success-soft text-success-strong",
  Rejected: "border-destructive/20 bg-destructive/10 text-destructive",
  Expired: "border-destructive/20 bg-destructive/10 text-destructive",
};

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return formatDateInZone(iso, lang);
  } catch {
    return iso;
  }
};

const permitTypeTone = (code?: string | null) => {
  const value = (code ?? "").toLowerCase();
  if (value.includes("pccc") || value.includes("fire"))
    return "border-warning/40 bg-warning-soft text-warning-strong";
  if (value.includes("dien") || value.includes("electric"))
    return "border-warning/40 bg-warning-soft text-warning-strong";
  if (value.includes("moi-truong") || value.includes("environment"))
    return "border-success/30 bg-success-soft text-success-strong";
  if (value.includes("nuoc") || value.includes("water"))
    return "border-info/40 bg-info-soft text-info-strong";
  if (value.includes("via-he") || value.includes("an-toan") || value.includes("safety"))
    return "border-success/40 bg-success-soft text-success-strong";
  if (value.includes("hoan-cong") || value.includes("completion"))
    return "border-info/40 bg-info-soft text-info-strong";
  if (value.includes("gpxd") || value.includes("xay-dung") || value.includes("building"))
    return "border-info/30 bg-info-soft text-info-strong";
  return "border-border/70 bg-muted/60 text-muted-foreground";
};

const PermitTypeGlyph = ({ code, className }: { code?: string | null; className?: string }) => {
  const Icon = permitTypeIcon(code);
  return (
    <span
      className={cn(
        "flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border",
        permitTypeTone(code),
        className,
      )}
      aria-hidden="true"
    >
      <Icon className="h-4 w-4" />
    </span>
  );
};

const PermitStatusBadge = ({ status, className }: { status: PermitStatus; className?: string }) => {
  const { t } = useI18n();
  const Icon = PERMIT_STATUS_ICON[status];
  return (
    <Badge
      variant="outline"
      className={cn("gap-1 whitespace-nowrap", STATUS_BADGE[status], className)}
    >
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      {t(`permits.status.${status}`)}
    </Badge>
  );
};

const nextActionIcon = (tone: NextTone): LucideIcon => {
  if (tone === "danger") return AlertTriangle;
  if (tone === "warning") return Clock;
  if (tone === "info") return Clock;
  if (tone === "success") return CheckCircle2;
  return CircleDashed;
};

const toDateInputValue = (iso?: string | null) => (iso ? iso.slice(0, 10) : "");

type NextTone = "danger" | "warning" | "info" | "success" | "muted";
const NEXT_TONE: Record<NextTone, string> = {
  danger: "text-destructive",
  warning: "text-warning-strong",
  info: "text-info-strong",
  success: "text-success-strong",
  muted: "text-muted-foreground",
};
const NEXT_BANNER: Record<NextTone, string> = {
  danger: "border-destructive/20 bg-destructive/10 text-danger-strong",
  warning: "border-warning/40 bg-warning-soft text-warning-strong",
  info: "border-info/30 bg-info-soft text-info-strong",
  success: "border-success/30 bg-success-soft text-success-strong",
  muted: "border-border bg-muted/30 text-foreground",
};

const permitNextStep = (
  row: PermitChecklistItemResponse,
  t: (key: string, params?: Record<string, string | number>) => string,
  lang: string,
): { text: string; tone: NextTone } => {
  if (row.status === "Expired") return { text: t("permits.next.expired"), tone: "danger" };
  if (row.status === "Rejected") return { text: t("permits.next.rejected"), tone: "danger" };
  if (row.status === "NeedMoreDocs")
    return { text: t("permits.next.needMoreDocs"), tone: "warning" };
  if (row.status === "Issued") {
    if (row.isExpiringSoon && row.expiresAt)
      return {
        text: t("permits.next.renewSoon", { date: formatDate(row.expiresAt, lang) }),
        tone: "warning",
      };
    if (row.expiresAt)
      return {
        text: t("permits.next.validUntil", { date: formatDate(row.expiresAt, lang) }),
        tone: "success",
      };
    return { text: t("permits.next.valid"), tone: "success" };
  }
  if (row.status === "Submitted" || row.status === "UnderReview")
    return { text: t("permits.next.underReview"), tone: "info" };
  if (row.status === "NotStarted" || row.status === "Preparing") {
    if (row.isOverdue) return { text: t("permits.next.submitOverdue"), tone: "danger" };
    return { text: t("permits.next.notSubmitted"), tone: "info" };
  }
  if (row.isOverdue) return { text: t("permits.next.submitOverdue"), tone: "danger" };
  return { text: "", tone: "muted" };
};

const WORK_TASK_STATUSES = [
  "Planned",
  "InProgress",
  "Completed",
  "Verified",
  "Cancelled",
  "Disabled",
] as const;
const taskStatusLabel = (
  t: (key: string, params?: Record<string, string | number>) => string,
  status?: string | null,
) =>
  status && (WORK_TASK_STATUSES as readonly string[]).includes(status)
    ? t(`workPackages.taskStatus.${status}`)
    : (status ?? "—");

const PERMIT_HISTORY_ACTIONS = [
  "Updated",
  "StatusChanged",
  "SubmittedPackageUploaded",
  "IssuedPermitUploaded",
  "SeededLegalScenario",
] as const;
const historyActionLabel = (
  t: (key: string, params?: Record<string, string | number>) => string,
  action: string,
) =>
  (PERMIT_HISTORY_ACTIONS as readonly string[]).includes(action)
    ? t(`permits.historyAction.${action}`)
    : t("permits.historyAction.Other");

const permitStatusLabel = (
  t: (key: string, params?: Record<string, string | number>) => string,
  status?: string | null,
) =>
  status && PERMIT_STATUSES.includes(status as PermitStatus)
    ? t(`permits.status.${status}`)
    : (status ?? "—");

const fileDisplayName = (path?: string | null) => {
  if (!path) return null;
  const name = path.split("/").pop() ?? path;
  try {
    return decodeURIComponent(name);
  } catch {
    return name;
  }
};

/** Local YYYY-MM-DD → `${YYYY-MM-DD}T00:00:00.000Z`. Keeps the picked
 * calendar date intact across the UTC+7 boundary. */
const toUtcMidnight = (yyyyMmDd: string): string => (yyyyMmDd ? `${yyyyMmDd}T00:00:00.000Z` : "");

interface UserOption extends PersonIdentity {
  id: number;
  fullName: string;
}

interface PermitForm {
  projectId: number | null;
  permitTypeCode: string;
  status: PermitStatus;
  issuingAgency: string;
  ownerUserId: number | null;
  targetDeadline: string;
  submittedAt: string;
  issuedAt: string;
  expiresAt: string;
  note: string;
}

const emptyForm = (): PermitForm => ({
  projectId: null,
  permitTypeCode: "",
  status: "NotStarted",
  issuingAgency: "",
  ownerUserId: null,
  targetDeadline: "",
  submittedAt: "",
  issuedAt: "",
  expiresAt: "",
  note: "",
});

const formFrom = (row: PermitChecklistItemResponse): PermitForm => ({
  projectId: row.operationalProjectId,
  permitTypeCode: row.permitTypeCode,
  status: row.status,
  issuingAgency: row.issuingAgency ?? "",
  ownerUserId: row.ownerUserId ?? null,
  targetDeadline: toDateInputValue(row.targetDeadline),
  submittedAt: toDateInputValue(row.submittedAt),
  issuedAt: toDateInputValue(row.issuedAt),
  expiresAt: toDateInputValue(row.expiresAt),
  note: row.note ?? "",
});

// -------------------------------- page ---------------------------------

const AdminPermits = () => {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const confirmAction = useConfirm();
  const [searchParams] = useSearchParams();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.permitsManage);
  const canPickUser = has(ADMIN_PERMS.users);

  // list state
  const [rows, setRows] = useState<PermitChecklistItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [risk, setRisk] = useState<PermitChecklistRiskSummary>({
    overdue: 0,
    dueSoon: 0,
    expiringSoon: 0,
    totalOpen: 0,
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const [search, setSearch] = useState("");
  const settledSearch = useDebouncedValue(search);
  const [projectId, setProjectId] = useState<number | null>(() => {
    const value = Number(searchParams.get("operationalProjectId"));
    return Number.isInteger(value) && value > 0 ? value : null;
  });
  const [permitTypeCode, setPermitTypeCode] = useState<string>("");
  const [status, setStatus] = useState<PermitStatus | "">("");
  const [ownerUserId, setOwnerUserId] = useState<number | null>(null);
  const [approverUserId, setApproverUserId] = useState<number | null>(null);
  const [flag, setFlag] = useState<"" | "overdue" | "dueSoon" | "expiringSoon">("");
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // lookups
  const [projects, setProjects] = useState<OperationalProjectListItemResponse[]>([]);
  const [permitTypes, setPermitTypes] = useState<MasterDataOption[]>([]);
  const [users, setUsers] = useState<UserOption[]>([]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [projResp, typesResp] = await Promise.all([
          collectLookupPages((page) =>
            adminApi.listOperationalProjects({ pageSize: 200, page }),
          ).catch(() => ({
            data: { total: 0, page: 1, pageSize: 200, items: [] },
          })),
          adminApi.getMasterDataOptions("permit_type", lang).catch(() => ({ data: [] })),
        ]);
        if (cancelled) return;
        setProjects(projResp.data.items ?? []);
        setPermitTypes((typesResp.data ?? []).filter((o) => o.isActive));

        if (canPickUser) {
          try {
            const { data } = await adminApi.getPeopleDirectory();
            if (!cancelled) {
              setUsers(
                (data.items ?? []).map((u) => ({
                  ...u,
                  id: u.id,
                  fullName: u.fullName ?? u.email ?? `#${u.id}`,
                })),
              );
            }
          } catch {
            // no permission → filter stays hidden
          }
        }
      } catch {
        // non-fatal
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canPickUser, lang]);

  const startListRequest = useRequestGuard();
  const fetchList = useCallback(async () => {
    const isCurrent = startListRequest();
    setLoading(true);
    setError(null);
    try {
      const params: PermitChecklistListParams = { page, pageSize };
      if (settledSearch.trim()) params.search = settledSearch.trim();
      if (projectId != null) params.operationalProjectId = projectId;
      if (permitTypeCode) params.permitTypeCode = permitTypeCode;
      if (status) params.status = status;
      if (ownerUserId != null) params.ownerUserId = ownerUserId;
      if (approverUserId != null) params.approverUserId = approverUserId;
      if (flag === "overdue") params.overdue = true;
      if (flag === "dueSoon") params.dueSoon = true;
      if (flag === "expiringSoon") params.expiringSoon = true;
      const { data } = await adminApi.listPermits(params);
      if (!isCurrent()) return;
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
      setRisk(data.risk);
    } catch (err) {
      if (!isCurrent()) return;
      setError(uiError(err));
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      if (isCurrent()) setLoading(false);
    }
  }, [
    startListRequest,
    page,
    settledSearch,
    projectId,
    permitTypeCode,
    status,
    ownerUserId,
    approverUserId,
    flag,
    uiError,
    toast,
    t,
  ]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const hasActiveFilter =
    search.trim().length > 0 ||
    projectId != null ||
    permitTypeCode !== "" ||
    status !== "" ||
    ownerUserId != null ||
    approverUserId != null ||
    flag !== "";

  const advancedFilterCount = [
    projectId != null,
    permitTypeCode !== "",
    status !== "",
    ownerUserId != null,
    approverUserId != null,
    flag !== "",
  ].filter(Boolean).length;
  const projectOptions = useMemo(
    () =>
      projects.map((p) => ({
        value: String(p.id),
        label: `${p.code} — ${p.name}`,
      })),
    [projects],
  );
  const userOptions = useMemo(() => users.map((u) => personOption(u, t)), [users, t]);

  const resetFilters = () => {
    setSearch("");
    setProjectId(null);
    setPermitTypeCode("");
    setStatus("");
    setOwnerUserId(null);
    setApproverUserId(null);
    setFlag("");
    setPage(1);
  };

  // -------- CRUD dialogs --------
  const [dialogMode, setDialogMode] = useState<"create" | "edit" | null>(null);
  const [editing, setEditing] = useState<PermitChecklistItemResponse | null>(null);
  const [viewing, setViewing] = useState<PermitChecklistItemResponse | null>(null);
  const [permitHistory, setPermitHistory] = useState<PermitChecklistHistoryResponse[]>([]);
  const [viewTask, setViewTask] = useState<DepartmentWorkTaskResponse | null>(null);
  const [taskProgress, setTaskProgress] = useState(1);
  const [taskRejectReason, setTaskRejectReason] = useState("");
  const [taskSaving, setTaskSaving] = useState(false);
  const [setupProjectId, setSetupProjectId] = useState<number | null>(null);
  const openedPermitIdRef = useRef<number | null>(null);
  const [pendingDelete, setPendingDelete] = useState<PermitChecklistItemResponse | null>(null);
  const [form, setForm] = useState<PermitForm | null>(null);
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    const permitId = Number(searchParams.get("permitId"));
    if (!Number.isInteger(permitId) || permitId <= 0 || openedPermitIdRef.current === permitId)
      return;
    openedPermitIdRef.current = permitId;
    void adminApi
      .getPermit(permitId)
      .then(({ data }) => setViewing(data))
      .catch((err) =>
        toast({
          title: t("common.error"),
          description: uiError(err),
          variant: "destructive",
        }),
      );
  }, [searchParams, t, toast, uiError]);

  useEffect(() => {
    if (!viewing) {
      setPermitHistory([]);
      setViewTask(null);
      setTaskRejectReason("");
      return;
    }
    let cancelled = false;
    void Promise.all([
      adminApi
        .getPermitHistory(viewing.id)
        .then(({ data }) => data)
        .catch(() => []),
      viewing.workPackageId && viewing.workTaskId
        ? adminApi
            .getWorkPackage(viewing.workPackageId)
            .then(({ data }) => data.tasks.find((task) => task.id === viewing.workTaskId) ?? null)
            .catch(() => null)
        : Promise.resolve(null),
    ]).then(([history, task]) => {
      if (cancelled) return;
      setPermitHistory(history);
      setViewTask(task);
      setTaskProgress(Math.max(1, Math.min(99, task?.progressPercent || 1)));
    });
    return () => {
      cancelled = true;
    };
  }, [viewing]);

  const refreshViewedPermit = async () => {
    if (!viewing) return;
    const [{ data: permit }, task] = await Promise.all([
      adminApi.getPermit(viewing.id),
      viewing.workPackageId && viewing.workTaskId
        ? adminApi
            .getWorkPackage(viewing.workPackageId)
            .then(({ data }) => data.tasks.find((item) => item.id === viewing.workTaskId) ?? null)
        : Promise.resolve(null),
    ]);
    setViewing(permit);
    setViewTask(task);
    setTaskProgress(Math.max(1, Math.min(99, task?.progressPercent || 1)));
    await fetchList();
  };

  const updateViewedTaskProgress = async (complete: boolean) => {
    if (!viewing?.workPackageId || !viewTask) return;
    if (
      complete &&
      viewTask.hasSubtasks &&
      !(await confirmAction(t("workPackages.confirm.completeParent"), {
        destructive: false,
        confirmLabel: t("workPackages.action.completeTask"),
      }))
    )
      return;
    setTaskSaving(true);
    try {
      await adminApi.updateWorkTaskProgress(
        viewing.workPackageId,
        viewTask.id,
        {
          progressPercent: complete ? 100 : taskProgress,
          status: complete ? "Completed" : "InProgress",
          confirmSubtasksCompletion: complete && viewTask.hasSubtasks,
          rowVersion: viewTask.rowVersion,
        },
        newIdempotencyKey(),
      );
      await refreshViewedPermit();
    } catch (cause) {
      toast({
        title: t("common.error"),
        description: uiError(cause),
        variant: "destructive",
      });
    } finally {
      setTaskSaving(false);
    }
  };

  const decideViewedTask = async (approve: boolean) => {
    if (!viewing?.workPackageId || !viewTask || (!approve && !taskRejectReason.trim())) return;
    setTaskSaving(true);
    try {
      if (approve) {
        await adminApi.verifyWorkTask(
          viewing.workPackageId,
          viewTask.id,
          viewTask.rowVersion,
          newIdempotencyKey(),
        );
        toast({ title: t("workPackages.toast.verified") });
      } else {
        await adminApi.rejectWorkTask(
          viewing.workPackageId,
          viewTask.id,
          viewTask.rowVersion,
          taskRejectReason.trim(),
          newIdempotencyKey(),
        );
        setTaskRejectReason("");
        toast({ title: t("workPackages.toast.returned") });
      }
      await refreshViewedPermit();
    } catch (cause) {
      toast({
        title: t("common.error"),
        description: uiError(cause),
        variant: "destructive",
      });
    } finally {
      setTaskSaving(false);
    }
  };

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm());
    setFormError(null);
    setDialogMode("create");
  };

  const openEdit = (row: PermitChecklistItemResponse) => {
    setEditing(row);
    setForm(formFrom(row));
    setFormError(null);
    setDialogMode("edit");
  };

  const closeForm = () => {
    setDialogMode(null);
    setEditing(null);
    setForm(null);
    setFormError(null);
  };

  const submitForm = async () => {
    if (!form || !dialogMode) return;
    setFormError(null);
    if (dialogMode === "create" && (!form.projectId || !form.permitTypeCode)) {
      setFormError(t("permits.form.projectTypeRequired"));
      return;
    }
    setSaving(true);
    try {
      if (dialogMode === "create") {
        const payload: CreatePermitChecklistItemRequest = {
          operationalProjectId: form.projectId!,
          permitTypeCode: form.permitTypeCode,
          status: form.status,
          issuingAgency: form.issuingAgency.trim() || null,
          ownerUserId: form.ownerUserId,
          targetDeadline: toUtcMidnight(form.targetDeadline) || null,
          submittedAt: toUtcMidnight(form.submittedAt) || null,
          issuedAt: toUtcMidnight(form.issuedAt) || null,
          expiresAt: toUtcMidnight(form.expiresAt) || null,
          note: form.note.trim() || null,
        };
        await adminApi.createPermit(payload);
        toast({ title: t("permits.created") });
        closeForm();
        setPage(1);
        await fetchList();
        return;
      }
      if (!editing) return;
      // Compute an explicit patch payload — Clear* flags are used only when
      // the field was previously set and the operator wiped it clean.
      const payload: UpdatePermitChecklistItemRequest = { status: form.status };
      const nextAgency = form.issuingAgency.trim();
      if (nextAgency !== (editing.issuingAgency ?? "")) {
        if (nextAgency.length === 0) payload.clearIssuingAgency = true;
        else payload.issuingAgency = nextAgency;
      }
      if ((form.ownerUserId ?? null) !== (editing.ownerUserId ?? null)) {
        if (form.ownerUserId == null) payload.clearOwner = true;
        else payload.ownerUserId = form.ownerUserId;
      }
      applyDatePatch("targetDeadline", form.targetDeadline, editing.targetDeadline, payload);
      applyDatePatch("submittedAt", form.submittedAt, editing.submittedAt, payload);
      applyDatePatch("issuedAt", form.issuedAt, editing.issuedAt, payload);
      applyDatePatch("expiresAt", form.expiresAt, editing.expiresAt, payload);
      const nextNote = form.note.trim();
      if (nextNote !== (editing.note ?? "")) {
        if (nextNote.length === 0) payload.clearNote = true;
        else payload.note = nextNote;
      }

      await adminApi.updatePermit(editing.id, payload);
      toast({ title: t("permits.updated") });
      closeForm();
      await fetchList();
    } catch (err) {
      setFormError(uiError(err));
    } finally {
      setSaving(false);
    }
  };

  const deletePermit = async () => {
    if (!pendingDelete) return;
    setDeleting(true);
    try {
      await adminApi.deletePermit(pendingDelete.id);
      setPendingDelete(null);
      toast({ title: t("permits.deleted") });
      if (rows.length === 1 && page > 1) setPage((current) => current - 1);
      else await fetchList();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      setDeleting(false);
    }
  };

  const uploadPermitFile = async (id: number, kind: PermitDocumentKind, file: File) => {
    const { data } = await adminApi.uploadPermitDocument(id, kind, file);
    const path = kind === "SubmittedPackage" ? data.submittedFilePath : data.issuedFilePath;
    if (!path) throw new Error(t("common.documentUpload.error"));
    return path;
  };

  const refreshPermitAfterUpload = async (id: number) => {
    const { data } = await adminApi.getPermit(id);
    setEditing((current) => (current?.id === id ? data : current));
    setViewing((current) => (current?.id === id ? data : current));
    await fetchList();
    toast({ title: t("permits.document.uploaded") });
  };

  const applyDatePatch = (
    key: "targetDeadline" | "submittedAt" | "issuedAt" | "expiresAt",
    nextLocal: string,
    prev: string | null | undefined,
    payload: UpdatePermitChecklistItemRequest,
  ) => {
    const prevLocal = toDateInputValue(prev);
    if (nextLocal === prevLocal) return;
    const clearKey =
      key === "targetDeadline"
        ? "clearTargetDeadline"
        : key === "submittedAt"
          ? "clearSubmittedAt"
          : key === "issuedAt"
            ? "clearIssuedAt"
            : "clearExpiresAt";
    if (nextLocal.length === 0) {
      (payload as Record<string, unknown>)[clearKey] = true;
    } else {
      (payload as Record<string, unknown>)[key] = toUtcMidnight(nextLocal);
    }
  };

  const viewingNext = viewing ? permitNextStep(viewing, t, lang) : null;
  const viewingPermitReady =
    viewing != null &&
    viewing.status === "Issued" &&
    Boolean(viewing.issuedAt) &&
    Boolean(viewing.issuedFilePath);

  // ----------------------------- render -----------------------------

  return (
    <AdminLayout>
      <div className="space-y-4">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex items-center gap-3">
            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl border border-primary/20 bg-primary/5 text-primary">
              <ShieldCheck className="h-5 w-5" aria-hidden="true" />
            </span>
            <div className="flex flex-col gap-1">
              <h1 className="text-foreground text-2xl font-semibold">{t("permits.title")}</h1>
              <p className="text-sm text-muted-foreground">{t("permits.subtitle")}</p>
            </div>
          </div>
          {canManage ? (
            <div className="flex w-full flex-col items-end gap-1 sm:w-auto">
              <Button
                size="sm"
                className="w-full sm:w-auto"
                disabled={!projectId}
                aria-describedby={!projectId ? "permit-setup-hint" : undefined}
                onClick={() => projectId && setSetupProjectId(projectId)}
              >
                <Plus className="mr-1 h-4 w-4" />
                {t("permits.setupLegal")}
              </Button>
              {!projectId ? (
                <p
                  id="permit-setup-hint"
                  className="max-w-64 text-right text-xs text-muted-foreground"
                >
                  {t("permits.setupLegalHint")}
                </p>
              ) : null}
            </div>
          ) : null}
        </header>

        {/* Risk register */}
        {has("permit.templates.view") ? <LegalChecklistTemplates /> : null}
        <section className="grid grid-cols-2 gap-2 md:grid-cols-4">
          <RiskCard
            title={t("permits.risk.overdue")}
            value={loading ? "—" : risk.overdue}
            tone="rose"
            icon={<ShieldAlert className="h-4 w-4" />}
            onClick={() => {
              setFlag("overdue");
              setPage(1);
            }}
            active={flag === "overdue"}
          />
          <RiskCard
            title={t("permits.risk.dueSoon")}
            value={loading ? "—" : risk.dueSoon}
            tone="amber"
            icon={<Clock className="h-4 w-4" />}
            onClick={() => {
              setFlag("dueSoon");
              setPage(1);
            }}
            active={flag === "dueSoon"}
          />
          <RiskCard
            title={t("permits.risk.expiringSoon")}
            value={loading ? "—" : risk.expiringSoon}
            tone="orange"
            icon={<AlertTriangle className="h-4 w-4" />}
            onClick={() => {
              setFlag("expiringSoon");
              setPage(1);
            }}
            active={flag === "expiringSoon"}
          />
          <RiskCard
            title={t("permits.risk.open")}
            value={loading ? "—" : risk.totalOpen}
            tone="slate"
            icon={<FileCheck2 className="h-4 w-4" />}
          />
        </section>

        <section className="rounded-lg border border-border bg-card p-3 shadow-sm">
          <div className="grid gap-3 lg:grid-cols-4">
            <FieldScope className="lg:col-span-2">
              <Label className="text-xs">{t("permits.filter.searchLabel")}</Label>
              <div className="relative mt-1">
                <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  className="pl-8"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder={t("permits.filter.search")}
                />
              </div>
            </FieldScope>
            <div className="flex flex-wrap items-end gap-2 lg:col-span-2 lg:justify-end">
              <Button
                variant="outline"
                size="sm"
                onClick={() => void fetchList()}
                disabled={loading}
              >
                <RefreshCcw className={cn("mr-1 h-4 w-4", loading && "animate-spin")} />
                {t("common.refresh")}
              </Button>
              <Button variant="ghost" size="sm" onClick={resetFilters} disabled={loading}>
                <Filter className="mr-1 h-4 w-4" />
                {t("common.reset")}
              </Button>
              <span className="text-xs text-muted-foreground">
                {t("permits.total").replace("{count}", String(total))}
              </span>
            </div>
          </div>
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="mt-3 w-full xl:hidden"
            aria-expanded={filtersOpen}
            aria-controls="permit-filters-more"
            onClick={() => setFiltersOpen((open) => !open)}
          >
            <Filter className="mr-1 h-4 w-4" />
            {t("permits.filter.toggle")}
            {advancedFilterCount > 0 ? ` (${advancedFilterCount})` : ""}
          </Button>
          <div
            id="permit-filters-more"
            className={cn(
              "mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-5",
              !filtersOpen && "hidden xl:grid",
            )}
          >
            <FieldScope>
              <Label className="text-xs">{t("permits.field.status")}</Label>
              <Select
                value={status}
                onValueChange={(v) => {
                  setStatus(v === "__all__" ? "" : (v as PermitStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("permits.filter.allStatuses")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("permits.filter.allStatuses")}</SelectItem>
                  {PERMIT_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`permits.status.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label className="text-xs">{t("permits.field.permitType")}</Label>
              <Select
                value={permitTypeCode}
                onValueChange={(v) => {
                  setPermitTypeCode(v === "__all__" ? "" : v);
                  setPage(1);
                }}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("permits.filter.allTypes")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("permits.filter.allTypes")}</SelectItem>
                  {permitTypes.map((c) => (
                    <SelectItem key={c.code} value={c.code}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            {projectOptions.length > 0 ? (
              <FieldScope>
                <Label className="text-xs">{t("permits.field.project")}</Label>
                <SearchableSelect
                  className="mt-1"
                  value={projectId != null ? String(projectId) : ""}
                  onChange={(v) => {
                    setProjectId(v ? Number(v) : null);
                    setPage(1);
                  }}
                  options={[
                    { value: "", label: t("permits.filter.allProjects") },
                    ...projectOptions,
                  ]}
                  placeholder={t("permits.filter.allProjects")}
                />
              </FieldScope>
            ) : null}
            {canPickUser && userOptions.length > 0 ? (
              <FieldScope>
                <Label className="text-xs">{t("permits.field.owner")}</Label>
                <SearchableSelect
                  ariaLabel={t("permits.field.owner")}
                  className="mt-1"
                  value={ownerUserId != null ? String(ownerUserId) : ""}
                  onChange={(v) => {
                    setOwnerUserId(v ? Number(v) : null);
                    setPage(1);
                  }}
                  options={[{ value: "", label: t("permits.filter.allOwners") }, ...userOptions]}
                  placeholder={t("permits.filter.allOwners")}
                />
              </FieldScope>
            ) : null}
            {canPickUser && userOptions.length > 0 ? (
              <FieldScope>
                <Label className="text-xs">{t("permits.field.approver")}</Label>
                <SearchableSelect
                  ariaLabel={t("permits.field.approver")}
                  className="mt-1"
                  value={approverUserId != null ? String(approverUserId) : ""}
                  onChange={(value) => {
                    setApproverUserId(value ? Number(value) : null);
                    setPage(1);
                  }}
                  options={[{ value: "", label: t("permits.filter.allApprovers") }, ...userOptions]}
                  placeholder={t("permits.filter.allApprovers")}
                />
              </FieldScope>
            ) : null}
          </div>
        </section>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : rows.length === 0 ? (
          <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
            <p>{hasActiveFilter ? t("permits.emptyFiltered") : t("permits.empty")}</p>
            {hasActiveFilter ? (
              <Button variant="outline" size="sm" className="mt-3" onClick={resetFilters}>
                {t("common.reset")}
              </Button>
            ) : null}
          </div>
        ) : (
          <>
            {/* Mobile / tablet card view */}
            <div className="grid gap-3 xl:hidden">
              {rows.map((r) => {
                const next = permitNextStep(r, t, lang);
                const NextIcon = nextActionIcon(next.tone);
                const risky = r.status === "Expired" || r.status === "Rejected" || r.isOverdue;
                return (
                  <article
                    key={r.id}
                    className={cn(
                      "rounded-lg border bg-card p-3 shadow-sm",
                      risky && "border-destructive/20 bg-danger-soft/30",
                    )}
                  >
                    <header className="flex items-start justify-between gap-2">
                      <div className="flex min-w-0 items-start gap-2">
                        <PermitTypeGlyph code={r.permitTypeCode} />
                        <div className="min-w-0">
                          <h3 className="break-words text-sm font-semibold leading-tight">
                            {r.permitTypeLabel ?? r.permitTypeCode}
                            {r.isRequired ? (
                              <Badge variant="outline" className="ml-2">
                                {t("legalTemplates.required")}
                              </Badge>
                            ) : null}
                          </h3>
                          <p className="mt-0.5 break-words text-xs text-muted-foreground">
                            {r.operationalProjectCode ? `${r.operationalProjectCode} — ` : ""}
                            {r.operationalProjectName ?? "—"}
                          </p>
                        </div>
                      </div>
                      <div className="flex flex-col items-end gap-1">
                        <PermitStatusBadge status={r.status} />
                        <RiskPills row={r} t={t} />
                      </div>
                    </header>
                    {next.text ? (
                      <p
                        className={cn(
                          "mt-2 flex items-start gap-1 text-xs font-medium",
                          NEXT_TONE[next.tone],
                        )}
                      >
                        <NextIcon className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
                        <span>{next.text}</span>
                      </p>
                    ) : null}
                    <dl className="mt-2 grid grid-cols-2 gap-2 text-xs">
                      <div>
                        <dt className="text-muted-foreground">{t("permits.field.owner")}</dt>
                        <dd className="font-medium">{r.ownerName ?? "—"}</dd>
                      </div>
                      <div>
                        <dt className="text-muted-foreground">{t("permits.field.agency")}</dt>
                        <dd className="font-medium">{r.issuingAgency ?? "—"}</dd>
                      </div>
                      <div>
                        <dt className="text-muted-foreground">{t("permits.field.approver")}</dt>
                        <dd className="font-medium">{r.approverName ?? "—"}</dd>
                      </div>
                      <div>
                        <dt className="text-muted-foreground">{t("permits.field.progress")}</dt>
                        <dd className="font-medium">
                          {r.progressPercent}% · {taskStatusLabel(t, r.workTaskStatus)}
                        </dd>
                      </div>
                      <div>
                        <dt className="text-muted-foreground">
                          {t("permits.field.targetDeadline")}
                        </dt>
                        <dd className="font-medium">{formatDate(r.targetDeadline, lang)}</dd>
                      </div>
                      <div>
                        <dt className="text-muted-foreground">{t("permits.field.expiresAt")}</dt>
                        <dd className="font-medium">{formatDate(r.expiresAt, lang)}</dd>
                      </div>
                    </dl>
                    <div className="mt-3 flex flex-wrap justify-end gap-1 border-t pt-2">
                      <Button variant="ghost" size="sm" onClick={() => setViewing(r)}>
                        <Eye className="mr-1 h-4 w-4" />
                        {t("common.view")}
                      </Button>
                      {canManage ? (
                        <Button variant="ghost" size="sm" onClick={() => openEdit(r)}>
                          <Pencil className="mr-1 h-4 w-4" />
                          {t("common.edit")}
                        </Button>
                      ) : null}
                    </div>
                  </article>
                );
              })}
            </div>

            {/* Desktop table */}
            <div className="hidden overflow-x-auto rounded-lg border border-border bg-card shadow-sm xl:block">
              <table className="min-w-[900px] text-sm">
                <thead className="bg-muted/30 text-left text-xs font-medium text-muted-foreground">
                  <tr>
                    <th className="px-3 py-2">{t("permits.field.project")}</th>
                    <th className="px-3 py-2">{t("permits.field.permitType")}</th>
                    <th className="px-3 py-2">{t("permits.table.people")}</th>
                    <th className="px-3 py-2">{t("permits.field.progress")}</th>
                    <th className="px-3 py-2">{t("permits.field.status")}</th>
                    <th className="px-3 py-2">{t("permits.table.dates")}</th>
                    <th className="px-3 py-2 text-right">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {rows.map((r) => {
                    const next = permitNextStep(r, t, lang);
                    const NextIcon = nextActionIcon(next.tone);
                    const risky = r.status === "Expired" || r.status === "Rejected" || r.isOverdue;
                    return (
                      <tr
                        key={r.id}
                        className={cn(
                          "transition-colors",
                          risky ? "bg-danger-soft/30 hover:bg-danger-soft/50" : "hover:bg-muted/70",
                        )}
                      >
                        <td className="px-3 py-3 align-top">
                          <div className="max-w-44">
                            <div className="font-mono text-xs text-muted-foreground">
                              {r.operationalProjectCode ?? "—"}
                            </div>
                            <div
                              className="mt-0.5 line-clamp-2 break-words text-sm font-medium text-foreground"
                              title={r.operationalProjectName ?? undefined}
                            >
                              {r.operationalProjectName ?? "—"}
                            </div>
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <div className="flex items-start gap-2">
                            <PermitTypeGlyph code={r.permitTypeCode} />
                            <div className="max-w-44">
                              <div className="font-medium text-foreground">
                                {r.permitTypeLabel ?? r.permitTypeCode}
                              </div>
                              {r.isRequired ? (
                                <Badge variant="outline" className="mt-1">
                                  {t("legalTemplates.required")}
                                </Badge>
                              ) : null}
                              <div className="mt-0.5 line-clamp-2 text-xs text-muted-foreground">
                                {r.issuingAgency ?? "—"}
                              </div>
                            </div>
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <div className="text-sm text-foreground">{r.ownerName ?? "—"}</div>
                          <div className="mt-0.5 text-xs text-muted-foreground">
                            {r.approverName ?? "—"}
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <div className="min-w-32">
                            <div className="mb-1 flex justify-between gap-2 text-xs">
                              <span className="whitespace-nowrap">
                                {taskStatusLabel(t, r.workTaskStatus)}
                              </span>
                              <span className="whitespace-nowrap tabular-nums">
                                {r.progressPercent}%
                              </span>
                            </div>
                            <div className="h-1.5 overflow-hidden rounded bg-muted">
                              <div
                                className={cn(
                                  "h-full",
                                  r.progressPercent >= 100 ? "bg-success" : "bg-info",
                                )}
                                style={{ width: `${r.progressPercent}%` }}
                              />
                            </div>
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <div className="flex max-w-52 flex-col items-start gap-1">
                            <PermitStatusBadge status={r.status} />
                            <RiskPills row={r} t={t} />
                            {next.text ? (
                              <p
                                className={cn(
                                  "flex items-start gap-1 text-xs",
                                  NEXT_TONE[next.tone],
                                )}
                              >
                                <NextIcon
                                  className="mt-0.5 h-3.5 w-3.5 shrink-0"
                                  aria-hidden="true"
                                />
                                <span>{next.text}</span>
                              </p>
                            ) : null}
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top text-xs text-foreground">
                          <div className="whitespace-nowrap">
                            <span className="text-muted-foreground">
                              {t("permits.field.targetDeadlineShort")}:{" "}
                            </span>
                            {formatDate(r.targetDeadline, lang)}
                          </div>
                          <div className="mt-0.5 whitespace-nowrap">
                            <span className="text-muted-foreground">
                              {t("permits.field.expiresAtShort")}:{" "}
                            </span>
                            {formatDate(r.expiresAt, lang)}
                          </div>
                        </td>
                        <td className="px-3 py-3 align-top">
                          <div className="flex justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              title={t("common.view")}
                              aria-label={t("common.view")}
                              onClick={() => setViewing(r)}
                            >
                              <Eye className="h-4 w-4" />
                            </Button>
                            {canManage ? (
                              <Button
                                variant="ghost"
                                size="icon"
                                title={t("common.edit")}
                                aria-label={t("common.edit")}
                                onClick={() => openEdit(r)}
                              >
                                <Pencil className="h-4 w-4" />
                              </Button>
                            ) : null}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            {totalPages > 1 ? (
              <div className="flex items-center justify-between text-sm text-muted-foreground">
                <span>
                  {t("designProjects.paginationLabel")
                    .replace("{page}", String(page))
                    .replace("{totalPages}", String(totalPages))}
                </span>
                <div className="flex gap-1">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page <= 1}
                  >
                    {t("common.prev")}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages}
                  >
                    {t("common.next")}
                  </Button>
                </div>
              </div>
            ) : null}
          </>
        )}
      </div>

      {/* Create / edit dialog */}
      <Dialog open={dialogMode !== null} onOpenChange={(open) => (!open ? closeForm() : undefined)}>
        <DialogContent
          className="flex max-h-[calc(100dvh-2rem)] max-w-2xl flex-col overflow-hidden"
          data-testid="permit-form-dialog"
        >
          <DialogHeader className="shrink-0 break-words pr-6">
            <DialogTitle>
              {t(dialogMode === "create" ? "permits.form.createTitle" : "permits.form.title")}
            </DialogTitle>
            <DialogDescription>
              {dialogMode === "edit" && editing ? (
                <span className="text-xs text-muted-foreground">
                  {(editing.designProjectCode ?? "") +
                    " — " +
                    (editing.permitTypeLabel ?? editing.permitTypeCode)}
                </span>
              ) : null}
              <span className="mt-1 block">{t("permits.form.hint")}</span>
            </DialogDescription>
          </DialogHeader>
          {form ? (
            <div className="grid min-h-0 min-w-0 gap-3 overflow-y-auto overscroll-contain md:grid-cols-2 [&>*]:min-w-0">
              {dialogMode === "create" ? (
                <>
                  <FieldScope>
                    <Label>{t("permits.field.project")}</Label>
                    <SearchableSelect
                      className="mt-1"
                      value={form.projectId != null ? String(form.projectId) : ""}
                      onChange={(value) =>
                        setForm((current) =>
                          current
                            ? { ...current, projectId: value ? Number(value) : null }
                            : current,
                        )
                      }
                      options={projectOptions}
                      placeholder={t("permits.form.selectProject")}
                    />
                  </FieldScope>
                  <FieldScope>
                    <Label>{t("permits.field.permitType")}</Label>
                    <Select
                      value={form.permitTypeCode}
                      onValueChange={(value) =>
                        setForm((current) =>
                          current ? { ...current, permitTypeCode: value } : current,
                        )
                      }
                    >
                      <SelectTrigger className="mt-1 h-9">
                        <SelectValue placeholder={t("permits.form.selectType")} />
                      </SelectTrigger>
                      <SelectContent>
                        {permitTypes.map((type) => (
                          <SelectItem key={type.code} value={type.code}>
                            {type.name}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FieldScope>
                </>
              ) : null}
              <FieldScope>
                <Label>{t("permits.field.status")}</Label>
                <Select
                  value={form.status}
                  onValueChange={(v) =>
                    setForm((f) => (f ? { ...f, status: v as PermitStatus } : f))
                  }
                >
                  <SelectTrigger className="mt-1 h-9">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {PERMIT_STATUSES.map((s) => (
                      <SelectItem key={s} value={s}>
                        {t(`permits.status.${s}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </FieldScope>
              <FieldScope>
                <Label>{t("permits.field.agency")}</Label>
                <Input
                  value={form.issuingAgency}
                  onChange={(e) =>
                    setForm((f) => (f ? { ...f, issuingAgency: e.target.value } : f))
                  }
                />
              </FieldScope>
              {canPickUser ? (
                <FieldScope className="md:col-span-2">
                  <Label>{t("permits.field.owner")}</Label>
                  <SearchableSelect
                    ariaLabel={t("permits.field.owner")}
                    className="mt-1"
                    value={form.ownerUserId != null ? String(form.ownerUserId) : ""}
                    onChange={(v) =>
                      setForm((f) => (f ? { ...f, ownerUserId: v ? Number(v) : null } : f))
                    }
                    options={[{ value: "", label: t("permits.form.ownerNone") }, ...userOptions]}
                    placeholder={t("permits.form.ownerNone")}
                    disabled={Boolean(editing?.workTaskId)}
                  />
                </FieldScope>
              ) : null}
              <FieldScope>
                <Label>{t("permits.field.targetDeadline")}</Label>
                <Input
                  type="date"
                  value={form.targetDeadline}
                  disabled={Boolean(editing?.workTaskId)}
                  onChange={(e) =>
                    setForm((f) => (f ? { ...f, targetDeadline: e.target.value } : f))
                  }
                />
              </FieldScope>
              <FieldScope>
                <Label>{t("permits.field.submittedAt")}</Label>
                <Input
                  type="date"
                  value={form.submittedAt}
                  onChange={(e) => setForm((f) => (f ? { ...f, submittedAt: e.target.value } : f))}
                />
              </FieldScope>
              <FieldScope>
                <Label>{t("permits.field.issuedAt")}</Label>
                <Input
                  type="date"
                  value={form.issuedAt}
                  onChange={(e) => setForm((f) => (f ? { ...f, issuedAt: e.target.value } : f))}
                />
              </FieldScope>
              <FieldScope>
                <Label>{t("permits.field.expiresAt")}</Label>
                <Input
                  type="date"
                  value={form.expiresAt}
                  onChange={(e) => setForm((f) => (f ? { ...f, expiresAt: e.target.value } : f))}
                />
              </FieldScope>
              <FieldScope className="md:col-span-2">
                <Label>{t("permits.field.note")}</Label>
                <Textarea
                  rows={3}
                  value={form.note}
                  onChange={(e) => setForm((f) => (f ? { ...f, note: e.target.value } : f))}
                />
              </FieldScope>
              {dialogMode === "edit" && editing ? (
                <div className="space-y-4 md:col-span-2">
                  <PermitDocumentField
                    permitId={editing.id}
                    label={t("permits.document.submittedPackage")}
                    icon={<FileUp className="h-4 w-4 text-muted-foreground" aria-hidden="true" />}
                    path={editing.submittedFilePath}
                    uploadFile={(file) => uploadPermitFile(editing.id, "SubmittedPackage", file)}
                    onUploaded={() => refreshPermitAfterUpload(editing.id)}
                    disabled={saving}
                    testId="permit-submitted-package-upload"
                  />
                  <PermitDocumentField
                    permitId={editing.id}
                    label={t("permits.document.issuedPermit")}
                    icon={<BadgeCheck className="h-4 w-4 text-success-strong" aria-hidden="true" />}
                    path={editing.issuedFilePath}
                    uploadFile={(file) => uploadPermitFile(editing.id, "IssuedPermit", file)}
                    onUploaded={() => refreshPermitAfterUpload(editing.id)}
                    disabled={saving}
                    testId="permit-issued-file-upload"
                  />
                </div>
              ) : null}
            </div>
          ) : null}
          {formError ? <p className="text-sm text-destructive">{formError}</p> : null}
          <DialogFooter className="shrink-0 gap-2">
            <Button variant="outline" onClick={closeForm} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submitForm()} disabled={saving || !canManage}>
              {saving
                ? t("common.loading")
                : t(dialogMode === "create" ? "permits.form.create" : "common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog
        open={viewing !== null}
        onOpenChange={(open) => {
          if (!open) setViewing(null);
        }}
      >
        <DialogContent
          className="flex max-h-[calc(100dvh-2rem)] max-w-2xl flex-col overflow-hidden"
          data-testid="permit-detail-dialog"
        >
          <DialogHeader className="shrink-0 break-words pr-6">
            <div className="flex items-center gap-3">
              {viewing ? (
                <PermitTypeGlyph code={viewing.permitTypeCode} className="h-10 w-10" />
              ) : null}
              <div className="min-w-0">
                <DialogTitle>{t("permits.viewTitle")}</DialogTitle>
                <DialogDescription>
                  {viewing
                    ? `${viewing.operationalProjectCode ?? ""} — ${viewing.permitTypeLabel ?? viewing.permitTypeCode}`
                    : ""}
                </DialogDescription>
              </div>
            </div>
          </DialogHeader>
          {viewing ? (
            <dl className="grid min-h-0 min-w-0 gap-4 overflow-y-auto overscroll-contain rounded-lg border bg-muted/10 p-4 sm:grid-cols-2 [&>*]:min-w-0">
              {viewingNext?.text ? (
                <div
                  className={cn(
                    "flex items-start gap-2 rounded-md border p-3 text-sm sm:col-span-2",
                    NEXT_BANNER[viewingNext.tone],
                  )}
                >
                  {viewingNext.tone === "success" ? (
                    <FileCheck2 className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                  ) : viewingNext.tone === "danger" ? (
                    <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                  ) : (
                    <Clock className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                  )}
                  <p>{viewingNext.text}</p>
                </div>
              ) : null}
              <DetailValue
                label={t("permits.field.project")}
                value={`${viewing.operationalProjectCode ?? ""} — ${viewing.operationalProjectName ?? "—"}`}
              />
              <DetailValue
                label={t("permits.field.permitType")}
                value={viewing.permitTypeLabel ?? viewing.permitTypeCode}
              />
              <div>
                <dt className="text-xs font-medium text-muted-foreground">
                  {t("permits.field.status")}
                </dt>
                <dd className="mt-1 flex flex-wrap items-center gap-1.5">
                  <PermitStatusBadge status={viewing.status} />
                  <RiskPills row={viewing} t={t} />
                </dd>
              </div>
              <DetailValue label={t("permits.field.agency")} value={viewing.issuingAgency} />
              <DetailValue label={t("permits.field.owner")} value={viewing.ownerName} />
              <DetailValue label={t("permits.field.approver")} value={viewing.approverName} />
              <DetailValue
                label={t("permits.field.progress")}
                value={`${viewing.progressPercent}% · ${viewing.workTaskStatus ?? "—"}`}
              />
              <DetailValue
                label={t("permits.field.targetDeadline")}
                value={formatDate(viewing.targetDeadline, lang)}
              />
              <DetailValue
                label={t("permits.field.submittedAt")}
                value={formatDate(viewing.submittedAt, lang)}
              />
              <DetailValue
                label={t("permits.field.issuedAt")}
                value={formatDate(viewing.issuedAt, lang)}
              />
              <DetailValue
                label={t("permits.field.expiresAt")}
                value={formatDate(viewing.expiresAt, lang)}
              />
              <DetailValue
                label={t("permits.field.updatedAt")}
                value={formatDate(viewing.updatedAt, lang)}
              />
              <div className="sm:col-span-2">
                <DetailValue label={t("permits.field.note")} value={viewing.note} />
              </div>
              {viewTask ? (
                <section
                  className="space-y-3 rounded-md border bg-background p-3 sm:col-span-2"
                  aria-label={t("workPackages.tasks.title")}
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <div>
                      <h3 className="flex items-center gap-2 text-sm font-semibold">
                        <ListChecks className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
                        {t("workPackages.tasks.title")}
                      </h3>
                      <p className="text-xs text-muted-foreground">
                        {viewTask.wbsCode} · {viewTask.title}
                      </p>
                    </div>
                    <Badge variant="outline">
                      {taskStatusLabel(t, viewTask.status)} · {viewTask.progressPercent}%
                    </Badge>
                  </div>
                  {viewTask.canUpdateProgress &&
                  ["Planned", "InProgress"].includes(viewTask.status) ? (
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-end">
                      <div className="min-w-0 flex-1 space-y-1">
                        <Label htmlFor="permit-task-progress">
                          {t("workPackages.action.progress")}
                        </Label>
                        <Input
                          id="permit-task-progress"
                          type="number"
                          min={1}
                          max={99}
                          value={taskProgress}
                          onChange={(event) =>
                            setTaskProgress(
                              Math.max(1, Math.min(99, Number(event.target.value) || 1)),
                            )
                          }
                        />
                      </div>
                      <Button
                        variant="outline"
                        disabled={taskSaving}
                        onClick={() => void updateViewedTaskProgress(false)}
                      >
                        {t("workPackages.action.progress")}
                      </Button>
                      <Button
                        disabled={taskSaving || !viewingPermitReady}
                        title={!viewingPermitReady ? t("permits.next.completeBlocked") : undefined}
                        onClick={() => void updateViewedTaskProgress(true)}
                      >
                        {t("workPackages.action.completeTask")}
                      </Button>
                    </div>
                  ) : null}
                  {viewTask.canComplete &&
                  viewTask.hasSubtasks &&
                  ["Planned", "InProgress"].includes(viewTask.status) ? (
                    <Button
                      disabled={taskSaving || !viewingPermitReady}
                      title={!viewingPermitReady ? t("permits.next.completeBlocked") : undefined}
                      onClick={() => void updateViewedTaskProgress(true)}
                    >
                      {t("workPackages.action.completeTask")}
                    </Button>
                  ) : null}
                  {viewTask.canVerify && viewTask.status === "Completed" ? (
                    <div className="space-y-2 border-t pt-3">
                      <Label htmlFor="permit-task-reject-reason">
                        {t("workPackages.reject.reason")}
                      </Label>
                      <Textarea
                        id="permit-task-reject-reason"
                        maxLength={2000}
                        value={taskRejectReason}
                        onChange={(event) => setTaskRejectReason(event.target.value)}
                      />
                      <div className="flex flex-wrap justify-end gap-2">
                        <Button
                          variant="destructiveOutline"
                          disabled={taskSaving || !taskRejectReason.trim()}
                          onClick={() => void decideViewedTask(false)}
                        >
                          {t("common.reject")}
                        </Button>
                        <Button disabled={taskSaving} onClick={() => void decideViewedTask(true)}>
                          <CheckCircle2 className="mr-2 h-4 w-4" />
                          {t("workPackages.action.verify")}
                        </Button>
                      </div>
                    </div>
                  ) : null}
                </section>
              ) : null}
              {viewing.workPackageId ? (
                <div className="sm:col-span-2">
                  <Button asChild variant="outline" size="sm">
                    <Link to={`/admin/work-packages/${viewing.workPackageId}`}>
                      {t("permits.openWorkPackage")}
                    </Link>
                  </Button>
                </div>
              ) : null}
              <div className="space-y-2 sm:col-span-2">
                <PermitDocumentField
                  permitId={viewing.id}
                  label={t("permits.document.submittedPackage")}
                  icon={<FileUp className="h-4 w-4 text-muted-foreground" aria-hidden="true" />}
                  path={viewing.submittedFilePath}
                  uploadFile={(file) => uploadPermitFile(viewing.id, "SubmittedPackage", file)}
                  onUploaded={() => refreshPermitAfterUpload(viewing.id)}
                  disabled={!canManage}
                  testId="permit-detail-submitted-package-upload"
                  hideUpload={!canManage}
                />
                <PermitDocumentField
                  permitId={viewing.id}
                  label={t("permits.document.issuedPermit")}
                  icon={<BadgeCheck className="h-4 w-4 text-success-strong" aria-hidden="true" />}
                  path={viewing.issuedFilePath}
                  uploadFile={(file) => uploadPermitFile(viewing.id, "IssuedPermit", file)}
                  onUploaded={() => refreshPermitAfterUpload(viewing.id)}
                  disabled={!canManage}
                  testId="permit-detail-issued-file-upload"
                  hideUpload={!canManage}
                />
              </div>
              <div className="space-y-2 sm:col-span-2">
                <h3 className="flex items-center gap-2 text-sm font-semibold">
                  <History className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
                  {t("permits.history")}
                </h3>
                {permitHistory.length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t("permits.historyEmpty")}</p>
                ) : (
                  permitHistory.map((entry) => (
                    <div key={entry.id} className="rounded-md border p-2 text-xs">
                      <div className="flex flex-wrap justify-between gap-2">
                        <span className="font-medium">
                          {historyActionLabel(t, entry.action)}
                          {entry.fromStatus && entry.fromStatus !== entry.toStatus
                            ? ` · ${permitStatusLabel(t, entry.fromStatus)} → ${permitStatusLabel(t, entry.toStatus)}`
                            : ""}
                        </span>
                        <span className="text-muted-foreground">
                          {formatDate(entry.changedAt, lang)}
                        </span>
                      </div>
                      <p className="mt-1 text-muted-foreground">{entry.changedByName}</p>
                    </div>
                  ))
                )}
              </div>
            </dl>
          ) : null}
          <DialogFooter className="shrink-0 gap-2">
            <Button variant="outline" onClick={() => setViewing(null)}>
              {t("common.close")}
            </Button>
            {canManage && viewing ? (
              <Button
                onClick={() => {
                  const row = viewing;
                  setViewing(null);
                  openEdit(row);
                }}
              >
                <Pencil className="mr-1 h-4 w-4" />
                {t("common.edit")}
              </Button>
            ) : null}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <AlertDialog
        open={pendingDelete !== null}
        onOpenChange={(open) => {
          if (!open && !deleting) setPendingDelete(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("permits.deleteTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("permits.deleteDescription")
                .replace(
                  "{type}",
                  pendingDelete?.permitTypeLabel ?? pendingDelete?.permitTypeCode ?? "",
                )
                .replace("{project}", pendingDelete?.operationalProjectName ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleting}>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              disabled={deleting}
              onClick={(event) => {
                event.preventDefault();
                void deletePermit();
              }}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              {deleting ? t("permits.deleting") : t("common.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
      {setupProjectId ? (
        <LegalActivationWizard
          operationalProjectId={setupProjectId}
          open
          onOpenChange={(next) => {
            if (!next) setSetupProjectId(null);
          }}
          onActivated={async () => {
            await fetchList();
          }}
        />
      ) : null}
    </AdminLayout>
  );
};

const DetailValue = ({ label, value }: { label: string; value?: string | null }) => (
  <div>
    <dt className="text-xs font-medium text-muted-foreground">{label}</dt>
    <dd className="mt-1 break-words text-sm font-medium">{value || "—"}</dd>
  </div>
);

const PermitDocumentField = ({
  permitId,
  label,
  icon,
  path,
  uploadFile,
  onUploaded,
  disabled,
  testId,
  hideUpload = false,
}: {
  permitId: number;
  label: string;
  icon: ReactNode;
  path?: string | null;
  uploadFile: (file: File) => Promise<string>;
  onUploaded: (paths: string[]) => void | Promise<void>;
  disabled: boolean;
  testId: string;
  hideUpload?: boolean;
}) => {
  const { t } = useI18n();
  const fileName = fileDisplayName(path);
  return (
    <div className="space-y-2 rounded-md border p-3">
      <div className="flex items-center justify-between gap-2">
        <span className="flex items-center gap-2 text-sm font-medium">
          {icon}
          {label}
        </span>
        {path ? (
          <AdminFilePreview
            url={path}
            fileName={fileName}
            fetchFile={
              isManagedDocumentPath(path, "/files/business-documents/permits")
                ? async () => (await adminApi.getPermitDocumentContent(permitId, path)).data
                : undefined
            }
          />
        ) : null}
      </div>
      <p className="break-words text-xs text-muted-foreground">
        {fileName ?? t("permits.document.none")}
      </p>
      {!hideUpload ? (
        <AdminDocumentUpload
          uploadFile={uploadFile}
          onUploaded={onUploaded}
          disabled={disabled}
          testId={testId}
        />
      ) : null}
    </div>
  );
};

const RiskCard = ({
  title,
  value,
  tone,
  icon,
  onClick,
  active,
}: {
  title: string;
  value: number | string;
  tone: "rose" | "amber" | "orange" | "slate";
  icon: React.ReactNode;
  onClick?: () => void;
  active?: boolean;
}) => {
  const tones: Record<typeof tone, string> = {
    rose: "border-destructive/20 bg-danger-soft/60 text-danger-strong",
    amber: "border-warning/40 bg-warning-soft/60 text-warning-strong",
    orange: "border-warning/40 bg-warning-soft/60 text-warning-strong",
    slate: "border-border bg-muted/30 text-foreground",
  };
  const className = cn(
    "flex items-center justify-between rounded-lg border px-3 py-2.5 text-left shadow-sm transition-colors",
    tones[tone],
    onClick ? "cursor-pointer hover:brightness-95" : "cursor-default",
    active && "ring-2 ring-offset-1 ring-slate-800/40",
  );
  const content = (
    <>
      <div className="flex items-center gap-2">
        <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-card/80 shadow-sm">
          {icon}
        </span>
        <span className="text-xs font-medium">{title}</span>
      </div>
      <span className="text-xl font-bold tabular-nums">{value}</span>
    </>
  );
  return onClick ? (
    <button type="button" aria-pressed={active} onClick={onClick} className={className}>
      {content}
    </button>
  ) : (
    <div className={className}>{content}</div>
  );
};

const RiskPills = ({
  row,
  t,
}: {
  row: PermitChecklistItemResponse;
  t: (key: string) => string;
}) => {
  if (!row.isOverdue && !row.isDueSoon && !row.isExpiringSoon) return null;
  return (
    <div className="flex flex-wrap items-center gap-1">
      {row.isOverdue ? (
        <Badge
          variant="outline"
          className="gap-1 border-destructive/20 bg-destructive/10 text-destructive whitespace-nowrap"
        >
          <AlertTriangle className="h-3 w-3" aria-hidden="true" />
          {t("permits.risk.overduePill")}
        </Badge>
      ) : null}
      {row.isDueSoon ? (
        <Badge
          variant="outline"
          className="gap-1 border-warning/40 bg-warning-soft text-warning-strong whitespace-nowrap"
        >
          <Clock className="h-3 w-3" aria-hidden="true" />
          {t("permits.risk.dueSoonPill")}
        </Badge>
      ) : null}
      {row.isExpiringSoon ? (
        <Badge
          variant="outline"
          className="gap-1 border-warning/40 bg-warning-soft text-warning-strong whitespace-nowrap"
        >
          <Clock className="h-3 w-3" aria-hidden="true" />
          {t("permits.risk.expiringSoonPill")}
        </Badge>
      ) : null}
    </div>
  );
};

export default AdminPermits;
