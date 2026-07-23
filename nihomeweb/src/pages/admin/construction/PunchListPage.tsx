import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  CircleDot,
  MapPin,
  Play,
  RefreshCcw,
  RotateCcw,
  Search,
  ShieldAlert,
  ShieldCheck,
  Trash2,
  Wrench,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { PageLoading, PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
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
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import {
  adminApi,
  type CreatePunchItemRequest,
  type DesignProjectListItemResponse,
  type PunchItemListParams,
  type PunchItemResponse,
  type PunchSeverity,
  type PunchStatus,
  type UpdatePunchItemRequest,
} from "@/services/adminApi";

const PUNCH_STATUSES: PunchStatus[] = ["Open", "InProgress", "Fixed", "Verified", "Cancelled"];
const PUNCH_SEVERITIES: PunchSeverity[] = ["Low", "Medium", "High", "Critical"];

const STATUS_BADGE: Record<PunchStatus, string> = {
  Open: "border-slate-200 bg-slate-50 text-slate-700",
  InProgress: "border-sky-200 bg-sky-50 text-sky-700",
  Fixed: "border-amber-200 bg-amber-50 text-amber-800",
  Verified: "border-emerald-200 bg-emerald-50 text-emerald-800",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
};

const SEVERITY_BADGE: Record<PunchSeverity, string> = {
  Low: "border-slate-200 bg-slate-100 text-slate-700",
  Medium: "border-sky-200 bg-sky-50 text-sky-700",
  High: "border-orange-200 bg-orange-50 text-orange-800",
  Critical: "border-rose-300 bg-rose-100 text-rose-800",
};

const OVERDUE_BADGE = "border-amber-300 bg-amber-50 text-amber-800";

interface UserOption {
  id: number;
  fullName: string;
}

type ConfirmKind = "reopen" | "verify" | "delete" | null;

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try { return new Date(iso).toLocaleDateString(lang); } catch { return iso; }
};

interface PunchForm {
  title: string;
  description: string;
  location: string;
  severity: PunchSeverity;
  assigneeUserId: number | null;
  deadline: string;
  resolutionNote: string;
  note: string;
}

const emptyForm = (): PunchForm => ({
  title: "",
  description: "",
  location: "",
  severity: "Medium",
  assigneeUserId: null,
  deadline: "",
  resolutionNote: "",
  note: "",
});

const formFromRow = (row: PunchItemResponse): PunchForm => ({
  title: row.title,
  description: row.description ?? "",
  location: row.location ?? "",
  severity: row.severity,
  assigneeUserId: row.assigneeUserId ?? null,
  deadline: row.deadline ?? "",
  resolutionNote: row.resolutionNote ?? "",
  note: row.note ?? "",
});

const AdminPunchList = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionPunchManage);
  const canVerify = has(ADMIN_PERMS.constructionPunchVerify);
  const canListUsers = has(ADMIN_PERMS.users);

  // list state
  const [rows, setRows] = useState<PunchItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<PunchStatus, number>>>({});
  const [overdueCount, setOverdueCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const [projectId, setProjectId] = useState<number | null>(null);
  const [status, setStatus] = useState<PunchStatus | "">("");
  const [severity, setSeverity] = useState<PunchSeverity | "">("");
  const [assigneeUserId, setAssigneeUserId] = useState<number | null>(null);
  const [openOnly, setOpenOnly] = useState(false);
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 50;

  // selection
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [confirmBulkDelete, setConfirmBulkDelete] = useState(false);
  const [bulkDeleting, setBulkDeleting] = useState(false);

  // dialogs
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState<PunchForm>(() => emptyForm());
  const [createProjectId, setCreateProjectId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const [detail, setDetail] = useState<PunchItemResponse | null>(null);
  const [detailForm, setDetailForm] = useState<PunchForm | null>(null);
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [confirmAction, setConfirmAction] = useState<{ kind: ConfirmKind; row: PunchItemResponse | null }>({
    kind: null, row: null,
  });
  const [actionBusy, setActionBusy] = useState(false);

  // lookups
  const [projects, setProjects] = useState<DesignProjectListItemResponse[]>([]);
  const [users, setUsers] = useState<UserOption[]>([]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.listDesignProjects({ pageSize: 200 });
        if (cancelled) return;
        setProjects(data.items ?? []);
      } catch { /* non-fatal */ }
      if (canListUsers) {
        try {
          const { data } = await adminApi.getUsers({ take: 200 });
          if (!cancelled) {
            setUsers((data.items ?? []).map((u) => ({
              id: u.id, fullName: u.fullName ?? u.email ?? `#${u.id}`,
            })));
          }
        } catch { /* ignore */ }
      }
    })();
    return () => { cancelled = true; };
  }, [canListUsers]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: PunchItemListParams = { page, pageSize };
      if (projectId != null) params.designProjectId = projectId;
      if (status) params.status = status;
      if (severity) params.severity = severity;
      if (assigneeUserId != null) params.assigneeUserId = assigneeUserId;
      if (openOnly) params.openOnly = true;
      if (overdueOnly) params.overdueOnly = true;
      if (search.trim()) params.search = search.trim();
      const { data } = await adminApi.listPunchItems(params);
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
      setStatusCounts(data.statusCounts ?? {});
      setOverdueCount(data.overdueCount ?? 0);
      const visible = new Set((data.items ?? []).map((r) => r.id));
      setSelected((prev) => {
        const next = new Set<number>();
        prev.forEach((id) => { if (visible.has(id)) next.add(id); });
        return next;
      });
    } catch (err) {
      setError(extractApiError(err));
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, projectId, status, severity, assigneeUserId, openOnly, overdueOnly, search, t, toast]);

  useEffect(() => { void fetchList(); }, [fetchList]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const statusPill = (s: PunchStatus) => statusCounts[s] ?? 0;

  const projectOptions = useMemo(
    () => [
      { value: "", label: t("punch.filter.allProjects") },
      ...projects.map((p) => ({ value: String(p.id), label: `${p.projectCode} — ${p.name}` })),
    ],
    [projects, t],
  );
  const projectOptionsForCreate = useMemo(
    () => projects.map((p) => ({ value: String(p.id), label: `${p.projectCode} — ${p.name}` })),
    [projects],
  );
  const userFilterOptions = useMemo(
    () => [
      { value: "", label: t("punch.filter.allAssignees") },
      ...users.map((u) => ({ value: String(u.id), label: u.fullName })),
    ],
    [users, t],
  );
  const userFormOptions = useMemo(
    () => users.map((u) => ({ value: String(u.id), label: u.fullName })),
    [users],
  );

  const hasActiveFilter =
    projectId != null || status !== "" || severity !== "" ||
    assigneeUserId != null || openOnly || overdueOnly || search.trim() !== "";

  const resetFilters = () => {
    setProjectId(null);
    setStatus("");
    setSeverity("");
    setAssigneeUserId(null);
    setOpenOnly(false);
    setOverdueOnly(false);
    setSearch("");
    setPage(1);
  };

  // ---- create ----
  const openCreate = () => {
    setCreateForm(emptyForm());
    setCreateProjectId(projectId);
    setCreateError(null);
    setCreating(true);
  };
  const submitCreate = async () => {
    setCreateError(null);
    if (createProjectId == null) { setCreateError(t("punch.form.projectRequired")); return; }
    if (!createForm.title.trim()) { setCreateError(t("punch.form.titleRequired")); return; }
    setSaving(true);
    try {
      const body: CreatePunchItemRequest = {
        designProjectId: createProjectId,
        title: createForm.title.trim(),
        description: createForm.description.trim() || undefined,
        location: createForm.location.trim() || undefined,
        severity: createForm.severity,
        assigneeUserId: createForm.assigneeUserId ?? undefined,
        deadline: createForm.deadline || undefined,
        note: createForm.note.trim() || undefined,
      };
      await adminApi.createPunchItem(body);
      toast({ title: t("punch.created") });
      setCreating(false);
      await fetchList();
    } catch (err) {
      setCreateError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // ---- detail ----
  const openDetail = (row: PunchItemResponse) => {
    setDetail(row);
    setDetailForm(formFromRow(row));
    setDetailError(null);
  };
  const closeDetail = () => {
    setDetail(null);
    setDetailForm(null);
    setDetailError(null);
  };
  useEffect(() => {
    if (!detail) return;
    setDetailForm(formFromRow(detail));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [detail?.id]);

  const canEditDetail = canManage
    && detail
    && detail.status !== "Verified"
    && detail.status !== "Cancelled";

  const submitDetail = async () => {
    if (!detail || !detailForm) return;
    setDetailError(null);
    if (!detailForm.title.trim()) { setDetailError(t("punch.form.titleRequired")); return; }
    setDetailSaving(true);
    try {
      const body: UpdatePunchItemRequest = {
        title: detailForm.title.trim(),
        description: detailForm.description.trim() || undefined,
        location: detailForm.location.trim() || undefined,
        severity: detailForm.severity,
        assigneeUserId: detailForm.assigneeUserId ?? undefined,
        deadline: detailForm.deadline || undefined,
        resolutionNote: detailForm.resolutionNote.trim() || undefined,
        note: detailForm.note.trim() || undefined,
      };
      const { data: updated } = await adminApi.updatePunchItem(detail.id, body);
      toast({ title: t("punch.updated") });
      setDetail(updated);
      await fetchList();
    } catch (err) {
      setDetailError(extractApiError(err));
    } finally {
      setDetailSaving(false);
    }
  };

  // ---- transitions ----
  const changeStatus = async (row: PunchItemResponse, next: PunchStatus) => {
    try {
      const { data: updated } = await adminApi.transitionPunchStatus(row.id, { status: next });
      toast({ title: t("punch.statusChanged") });
      if (detail?.id === row.id) setDetail(updated);
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    }
  };

  const runConfirmAction = async () => {
    const { kind, row } = confirmAction;
    if (!kind || !row) return;
    setActionBusy(true);
    try {
      let updated: PunchItemResponse | null = null;
      if (kind === "verify") {
        const { data } = await adminApi.verifyPunchItem(row.id);
        updated = data;
        toast({ title: t("punch.verified") });
      } else if (kind === "reopen") {
        const { data } = await adminApi.transitionPunchStatus(row.id, { status: "Open" });
        updated = data;
        toast({ title: t("punch.reopened") });
      } else if (kind === "delete") {
        await adminApi.deletePunchItem(row.id);
        toast({ title: t("punch.deleted") });
        if (detail?.id === row.id) closeDetail();
      }
      if (updated && detail?.id === updated.id) setDetail(updated);
      setConfirmAction({ kind: null, row: null });
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setActionBusy(false);
    }
  };

  // ---- bulk delete ----
  const toggleSelect = (id: number, checked: boolean) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id); else next.delete(id);
      return next;
    });
  };
  const toggleSelectAll = (checked: boolean) => {
    if (checked) setSelected(new Set(rows.map((r) => r.id)));
    else setSelected(new Set());
  };
  const submitBulkDelete = async () => {
    if (selected.size === 0) return;
    setBulkDeleting(true);
    try {
      const { data } = await adminApi.bulkDeletePunchItems({ ids: Array.from(selected) });
      toast({
        title: t("punch.bulkDeleted")
          .replace("{deleted}", String(data.deleted))
          .replace("{requested}", String(data.requested)),
      });
      if (data.failures.length > 0) {
        const MAX_SHOWN = 3;
        const shown = data.failures.slice(0, MAX_SHOWN).map((f) => `#${f.id}: ${f.message}`).join(" · ");
        const rest = data.failures.length - MAX_SHOWN;
        toast({
          title: t("common.warning"),
          description: rest > 0 ? `${shown} … (+${rest})` : shown,
          variant: "destructive",
        });
      }
      setConfirmBulkDelete(false);
      setSelected(new Set());
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setBulkDeleting(false);
    }
  };

  // ---- render helpers ----
  const renderBadges = (row: PunchItemResponse) => (
    <div className="flex flex-wrap items-center gap-1">
      <Badge variant="outline" className={cn(SEVERITY_BADGE[row.severity])}>
        {t(`punch.severity.${row.severity}`)}
      </Badge>
      <Badge variant="outline" className={cn(STATUS_BADGE[row.status])}>
        {t(`punch.status.${row.status}`)}
      </Badge>
      {row.isOverdue && (
        <Badge variant="outline" className={OVERDUE_BADGE}>
          {t("punch.status.Overdue")}
        </Badge>
      )}
    </div>
  );

  const confirmTitle = () => {
    switch (confirmAction.kind) {
      case "verify": return t("punch.action.verify");
      case "reopen": return t("punch.action.reopen");
      case "delete": return t("punch.action.delete");
      default: return "";
    }
  };
  const confirmMessage = () => {
    const { kind, row } = confirmAction;
    if (!kind || !row) return "";
    const code = row.punchCode;
    switch (kind) {
      case "verify": return t("punch.confirmVerify").replace("{code}", code);
      case "reopen": return t("punch.confirmReopen").replace("{code}", code);
      case "delete": return t("punch.confirmDelete").replace("{code}", code);
      default: return "";
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-6" data-testid="punch-page">
        {/* header */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900">{t("punch.title")}</h1>
            <p className="text-sm text-slate-500">{t("punch.subtitle")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => void fetchList()}>
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button size="sm" onClick={openCreate} data-testid="punch-new">
                <ShieldAlert className="mr-2 h-4 w-4" />
                {t("punch.new")}
              </Button>
            )}
          </div>
        </div>

        {/* stats */}
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          <StatTile icon={<ShieldAlert className="h-6 w-6" />} label={t("punch.stat.total")} value={total} gradient="from-indigo-500 to-violet-600" />
          <StatTile icon={<CircleDot className="h-6 w-6" />} label={t("punch.stat.open")} value={statusPill("Open")} gradient="from-slate-500 to-slate-700" />
          <StatTile icon={<Wrench className="h-6 w-6" />} label={t("punch.stat.inProgress")} value={statusPill("InProgress")} gradient="from-sky-500 to-blue-600" />
          <StatTile icon={<AlertTriangle className="h-6 w-6" />} label={t("punch.stat.overdue")} value={overdueCount} gradient="from-amber-500 to-orange-600" />
        </div>

        {/* filters */}
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 [&>div]:min-w-0">
            <div>
              <Label className="text-xs text-slate-500">{t("punch.field.project")}</Label>
              <SearchableSelect
                value={projectId == null ? "" : String(projectId)}
                onChange={(v) => { setProjectId(v ? Number(v) : null); setPage(1); }}
                options={projectOptions}
                placeholder={t("punch.filter.allProjects")}
              />
            </div>
            <div>
              <Label className="text-xs text-slate-500">{t("punch.field.status")}</Label>
              <Select value={status || "__all__"} onValueChange={(v) => { setStatus(v === "__all__" ? "" : (v as PunchStatus)); setPage(1); }}>
                <SelectTrigger><SelectValue placeholder={t("punch.filter.allStatuses")} /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("punch.filter.allStatuses")}</SelectItem>
                  {PUNCH_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>{t(`punch.status.${s}`)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label className="text-xs text-slate-500">{t("punch.field.severity")}</Label>
              <Select value={severity || "__all__"} onValueChange={(v) => { setSeverity(v === "__all__" ? "" : (v as PunchSeverity)); setPage(1); }}>
                <SelectTrigger><SelectValue placeholder={t("punch.filter.allSeverities")} /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("punch.filter.allSeverities")}</SelectItem>
                  {PUNCH_SEVERITIES.map((s) => (
                    <SelectItem key={s} value={s}>{t(`punch.severity.${s}`)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label className="text-xs text-slate-500">{t("punch.field.assignee")}</Label>
              {canListUsers ? (
                <SearchableSelect
                  value={assigneeUserId == null ? "" : String(assigneeUserId)}
                  onChange={(v) => { setAssigneeUserId(v ? Number(v) : null); setPage(1); }}
                  options={userFilterOptions}
                  placeholder={t("punch.filter.allAssignees")}
                />
              ) : (
                <Input value="—" disabled />
              )}
            </div>
          </div>
          <div className="mt-3 grid gap-3 sm:grid-cols-2 [&>div]:min-w-0">
            <div>
              <Label className="text-xs text-slate-500">{t("punch.filter.search")}</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-2 top-2.5 h-4 w-4 text-slate-400" />
                <Input
                  className="pl-8"
                  value={search}
                  onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                  placeholder={t("punch.filter.search")}
                  data-testid="punch-search"
                />
              </div>
            </div>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-3">
            <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-600">
              <Checkbox checked={openOnly} onCheckedChange={(v) => { setOpenOnly(!!v); setPage(1); }} />
              {t("punch.filter.openOnly")}
            </label>
            <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-600">
              <Checkbox checked={overdueOnly} onCheckedChange={(v) => { setOverdueOnly(!!v); setPage(1); }} />
              {t("punch.filter.overdueOnly")}
            </label>
            {hasActiveFilter && (
              <Button variant="ghost" size="sm" onClick={resetFilters}>{t("common.reset")}</Button>
            )}
            {canManage && selected.size > 0 && (
              <Button variant="destructive" size="sm" onClick={() => setConfirmBulkDelete(true)} className="ml-auto" data-testid="punch-bulk-delete">
                <Trash2 className="mr-2 h-4 w-4" />
                {t("punch.action.bulkDelete")} ({selected.size})
              </Button>
            )}
          </div>
        </div>

        {/* body */}
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : rows.length === 0 ? (
          <div className="rounded-lg border border-dashed border-slate-300 bg-white p-10 text-center text-sm text-slate-500">
            {t("punch.empty")}
          </div>
        ) : (
          <>
            {/* Mobile cards (< md) */}
            <div className="space-y-3 md:hidden">
              {rows.map((row) => (
                <div
                  key={`m-${row.id}`}
                  role="button"
                  tabIndex={0}
                  onClick={() => openDetail(row)}
                  onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); openDetail(row); } }}
                  className={cn(
                    "relative rounded-xl border border-slate-200 bg-white p-4 text-left shadow-sm transition hover:border-slate-300 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-slate-300",
                    row.isOverdue && "border-amber-300 bg-amber-50/40",
                  )}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="font-mono text-xs text-slate-500">{row.punchCode}</div>
                      <div className="mt-1 line-clamp-2 text-base font-semibold text-slate-900">{row.title}</div>
                    </div>
                    {canManage && (
                      <div onClick={(e) => e.stopPropagation()} role="presentation">
                        <Checkbox
                          checked={selected.has(row.id)}
                          onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                          aria-label={`m-select-${row.punchCode}`}
                          className="mt-1"
                        />
                      </div>
                    )}
                  </div>
                  <div className="mt-2">{renderBadges(row)}</div>
                  {row.location && (
                    <div className="mt-2 inline-flex items-center gap-1 text-xs text-slate-600">
                      <MapPin className="h-3 w-3" /> {row.location}
                    </div>
                  )}
                  <div className="mt-2 grid grid-cols-2 gap-x-2 gap-y-1 text-xs text-slate-600">
                    <div>
                      <div className="text-[10px] uppercase tracking-wide text-slate-400">{t("punch.field.assignee")}</div>
                      <div>{row.assigneeName ?? "—"}</div>
                    </div>
                    <div>
                      <div className="text-[10px] uppercase tracking-wide text-slate-400">{t("punch.field.deadline")}</div>
                      <div>{formatDate(row.deadline, lang)}</div>
                    </div>
                  </div>
                </div>
              ))}
              <div className="flex items-center justify-between px-1 pt-1 text-xs text-slate-500">
                <span>{t("common.showing")} {rows.length} / {total}</span>
                <div className="flex gap-1">
                  <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>←</Button>
                  <span className="px-2 py-1">{page} / {totalPages}</span>
                  <Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>→</Button>
                </div>
              </div>
            </div>

            {/* Desktop table (md+) */}
            <div className="hidden overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm md:block">
              <div className="max-h-[70vh] overflow-auto">
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead className="sticky top-0 z-10 bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      {canManage && (
                        <th className="w-8 px-3 py-2">
                          <Checkbox
                            aria-label={t("punch.action.selectAll")}
                            checked={rows.length > 0 && selected.size === rows.length}
                            onCheckedChange={(v) => toggleSelectAll(!!v)}
                            data-testid="punch-select-all"
                          />
                        </th>
                      )}
                      <th className="px-3 py-2">{t("punch.field.code")}</th>
                      <th className="px-3 py-2">{t("punch.field.title")}</th>
                      <th className="px-3 py-2">{t("punch.field.severity")}</th>
                      <th className="px-3 py-2">{t("punch.field.status")}</th>
                      <th className="px-3 py-2">{t("punch.field.assignee")}</th>
                      <th className="px-3 py-2">{t("punch.field.deadline")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {rows.map((row) => (
                      <tr
                        key={row.id}
                        className={cn("cursor-pointer hover:bg-slate-50", row.isOverdue && "bg-amber-50/40")}
                        onClick={() => openDetail(row)}
                        data-testid={`punch-row-${row.id}`}
                      >
                        {canManage && (
                          <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                            <Checkbox
                              checked={selected.has(row.id)}
                              onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                              aria-label={`select-${row.punchCode}`}
                            />
                          </td>
                        )}
                        <td className="px-3 py-2 font-mono text-xs text-slate-600">{row.punchCode}</td>
                        <td className="px-3 py-2">
                          <div className="font-medium text-slate-900">{row.title}</div>
                          {row.location && (
                            <div className="mt-0.5 inline-flex items-center gap-1 text-[11px] text-slate-500">
                              <MapPin className="h-3 w-3" /> {row.location}
                            </div>
                          )}
                        </td>
                        <td className="px-3 py-2">
                          <Badge variant="outline" className={cn(SEVERITY_BADGE[row.severity])}>
                            {t(`punch.severity.${row.severity}`)}
                          </Badge>
                        </td>
                        <td className="px-3 py-2">
                          <div className="flex flex-wrap items-center gap-1">
                            <Badge variant="outline" className={cn(STATUS_BADGE[row.status])}>
                              {t(`punch.status.${row.status}`)}
                            </Badge>
                            {row.isOverdue && (
                              <Badge variant="outline" className={OVERDUE_BADGE}>
                                {t("punch.status.Overdue")}
                              </Badge>
                            )}
                          </div>
                        </td>
                        <td className="px-3 py-2 text-xs text-slate-600">{row.assigneeName ?? "—"}</td>
                        <td className="px-3 py-2 text-xs text-slate-600">{formatDate(row.deadline, lang)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="flex items-center justify-between border-t border-slate-100 bg-slate-50 px-3 py-2 text-xs text-slate-500">
                <span>{t("common.showing")} {rows.length} / {total}</span>
                <div className="flex gap-1">
                  <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>←</Button>
                  <span className="px-2 py-1">{page} / {totalPages}</span>
                  <Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage((p) => Math.min(totalPages, p + 1))}>→</Button>
                </div>
              </div>
            </div>
          </>
        )}

        {/* create dialog */}
        <Dialog open={creating} onOpenChange={setCreating}>
          <DialogContent className="max-h-[90vh] max-w-lg overflow-y-auto">
            <DialogHeader>
              <DialogTitle>{t("punch.new")}</DialogTitle>
              <DialogDescription>{t("punch.form.hint")}</DialogDescription>
            </DialogHeader>
            <div className="space-y-3">
              <div>
                <Label>{t("punch.field.project")}</Label>
                <SearchableSelect
                  value={createProjectId == null ? "" : String(createProjectId)}
                  onChange={(v) => setCreateProjectId(v ? Number(v) : null)}
                  options={projectOptionsForCreate}
                  placeholder="—"
                />
              </div>
              <div>
                <Label>{t("punch.field.title")}</Label>
                <Input
                  value={createForm.title}
                  onChange={(e) => setCreateForm((f) => ({ ...f, title: e.target.value }))}
                  data-testid="punch-new-title"
                />
              </div>
              <div>
                <Label>{t("punch.field.description")}</Label>
                <Textarea rows={2} value={createForm.description} onChange={(e) => setCreateForm((f) => ({ ...f, description: e.target.value }))} />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>{t("punch.field.location")}</Label>
                  <Input value={createForm.location} onChange={(e) => setCreateForm((f) => ({ ...f, location: e.target.value }))} />
                </div>
                <div>
                  <Label>{t("punch.field.severity")}</Label>
                  <Select value={createForm.severity} onValueChange={(v) => setCreateForm((f) => ({ ...f, severity: v as PunchSeverity }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {PUNCH_SEVERITIES.map((s) => (
                        <SelectItem key={s} value={s}>{t(`punch.severity.${s}`)}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>{t("punch.field.assignee")}</Label>
                  {canListUsers ? (
                    <SearchableSelect
                      value={createForm.assigneeUserId == null ? "" : String(createForm.assigneeUserId)}
                      onChange={(v) => setCreateForm((f) => ({ ...f, assigneeUserId: v ? Number(v) : null }))}
                      options={userFormOptions}
                      placeholder="—"
                    />
                  ) : (
                    <Input value="—" disabled />
                  )}
                </div>
                <div>
                  <Label>{t("punch.field.deadline")}</Label>
                  <Input type="date" value={createForm.deadline} onChange={(e) => setCreateForm((f) => ({ ...f, deadline: e.target.value }))} />
                </div>
              </div>
              <div>
                <Label>{t("punch.field.note")}</Label>
                <Textarea rows={2} value={createForm.note} onChange={(e) => setCreateForm((f) => ({ ...f, note: e.target.value }))} />
              </div>
              {createError && <div className="text-sm text-rose-600">{createError}</div>}
            </div>
            <DialogFooter>
              <Button variant="ghost" onClick={() => setCreating(false)} disabled={saving}>{t("common.cancel")}</Button>
              <Button onClick={submitCreate} disabled={saving} data-testid="punch-new-save">{saving ? t("common.saving") : t("common.save")}</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* detail sheet */}
        <Sheet open={detail !== null} onOpenChange={(open) => { if (!open) closeDetail(); }}>
          <SheetContent side="right" className="w-full max-w-xl overflow-y-auto sm:max-w-xl">
            {detail && detailForm && (
              <>
                <SheetHeader>
                  <SheetTitle>{detail.punchCode} — {detail.title}</SheetTitle>
                  <SheetDescription className="text-xs text-slate-500">
                    {detail.designProjectCode} · {detail.designProjectName}
                  </SheetDescription>
                </SheetHeader>
                <div className="mt-4 space-y-4">
                  <div className="flex flex-wrap items-center gap-2">{renderBadges(detail)}</div>
                  {detail.reopenCount > 0 && (
                    <div className="text-xs text-slate-500">
                      {t("punch.field.reopenCount")}: <span className="font-semibold">{detail.reopenCount}</span>
                    </div>
                  )}

                  <div>
                    <Label>{t("punch.field.title")}</Label>
                    <Input value={detailForm.title} onChange={(e) => setDetailForm((f) => f && { ...f, title: e.target.value })} disabled={!canEditDetail} />
                  </div>
                  <div>
                    <Label>{t("punch.field.description")}</Label>
                    <Textarea rows={3} value={detailForm.description} onChange={(e) => setDetailForm((f) => f && { ...f, description: e.target.value })} disabled={!canEditDetail} />
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label>{t("punch.field.location")}</Label>
                      <Input value={detailForm.location} onChange={(e) => setDetailForm((f) => f && { ...f, location: e.target.value })} disabled={!canEditDetail} />
                    </div>
                    <div>
                      <Label>{t("punch.field.severity")}</Label>
                      <Select value={detailForm.severity} onValueChange={(v) => setDetailForm((f) => f && { ...f, severity: v as PunchSeverity })} disabled={!canEditDetail}>
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {PUNCH_SEVERITIES.map((s) => (
                            <SelectItem key={s} value={s}>{t(`punch.severity.${s}`)}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div>
                      <Label>{t("punch.field.assignee")}</Label>
                      {canListUsers ? (
                        <SearchableSelect
                          value={detailForm.assigneeUserId == null ? "" : String(detailForm.assigneeUserId)}
                          onChange={(v) => setDetailForm((f) => f && { ...f, assigneeUserId: v ? Number(v) : null })}
                          options={userFormOptions}
                          placeholder="—"
                        />
                      ) : (
                        <Input value={detail.assigneeName ?? "—"} disabled />
                      )}
                    </div>
                    <div>
                      <Label>{t("punch.field.deadline")}</Label>
                      <Input type="date" value={detailForm.deadline} onChange={(e) => setDetailForm((f) => f && { ...f, deadline: e.target.value })} disabled={!canEditDetail} />
                    </div>
                  </div>
                  <div>
                    <Label>{t("punch.field.resolutionNote")}</Label>
                    <Textarea rows={2} value={detailForm.resolutionNote} onChange={(e) => setDetailForm((f) => f && { ...f, resolutionNote: e.target.value })} disabled={!canEditDetail} />
                  </div>
                  <div>
                    <Label>{t("punch.field.note")}</Label>
                    <Textarea rows={2} value={detailForm.note} onChange={(e) => setDetailForm((f) => f && { ...f, note: e.target.value })} disabled={!canEditDetail} />
                  </div>

                  {detail.verifiedByName && (
                    <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-800">
                      <div className="flex items-center gap-1 font-semibold">
                        <ShieldCheck className="h-4 w-4" /> {t("punch.field.verifiedBy")}
                      </div>
                      <div>{detail.verifiedByName} · {detail.verifiedAt && new Date(detail.verifiedAt).toLocaleString(lang)}</div>
                    </div>
                  )}

                  {detailError && <div className="text-sm text-rose-600">{detailError}</div>}

                  <div className="flex flex-wrap items-center justify-between gap-2 pt-2">
                    <div className="flex flex-wrap gap-2">
                      {canManage && detail.status === "Open" && (
                        <Button variant="destructive" size="sm" onClick={() => setConfirmAction({ kind: "delete", row: detail })} data-testid="punch-detail-delete">
                          <Trash2 className="mr-2 h-4 w-4" />
                          {t("punch.action.delete")}
                        </Button>
                      )}
                      {canManage && detail.status === "Open" && (
                        <Button variant="outline" size="sm" onClick={() => void changeStatus(detail, "InProgress")} data-testid="punch-start">
                          <Play className="mr-2 h-4 w-4" />
                          {t("punch.action.startProgress")}
                        </Button>
                      )}
                      {canManage && detail.status === "InProgress" && (
                        <Button variant="outline" size="sm" onClick={() => void changeStatus(detail, "Fixed")} data-testid="punch-fix">
                          <Wrench className="mr-2 h-4 w-4" />
                          {t("punch.action.markFixed")}
                        </Button>
                      )}
                      {canVerify && detail.status === "Fixed" && (
                        <Button variant="outline" size="sm" onClick={() => setConfirmAction({ kind: "verify", row: detail })} data-testid="punch-verify">
                          <ShieldCheck className="mr-2 h-4 w-4" />
                          {t("punch.action.verify")}
                        </Button>
                      )}
                      {canManage && (detail.status === "Fixed" || detail.status === "Verified" || detail.status === "InProgress") && detail.status !== "Cancelled" && (
                        <Button variant="outline" size="sm" onClick={() => setConfirmAction({ kind: "reopen", row: detail })} data-testid="punch-reopen">
                          <RotateCcw className="mr-2 h-4 w-4" />
                          {t("punch.action.reopen")}
                        </Button>
                      )}
                      {canManage && (detail.status === "Open" || detail.status === "InProgress" || detail.status === "Fixed") && (
                        <Button variant="ghost" size="sm" onClick={() => void changeStatus(detail, "Cancelled")} data-testid="punch-cancel">
                          {t("punch.action.cancel")}
                        </Button>
                      )}
                    </div>
                    <div className="flex gap-2">
                      <Button variant="ghost" onClick={closeDetail} disabled={detailSaving}>{t("common.close")}</Button>
                      {canEditDetail && (
                        <Button onClick={submitDetail} disabled={detailSaving} data-testid="punch-detail-save">
                          {detailSaving ? t("common.saving") : t("common.save")}
                        </Button>
                      )}
                    </div>
                  </div>
                </div>
              </>
            )}
          </SheetContent>
        </Sheet>

        {/* single-action confirm */}
        <AlertDialog open={confirmAction.kind !== null} onOpenChange={(v) => !v && setConfirmAction({ kind: null, row: null })}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{confirmTitle()}</AlertDialogTitle>
              <AlertDialogDescription>{confirmMessage()}</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={actionBusy}>{t("common.cancel")}</AlertDialogCancel>
              <AlertDialogAction onClick={runConfirmAction} disabled={actionBusy} data-testid="punch-action-confirm">
                {actionBusy ? t("common.saving") : t("common.confirm")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* bulk delete confirm */}
        <AlertDialog open={confirmBulkDelete} onOpenChange={setConfirmBulkDelete}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t("punch.action.bulkDelete")}</AlertDialogTitle>
              <AlertDialogDescription>
                {t("punch.confirmBulkDelete").replace("{count}", String(selected.size))}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={bulkDeleting}>{t("common.cancel")}</AlertDialogCancel>
              <AlertDialogAction onClick={submitBulkDelete} disabled={bulkDeleting} data-testid="punch-bulk-delete-confirm">
                {bulkDeleting ? t("common.saving") : t("common.delete")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </AdminLayout>
  );
};

// ------------------ small building blocks ------------------

interface StatTileProps {
  icon: React.ReactNode;
  label: string;
  value: number;
  gradient: string;
}

const StatTile = ({ icon, label, value, gradient }: StatTileProps) => (
  <div className={cn("relative overflow-hidden rounded-xl bg-gradient-to-br p-4 text-white shadow-sm", gradient)}>
    <div className="relative z-10 flex items-start justify-between gap-2">
      <div className="min-w-0">
        <div className="text-sm font-medium text-white/85">{label}</div>
        <div className="mt-1 text-3xl font-bold tabular-nums leading-none">{value}</div>
      </div>
      <div className="grid h-11 w-11 shrink-0 place-items-center rounded-lg bg-white/20 backdrop-blur-sm">
        {icon}
      </div>
    </div>
    <div aria-hidden className="pointer-events-none absolute -right-6 -top-6 h-24 w-24 rounded-full bg-white/10" />
  </div>
);

export default AdminPunchList;
