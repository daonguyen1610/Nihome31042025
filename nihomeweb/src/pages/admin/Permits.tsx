import { personOption, type PersonIdentity } from "@/lib/people";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  Eye,
  FileCheck2,
  Filter,
  Pencil,
  Plus,
  RefreshCcw,
  Search,
  ShieldAlert,
  Trash2,
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

// ------------------------------- helpers -------------------------------

const STATUS_BADGE: Record<PermitStatus, string> = {
  NotStarted: "border-slate-200 bg-slate-50 text-slate-700",
  Preparing: "border-sky-200 bg-sky-50 text-sky-700",
  Submitted: "border-indigo-200 bg-indigo-50 text-indigo-700",
  UnderReview: "border-amber-200 bg-amber-50 text-amber-700",
  NeedMoreDocs: "border-orange-200 bg-orange-50 text-orange-700",
  Issued: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  Expired: "border-rose-200 bg-rose-50 text-rose-700",
};

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

const toDateInputValue = (iso?: string | null) => (iso ? iso.slice(0, 10) : "");

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
  const { t, lang } = useI18n();
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
  const [projectId, setProjectId] = useState<number | null>(() => {
    const value = Number(searchParams.get("operationalProjectId"));
    return Number.isInteger(value) && value > 0 ? value : null;
  });
  const [permitTypeCode, setPermitTypeCode] = useState<string>("");
  const [status, setStatus] = useState<PermitStatus | "">("");
  const [ownerUserId, setOwnerUserId] = useState<number | null>(null);
  const [approverUserId, setApproverUserId] = useState<number | null>(null);
  const [flag, setFlag] = useState<"" | "overdue" | "dueSoon" | "expiringSoon">("");
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
          adminApi.listOperationalProjects({ pageSize: 200 }).catch(() => ({
            data: { total: 0, page: 1, pageSize: 200, items: [] },
          })),
          adminApi.getMasterDataOptions("permit_type").catch(() => ({ data: [] })),
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
  }, [canPickUser]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: PermitChecklistListParams = { page, pageSize };
      if (search.trim()) params.search = search.trim();
      if (projectId != null) params.operationalProjectId = projectId;
      if (permitTypeCode) params.permitTypeCode = permitTypeCode;
      if (status) params.status = status;
      if (ownerUserId != null) params.ownerUserId = ownerUserId;
      if (approverUserId != null) params.approverUserId = approverUserId;
      if (flag === "overdue") params.overdue = true;
      if (flag === "dueSoon") params.dueSoon = true;
      if (flag === "expiringSoon") params.expiringSoon = true;
      const { data } = await adminApi.listPermits(params);
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
      setRisk(data.risk);
    } catch (err) {
      setError(extractApiError(err));
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setLoading(false);
    }
  }, [
    page,
    pageSize,
    search,
    projectId,
    permitTypeCode,
    status,
    ownerUserId,
    approverUserId,
    flag,
    t,
    toast,
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
          description: extractApiError(err),
          variant: "destructive",
        }),
      );
  }, [searchParams, t, toast]);

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
    setTaskSaving(true);
    try {
      await adminApi.updateWorkTaskProgress(
        viewing.workPackageId,
        viewTask.id,
        {
          progressPercent: complete ? 100 : taskProgress,
          status: complete ? "Completed" : "InProgress",
          rowVersion: viewTask.rowVersion,
        },
        newIdempotencyKey(),
      );
      await refreshViewedPermit();
    } catch (cause) {
      toast({
        title: t("common.error"),
        description: extractApiError(cause),
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
        description: extractApiError(cause),
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
      setFormError(extractApiError(err));
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
        description: extractApiError(err),
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

  // ----------------------------- render -----------------------------

  return (
    <AdminLayout>
      <div className="space-y-4 p-3 md:p-4">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex flex-col gap-1">
            <h1 className="text-xl font-bold text-slate-900 md:text-2xl">{t("permits.title")}</h1>
            <p className="text-sm text-slate-600">{t("permits.subtitle")}</p>
          </div>
          {canManage ? (
            <Button
              size="sm"
              disabled={!projectId}
              onClick={() => projectId && setSetupProjectId(projectId)}
            >
              <Plus className="mr-1 h-4 w-4" />
              {t("permits.setupLegal")}
            </Button>
          ) : null}
        </header>

        {/* Risk register */}
        {has("permit.templates.view") ? <LegalChecklistTemplates /> : null}
        <section className="grid gap-2 md:grid-cols-4">
          <RiskCard
            title={t("permits.risk.overdue")}
            value={risk.overdue}
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
            value={risk.dueSoon}
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
            value={risk.expiringSoon}
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
            value={risk.totalOpen}
            tone="slate"
            icon={<FileCheck2 className="h-4 w-4" />}
          />
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
            <div className="lg:col-span-2">
              <Label className="text-xs">{t("permits.filter.search")}</Label>
              <div className="relative mt-1">
                <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-slate-400" />
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
            </div>
            <div>
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
            </div>
            <div>
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
            </div>
            {projectOptions.length > 0 ? (
              <div>
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
              </div>
            ) : null}
            {canPickUser && userOptions.length > 0 ? (
              <div>
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
              </div>
            ) : null}
            {canPickUser && userOptions.length > 0 ? (
              <div>
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
              </div>
            ) : null}
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => void fetchList()} disabled={loading}>
              <RefreshCcw className={cn("mr-1 h-4 w-4", loading && "animate-spin")} />
              {t("common.refresh")}
            </Button>
            <Button variant="ghost" size="sm" onClick={resetFilters} disabled={loading}>
              <Filter className="mr-1 h-4 w-4" />
              {t("common.reset")}
            </Button>
            <span className="ml-auto text-xs text-slate-500">
              {t("permits.total").replace("{count}", String(total))}
            </span>
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
            <div className="grid gap-3 lg:hidden">
              {rows.map((r) => (
                <article key={r.id} className="rounded-lg border bg-white p-3 shadow-sm">
                  <header className="flex items-start justify-between gap-2">
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
                    <div className="flex flex-col items-end gap-1">
                      <Badge
                        variant="outline"
                        className={cn("whitespace-nowrap", STATUS_BADGE[r.status])}
                      >
                        {t(`permits.status.${r.status}`)}
                      </Badge>
                      <RiskPills row={r} t={t} />
                    </div>
                  </header>
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
                        {r.progressPercent}% · {r.workTaskStatus ?? "—"}
                      </dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("permits.field.targetDeadline")}</dt>
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
              ))}
            </div>

            {/* Desktop table */}
            <div className="hidden overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm lg:block">
              <table className="min-w-full text-sm">
                <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                  <tr>
                    <th className="px-3 py-2">{t("permits.field.project")}</th>
                    <th className="px-3 py-2">{t("permits.field.permitType")}</th>
                    <th className="px-3 py-2">{t("permits.field.agency")}</th>
                    <th className="px-3 py-2">{t("permits.field.owner")}</th>
                    <th className="px-3 py-2">{t("permits.field.approver")}</th>
                    <th className="px-3 py-2">{t("permits.field.progress")}</th>
                    <th className="px-3 py-2">{t("permits.field.status")}</th>
                    <th className="px-3 py-2">{t("permits.field.targetDeadline")}</th>
                    <th className="px-3 py-2">{t("permits.field.expiresAt")}</th>
                    <th className="px-3 py-2 text-right">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {rows.map((r) => (
                    <tr key={r.id} className="transition-colors hover:bg-slate-50/70">
                      <td className="px-3 py-2 text-slate-700">
                        <div className="font-mono text-xs text-slate-500">
                          {r.operationalProjectCode ?? "—"}
                        </div>
                        <div className="text-slate-800">{r.operationalProjectName ?? "—"}</div>
                      </td>
                      <td className="px-3 py-2 font-medium text-slate-900">
                        {r.permitTypeLabel ?? r.permitTypeCode}
                        {r.isRequired ? (
                          <Badge variant="outline" className="ml-2">
                            {t("legalTemplates.required")}
                          </Badge>
                        ) : null}
                      </td>
                      <td className="px-3 py-2 text-slate-700">{r.issuingAgency ?? "—"}</td>
                      <td className="px-3 py-2 text-slate-700">{r.ownerName ?? "—"}</td>
                      <td className="px-3 py-2 text-slate-700">{r.approverName ?? "—"}</td>
                      <td className="px-3 py-2 text-slate-700">
                        <div className="min-w-24">
                          <div className="mb-1 flex justify-between text-xs">
                            <span>{r.workTaskStatus ?? "—"}</span>
                            <span>{r.progressPercent}%</span>
                          </div>
                          <div className="h-1.5 overflow-hidden rounded bg-slate-100">
                            <div
                              className="h-full bg-primary"
                              style={{ width: `${r.progressPercent}%` }}
                            />
                          </div>
                        </div>
                      </td>
                      <td className="px-3 py-2">
                        <div className="flex flex-col items-start gap-1">
                          <Badge
                            variant="outline"
                            className={cn("whitespace-nowrap", STATUS_BADGE[r.status])}
                          >
                            {t(`permits.status.${r.status}`)}
                          </Badge>
                          <RiskPills row={r} t={t} />
                        </div>
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-slate-700">
                        {formatDate(r.targetDeadline, lang)}
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-slate-700">
                        {formatDate(r.expiresAt, lang)}
                      </td>
                      <td className="px-3 py-2">
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
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 ? (
              <div className="flex items-center justify-between text-sm text-slate-600">
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
                <span className="text-xs text-slate-600">
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
                  <div>
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
                  </div>
                  <div>
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
                  </div>
                </>
              ) : null}
              <div>
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
              </div>
              <div>
                <Label>{t("permits.field.agency")}</Label>
                <Input
                  value={form.issuingAgency}
                  onChange={(e) =>
                    setForm((f) => (f ? { ...f, issuingAgency: e.target.value } : f))
                  }
                />
              </div>
              {canPickUser ? (
                <div className="md:col-span-2">
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
                </div>
              ) : null}
              <div>
                <Label>{t("permits.field.targetDeadline")}</Label>
                <Input
                  type="date"
                  value={form.targetDeadline}
                  disabled={Boolean(editing?.workTaskId)}
                  onChange={(e) =>
                    setForm((f) => (f ? { ...f, targetDeadline: e.target.value } : f))
                  }
                />
              </div>
              <div>
                <Label>{t("permits.field.submittedAt")}</Label>
                <Input
                  type="date"
                  value={form.submittedAt}
                  onChange={(e) => setForm((f) => (f ? { ...f, submittedAt: e.target.value } : f))}
                />
              </div>
              <div>
                <Label>{t("permits.field.issuedAt")}</Label>
                <Input
                  type="date"
                  value={form.issuedAt}
                  onChange={(e) => setForm((f) => (f ? { ...f, issuedAt: e.target.value } : f))}
                />
              </div>
              <div>
                <Label>{t("permits.field.expiresAt")}</Label>
                <Input
                  type="date"
                  value={form.expiresAt}
                  onChange={(e) => setForm((f) => (f ? { ...f, expiresAt: e.target.value } : f))}
                />
              </div>
              <div className="md:col-span-2">
                <Label>{t("permits.field.note")}</Label>
                <Textarea
                  rows={3}
                  value={form.note}
                  onChange={(e) => setForm((f) => (f ? { ...f, note: e.target.value } : f))}
                />
              </div>
              {dialogMode === "edit" && editing ? (
                <div className="space-y-4 md:col-span-2">
                  <PermitDocumentField
                    permitId={editing.id}
                    label={t("permits.document.submittedPackage")}
                    path={editing.submittedFilePath}
                    uploadFile={(file) => uploadPermitFile(editing.id, "SubmittedPackage", file)}
                    onUploaded={() => refreshPermitAfterUpload(editing.id)}
                    disabled={saving}
                    testId="permit-submitted-package-upload"
                  />
                  <PermitDocumentField
                    permitId={editing.id}
                    label={t("permits.document.issuedPermit")}
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
          {formError ? <p className="text-sm text-rose-600">{formError}</p> : null}
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
            <DialogTitle>{t("permits.viewTitle")}</DialogTitle>
            <DialogDescription>
              {viewing
                ? `${viewing.operationalProjectCode ?? ""} — ${viewing.permitTypeLabel ?? viewing.permitTypeCode}`
                : ""}
            </DialogDescription>
          </DialogHeader>
          {viewing ? (
            <dl className="grid min-h-0 min-w-0 gap-4 overflow-y-auto overscroll-contain rounded-lg border bg-muted/10 p-4 sm:grid-cols-2 [&>*]:min-w-0">
              <DetailValue
                label={t("permits.field.project")}
                value={`${viewing.operationalProjectCode ?? ""} — ${viewing.operationalProjectName ?? "—"}`}
              />
              <DetailValue
                label={t("permits.field.permitType")}
                value={viewing.permitTypeLabel ?? viewing.permitTypeCode}
              />
              <DetailValue
                label={t("permits.field.status")}
                value={t(`permits.status.${viewing.status}`)}
              />
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
                      <h3 className="text-sm font-semibold">{t("workPackages.tasks.title")}</h3>
                      <p className="text-xs text-muted-foreground">
                        {viewTask.wbsCode} Â· {viewTask.title}
                      </p>
                    </div>
                    <Badge variant="outline">
                      {viewTask.status} Â· {viewTask.progressPercent}%
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
                        disabled={taskSaving}
                        onClick={() => void updateViewedTaskProgress(true)}
                      >
                        {t("workPackages.action.completeTask")}
                      </Button>
                    </div>
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
                          variant="destructive"
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
                  path={viewing.issuedFilePath}
                  uploadFile={(file) => uploadPermitFile(viewing.id, "IssuedPermit", file)}
                  onUploaded={() => refreshPermitAfterUpload(viewing.id)}
                  disabled={!canManage}
                  testId="permit-detail-issued-file-upload"
                  hideUpload={!canManage}
                />
              </div>
              <div className="space-y-2 sm:col-span-2">
                <h3 className="text-sm font-semibold">{t("permits.history")}</h3>
                {permitHistory.length === 0 ? (
                  <p className="text-sm text-muted-foreground">{t("permits.historyEmpty")}</p>
                ) : (
                  permitHistory.map((entry) => (
                    <div key={entry.id} className="rounded-md border p-2 text-xs">
                      <div className="flex flex-wrap justify-between gap-2">
                        <span className="font-medium">
                          {entry.action}
                          {entry.fromStatus && entry.fromStatus !== entry.toStatus
                            ? ` · ${entry.fromStatus} → ${entry.toStatus}`
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
    <dt className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{label}</dt>
    <dd className="mt-1 break-words text-sm font-medium">{value || "—"}</dd>
  </div>
);

const PermitDocumentField = ({
  permitId,
  label,
  path,
  uploadFile,
  onUploaded,
  disabled,
  testId,
  hideUpload = false,
}: {
  permitId: number;
  label: string;
  path?: string | null;
  uploadFile: (file: File) => Promise<string>;
  onUploaded: (paths: string[]) => void | Promise<void>;
  disabled: boolean;
  testId: string;
  hideUpload?: boolean;
}) => (
  <div className="space-y-2 rounded-md border p-3">
    <div className="flex items-center justify-between gap-2">
      <Label>{label}</Label>
      {path ? (
        <AdminFilePreview
          url={path}
          fileName={path.split("/").pop()}
          fetchFile={
            isManagedDocumentPath(path, "/files/business-documents/permits")
              ? async () => (await adminApi.getPermitDocumentContent(permitId, path)).data
              : undefined
          }
        />
      ) : null}
    </div>
    <p className="break-all text-xs text-muted-foreground">{path ?? "—"}</p>
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

const RiskCard = ({
  title,
  value,
  tone,
  icon,
  onClick,
  active,
}: {
  title: string;
  value: number;
  tone: "rose" | "amber" | "orange" | "slate";
  icon: React.ReactNode;
  onClick?: () => void;
  active?: boolean;
}) => {
  const tones: Record<typeof tone, string> = {
    rose: "border-rose-200 bg-rose-50/60 text-rose-800",
    amber: "border-amber-200 bg-amber-50/60 text-amber-800",
    orange: "border-orange-200 bg-orange-50/60 text-orange-800",
    slate: "border-slate-200 bg-slate-50 text-slate-800",
  };
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={!onClick}
      className={cn(
        "flex items-center justify-between rounded-lg border px-3 py-2.5 text-left shadow-sm transition-colors",
        tones[tone],
        onClick ? "cursor-pointer hover:brightness-95" : "cursor-default",
        active && "ring-2 ring-offset-1 ring-slate-800/40",
      )}
    >
      <div className="flex items-center gap-2">
        <span className="rounded-md bg-white/70 p-1">{icon}</span>
        <span className="text-xs font-medium">{title}</span>
      </div>
      <span className="text-lg font-bold">{value}</span>
    </button>
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
          className="border-rose-200 bg-rose-50 text-rose-700 whitespace-nowrap"
        >
          {t("permits.risk.overduePill")}
        </Badge>
      ) : null}
      {row.isDueSoon ? (
        <Badge
          variant="outline"
          className="border-amber-200 bg-amber-50 text-amber-700 whitespace-nowrap"
        >
          {t("permits.risk.dueSoonPill")}
        </Badge>
      ) : null}
      {row.isExpiringSoon ? (
        <Badge
          variant="outline"
          className="border-orange-200 bg-orange-50 text-orange-700 whitespace-nowrap"
        >
          {t("permits.risk.expiringSoonPill")}
        </Badge>
      ) : null}
    </div>
  );
};

export default AdminPermits;
