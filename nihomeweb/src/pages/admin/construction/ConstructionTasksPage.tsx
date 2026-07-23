import { useCallback, useEffect, useMemo, useState, Fragment } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  CircleDot,
  HardHat,
  ListChecks,
  Plus,
  RefreshCcw,
  Search,
  Trash2,
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
import { Progress } from "@/components/ui/progress";
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
  type ConstructionTaskListParams,
  type ConstructionTaskResponse,
  type ConstructionTaskStatus,
  type CreateConstructionTaskRequest,
  type DesignProjectListItemResponse,
  type UpdateConstructionTaskRequest,
} from "@/services/adminApi";

const CONSTRUCTION_STATUSES: ConstructionTaskStatus[] = [
  "Planned",
  "InProgress",
  "Completed",
  "Cancelled",
];

const STATUS_BADGE: Record<ConstructionTaskStatus, string> = {
  Planned: "border-slate-200 bg-slate-50 text-slate-700",
  InProgress: "border-sky-200 bg-sky-50 text-sky-700",
  Completed: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
};

const OVERDUE_BADGE = "border-amber-300 bg-amber-50 text-amber-800";

interface UserOption {
  id: number;
  fullName: string;
}

const todayIso = () => new Date().toISOString().slice(0, 10);
const isoAfter = (base: string, days: number) => {
  const d = new Date(base);
  d.setDate(d.getDate() + days);
  return d.toISOString().slice(0, 10);
};

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

interface CreateForm {
  designProjectId: number | null;
  taskCode: string;
  wbs: string;
  name: string;
  description: string;
  plannedStart: string;
  plannedEnd: string;
  ownerUserId: number | null;
}

interface EditForm {
  wbs: string;
  name: string;
  description: string;
  plannedStart: string;
  plannedEnd: string;
  actualStart: string;
  actualEnd: string;
  progressPercent: number;
  ownerUserId: number | null;
  status: ConstructionTaskStatus;
}

const emptyCreateForm = (defaultProjectId: number | null): CreateForm => ({
  designProjectId: defaultProjectId,
  taskCode: "",
  wbs: "",
  name: "",
  description: "",
  plannedStart: todayIso(),
  plannedEnd: isoAfter(todayIso(), 7),
  ownerUserId: null,
});

const editFormFrom = (row: ConstructionTaskResponse): EditForm => ({
  wbs: row.wbs ?? "",
  name: row.name,
  description: row.description ?? "",
  plannedStart: row.plannedStart,
  plannedEnd: row.plannedEnd,
  actualStart: row.actualStart ?? "",
  actualEnd: row.actualEnd ?? "",
  progressPercent: row.progressPercent,
  ownerUserId: row.ownerUserId ?? null,
  status: row.status,
});

// ---------------------------- Gantt helpers ----------------------------

interface GanttRow {
  task: ConstructionTaskResponse;
  offsetDays: number;
  spanDays: number;
}

const buildGantt = (tasks: ConstructionTaskResponse[]) => {
  if (tasks.length === 0) return { rows: [] as GanttRow[], totalDays: 0, startIso: "" };
  const starts = tasks.map((t) => new Date(t.plannedStart).getTime());
  const ends = tasks.map((t) =>
    Math.max(
      new Date(t.plannedEnd).getTime(),
      t.actualEnd ? new Date(t.actualEnd).getTime() : 0,
    ),
  );
  const min = Math.min(...starts);
  const max = Math.max(...ends);
  const totalDays = Math.max(1, Math.round((max - min) / 86_400_000) + 1);
  const rows = tasks.map((task) => {
    const start = new Date(task.plannedStart).getTime();
    const end = Math.max(
      new Date(task.plannedEnd).getTime(),
      task.actualEnd ? new Date(task.actualEnd).getTime() : 0,
    );
    return {
      task,
      offsetDays: Math.max(0, Math.round((start - min) / 86_400_000)),
      spanDays: Math.max(1, Math.round((end - start) / 86_400_000) + 1),
    };
  });
  return { rows, totalDays, startIso: new Date(min).toISOString().slice(0, 10) };
};

// ------------------------------- page ---------------------------------

const AdminConstructionTasks = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionTasksManage);
  const canListUsers = has(ADMIN_PERMS.users);

  // list state
  const [rows, setRows] = useState<ConstructionTaskResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<ConstructionTaskStatus, number>>>({});
  const [overdueCount, setOverdueCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const [projectId, setProjectId] = useState<number | null>(null);
  const [status, setStatus] = useState<ConstructionTaskStatus | "">("");
  const [ownerUserId, setOwnerUserId] = useState<number | null>(null);
  const [search, setSearch] = useState("");
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [page, setPage] = useState(1);
  const pageSize = 50;
  const [view, setView] = useState<"list" | "gantt">("list");

  // selection for bulk delete
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [confirmBulkDelete, setConfirmBulkDelete] = useState(false);
  const [bulkDeleting, setBulkDeleting] = useState(false);

  // dialog / sheet
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState<CreateForm>(() => emptyCreateForm(null));
  const [saving, setSaving] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const [detail, setDetail] = useState<ConstructionTaskResponse | null>(null);
  const [detailForm, setDetailForm] = useState<EditForm | null>(null);
  const [detailPreds, setDetailPreds] = useState<number[]>([]);
  /**
   * Full sibling pool for the predecessor picker — fetched fresh every
   * time a detail sheet opens so the options include tasks living on
   * pages other than the currently visible one, and don’t drop when
   * the user has an active status/search filter.
   */
  const [detailSiblings, setDetailSiblings] = useState<ConstructionTaskResponse[]>([]);
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [deleting, setDeleting] = useState<ConstructionTaskResponse | null>(null);
  const [deletingBusy, setDeletingBusy] = useState(false);

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
      } catch {
        // non-fatal
      }
      if (canListUsers) {
        try {
          const { data } = await adminApi.getUsers({ take: 200 });
          if (!cancelled) {
            setUsers(
              (data.items ?? []).map((u) => ({
                id: u.id,
                fullName: u.fullName ?? u.email ?? `#${u.id}`,
              })),
            );
          }
        } catch {
          /* ignore */
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canListUsers]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: ConstructionTaskListParams = { page, pageSize };
      if (projectId != null) params.designProjectId = projectId;
      if (status) params.status = status;
      if (ownerUserId != null) params.ownerUserId = ownerUserId;
      if (search.trim()) params.search = search.trim();
      if (overdueOnly) params.overdueOnly = true;
      const { data } = await adminApi.listConstructionTasks(params);
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
      setStatusCounts(data.statusCounts ?? {});
      setOverdueCount(data.overdueCount ?? 0);
      // Prune selection to only visible ids so the bulk-delete count
      // doesn't secretly include rows that have since dropped off.
      const visibleIds = new Set((data.items ?? []).map((r) => r.id));
      setSelected((prev) => {
        const next = new Set<number>();
        prev.forEach((id) => {
          if (visibleIds.has(id)) next.add(id);
        });
        return next;
      });
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
  }, [page, pageSize, projectId, status, ownerUserId, search, overdueOnly, t, toast]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const projectOptions = useMemo(
    () => [
      // Explicit "All projects" so users can clear the filter after
      // picking one. Empty-string sentinel matches SearchableSelect's
      // convention for `no selection`.
      { value: "", label: t("constructionTasks.filter.allProjects") },
      ...projects.map((p) => ({
        value: String(p.id),
        label: `${p.projectCode} — ${p.name}`,
      })),
    ],
    [projects, t],
  );
  const userOptions = useMemo(
    () => [
      { value: "", label: t("constructionTasks.filter.allOwners") },
      ...users.map((u) => ({ value: String(u.id), label: u.fullName })),
    ],
    [users, t],
  );

  const resetFilters = () => {
    setProjectId(null);
    setStatus("");
    setOwnerUserId(null);
    setSearch("");
    setOverdueOnly(false);
    setPage(1);
  };

  // -------- create --------
  const openCreate = () => {
    setCreateForm(emptyCreateForm(projectId));
    setCreateError(null);
    setCreating(true);
  };
  const submitCreate = async () => {
    setCreateError(null);
    if (createForm.designProjectId == null) {
      setCreateError(t("constructionTasks.form.projectRequired"));
      return;
    }
    if (!createForm.name.trim()) {
      setCreateError(t("constructionTasks.form.nameRequired"));
      return;
    }
    if (createForm.plannedEnd < createForm.plannedStart) {
      setCreateError(t("constructionTasks.form.endBeforeStart"));
      return;
    }
    setSaving(true);
    try {
      const body: CreateConstructionTaskRequest = {
        designProjectId: createForm.designProjectId,
        name: createForm.name.trim(),
        plannedStart: createForm.plannedStart,
        plannedEnd: createForm.plannedEnd,
        taskCode: createForm.taskCode.trim() || undefined,
        wbs: createForm.wbs.trim() || undefined,
        description: createForm.description.trim() || undefined,
        ownerUserId: createForm.ownerUserId ?? undefined,
      };
      await adminApi.createConstructionTask(body);
      toast({ title: t("constructionTasks.created") });
      setCreating(false);
      await fetchList();
    } catch (err) {
      setCreateError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // -------- detail / edit --------
  const openDetail = async (row: ConstructionTaskResponse) => {
    setDetail(row);
    setDetailForm(editFormFrom(row));
    setDetailPreds(row.predecessors.map((p) => p.predecessorTaskId));
    setDetailError(null);
    // Pull the whole project’s task set for the predecessor picker
    // (independent of the current filter/pagination). Fire-and-forget
    // — the picker falls back to the visible rows while it loads.
    try {
      const { data } = await adminApi.listConstructionTasks({
        designProjectId: row.designProjectId,
        pageSize: 200,
      });
      setDetailSiblings(data.items ?? []);
    } catch {
      setDetailSiblings([]);
    }
  };
  const closeDetail = () => {
    setDetail(null);
    setDetailForm(null);
    setDetailPreds([]);
    setDetailSiblings([]);
    setDetailError(null);
  };

  // Safety net: if a rapid click changes `detail` before we set the form
  // and pred set in `openDetail`, this resyncs on the id change so the
  // sheet never renders stale data.
  useEffect(() => {
    if (!detail) return;
    setDetailForm(editFormFrom(detail));
    setDetailPreds(detail.predecessors.map((p) => p.predecessorTaskId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [detail?.id]);

  const submitDetail = async () => {
    if (!detail || !detailForm) return;
    setDetailError(null);
    if (!detailForm.name.trim()) {
      setDetailError(t("constructionTasks.form.nameRequired"));
      return;
    }
    if (detailForm.plannedEnd < detailForm.plannedStart) {
      setDetailError(t("constructionTasks.form.endBeforeStart"));
      return;
    }
    setDetailSaving(true);
    try {
      const body: UpdateConstructionTaskRequest = {
        wbs: detailForm.wbs.trim() || undefined,
        name: detailForm.name.trim(),
        description: detailForm.description.trim() || undefined,
        plannedStart: detailForm.plannedStart,
        plannedEnd: detailForm.plannedEnd,
        actualStart: detailForm.actualStart || undefined,
        actualEnd: detailForm.actualEnd || undefined,
        progressPercent: detailForm.progressPercent,
        ownerUserId: detailForm.ownerUserId ?? undefined,
        status: detailForm.status,
      };
      const { data: updated } = await adminApi.updateConstructionTask(detail.id, body);
      // Only push predecessors if the operator actually changed the set,
      // so we don't add a redundant audit entry on plain edits.
      const originalPreds = new Set(detail.predecessors.map((p) => p.predecessorTaskId));
      const nextPreds = new Set(detailPreds);
      const changed = originalPreds.size !== nextPreds.size
        || [...nextPreds].some((id) => !originalPreds.has(id));
      let latest = updated;
      if (changed) {
        const resp = await adminApi.setConstructionTaskPredecessors(detail.id, {
          predecessorTaskIds: detailPreds,
        });
        latest = resp.data;
      }
      toast({ title: t("constructionTasks.updated") });
      setDetail(latest);
      setDetailForm(editFormFrom(latest));
      setDetailPreds(latest.predecessors.map((p) => p.predecessorTaskId));
      await fetchList();
    } catch (err) {
      setDetailError(extractApiError(err));
    } finally {
      setDetailSaving(false);
    }
  };

  // -------- delete --------
  const submitDelete = async () => {
    if (!deleting) return;
    setDeletingBusy(true);
    try {
      await adminApi.deleteConstructionTask(deleting.id);
      toast({ title: t("constructionTasks.deleted") });
      setDeleting(null);
      if (detail?.id === deleting.id) closeDetail();
      await fetchList();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setDeletingBusy(false);
    }
  };

  // -------- bulk delete --------
  const toggleSelect = (id: number, checked: boolean) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
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
      const { data } = await adminApi.bulkDeleteConstructionTasks({
        ids: Array.from(selected),
      });
      toast({
        title: t("constructionTasks.bulkDeleted")
          .replace("{deleted}", String(data.deleted))
          .replace("{requested}", String(data.requested)),
      });
      if (data.failures.length > 0) {
        // Cap the toast body so a 100-row bulk-delete doesn’t drown the
        // corner — keep the first 3 messages + a count of the rest.
        const MAX_SHOWN = 3;
        const shown = data.failures
          .slice(0, MAX_SHOWN)
          .map((f) => `#${f.id}: ${f.message}`)
          .join(" · ");
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
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setBulkDeleting(false);
    }
  };

  // -------- render helpers --------
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const hasActiveFilter =
    projectId != null
    || status !== ""
    || ownerUserId != null
    || search.trim().length > 0
    || overdueOnly;

  const statusPillCount = (s: ConstructionTaskStatus) => statusCounts[s] ?? 0;

  const predecessorOptions = useMemo(() => {
    if (!detail) return [] as { value: string; label: string }[];
    // Prefer the freshly-loaded sibling pool so we include tasks that
    // are outside the current filter/page. Fall back to `rows` while
    // the sibling fetch is in flight.
    const pool = detailSiblings.length > 0 ? detailSiblings : rows;
    return pool
      .filter((r) => r.designProjectId === detail.designProjectId && r.id !== detail.id)
      .map((r) => ({ value: String(r.id), label: `${r.taskCode} — ${r.name}` }));
  }, [detail, detailSiblings, rows]);

  const ganttData = useMemo(() => buildGantt(rows), [rows]);

  const renderStatusBadge = (row: ConstructionTaskResponse) => (
    <div className="flex flex-wrap items-center gap-1">
      <Badge variant="outline" className={cn(STATUS_BADGE[row.status])}>
        {t(`constructionTasks.status.${row.status}`)}
      </Badge>
      {row.isOverdue && (
        <Badge variant="outline" className={OVERDUE_BADGE}>
          {t("constructionTasks.status.Overdue")}
        </Badge>
      )}
    </div>
  );

  return (
    <AdminLayout>
      <div className="space-y-6" data-testid="construction-tasks-page">
        {/* header */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900">
              {t("constructionTasks.title")}
            </h1>
            <p className="text-sm text-slate-500">{t("constructionTasks.subtitle")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setView(view === "list" ? "gantt" : "list")}
              data-testid="construction-view-toggle"
            >
              <ListChecks className="mr-2 h-4 w-4" />
              {view === "list" ? t("constructionTasks.tab.gantt") : t("constructionTasks.tab.list")}
            </Button>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => void fetchList()}
            >
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button
                type="button"
                size="sm"
                onClick={openCreate}
                data-testid="construction-new"
              >
                <Plus className="mr-2 h-4 w-4" />
                {t("constructionTasks.new")}
              </Button>
            )}
          </div>
        </div>

        {/* stats — bold gradient tiles that stay legible on any width */}
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          <StatTile
            icon={<HardHat className="h-6 w-6" />}
            label={t("constructionTasks.stat.total")}
            value={total}
            gradient="from-indigo-500 to-violet-600"
          />
          <StatTile
            icon={<CircleDot className="h-6 w-6" />}
            label={t("constructionTasks.stat.inProgress")}
            value={statusPillCount("InProgress")}
            gradient="from-sky-500 to-blue-600"
          />
          <StatTile
            icon={<CheckCircle2 className="h-6 w-6" />}
            label={t("constructionTasks.stat.completed")}
            value={statusPillCount("Completed")}
            gradient="from-emerald-500 to-teal-600"
          />
          <StatTile
            icon={<AlertTriangle className="h-6 w-6" />}
            label={t("constructionTasks.stat.overdue")}
            value={overdueCount}
            gradient="from-amber-500 to-orange-600"
          />
        </div>

        {/* filters */}
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          {/* `min-w-0` on every grid item so long option labels (project
              codes + names) truncate via SearchableSelect's own `truncate`
              instead of blowing past the viewport on narrow screens. */}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 [&>div]:min-w-0">
            <div>
              <Label className="text-xs text-slate-500">
                {t("constructionTasks.field.project")}
              </Label>
              <SearchableSelect
                value={projectId == null ? "" : String(projectId)}
                onChange={(v) => {
                  setProjectId(v ? Number(v) : null);
                  setPage(1);
                }}
                options={projectOptions}
                placeholder={t("constructionTasks.filter.allProjects")}
              />
            </div>
            <div>
              <Label className="text-xs text-slate-500">
                {t("constructionTasks.field.status")}
              </Label>
              <Select
                value={status || "__all__"}
                onValueChange={(v) => {
                  setStatus(v === "__all__" ? "" : (v as ConstructionTaskStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger data-testid="construction-status-filter">
                  <SelectValue placeholder={t("constructionTasks.filter.allStatuses")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">
                    {t("constructionTasks.filter.allStatuses")}
                  </SelectItem>
                  {CONSTRUCTION_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`constructionTasks.status.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label className="text-xs text-slate-500">
                {t("constructionTasks.field.owner")}
              </Label>
              {canListUsers ? (
                <SearchableSelect
                  value={ownerUserId == null ? "" : String(ownerUserId)}
                  onChange={(v) => {
                    setOwnerUserId(v ? Number(v) : null);
                    setPage(1);
                  }}
                  options={userOptions}
                  placeholder={t("constructionTasks.filter.allOwners")}
                />
              ) : (
                <Input value="—" disabled />
              )}
            </div>
            <div>
              <Label className="text-xs text-slate-500">
                {t("constructionTasks.filter.search")}
              </Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-2 top-2.5 h-4 w-4 text-slate-400" />
                <Input
                  className="pl-8"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder={t("constructionTasks.filter.search")}
                  data-testid="construction-search"
                />
              </div>
            </div>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-3">
            <label className="flex cursor-pointer items-center gap-2 text-sm text-slate-600">
              <Checkbox
                checked={overdueOnly}
                onCheckedChange={(v) => {
                  setOverdueOnly(!!v);
                  setPage(1);
                }}
                data-testid="construction-overdue-only"
              />
              {t("constructionTasks.filter.overdueOnly")}
            </label>
            {hasActiveFilter && (
              <Button variant="ghost" size="sm" onClick={resetFilters}>
                {t("common.reset")}
              </Button>
            )}
            {canManage && selected.size > 0 && (
              <Button
                variant="destructive"
                size="sm"
                onClick={() => setConfirmBulkDelete(true)}
                data-testid="construction-bulk-delete"
                className="ml-auto"
              >
                <Trash2 className="mr-2 h-4 w-4" />
                {t("constructionTasks.action.bulkDelete")} ({selected.size})
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
            {t("constructionTasks.empty")}
          </div>
        ) : view === "list" ? (
          <>
            {/* Mobile cards (< md) — desktop table drops the important
                columns, so this stacks the same fields in a card layout
                that stays tap-friendly. The whole card is clickable to
                open the detail sheet — the checkbox + delete button
                stop propagation so they don't also trigger it. Testids
                are scoped to the desktop table below so the E2E suite
                (desktop viewport) has a single, unambiguous target. */}
            <div className="space-y-3 md:hidden">
              {rows.map((row) => (
                <div
                  key={`m-${row.id}`}
                  role="button"
                  tabIndex={0}
                  onClick={() => void openDetail(row)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" || e.key === " ") {
                      e.preventDefault();
                      void openDetail(row);
                    }
                  }}
                  className={cn(
                    "relative rounded-xl border border-slate-200 bg-white p-4 text-left shadow-sm transition hover:border-slate-300 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-slate-300",
                    row.isOverdue && "border-amber-300 bg-amber-50/40",
                  )}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <span className="font-mono text-xs text-slate-500">
                          {row.taskCode}
                        </span>
                        {row.wbs && (
                          <span className="rounded bg-slate-100 px-1.5 py-0.5 text-[10px] text-slate-500">
                            WBS {row.wbs}
                          </span>
                        )}
                      </div>
                      <div className="mt-1 line-clamp-2 text-base font-semibold text-slate-900">
                        {row.name}
                      </div>
                    </div>
                    {canManage && (
                      <div
                        onClick={(e) => e.stopPropagation()}
                        role="presentation"
                      >
                        <Checkbox
                          checked={selected.has(row.id)}
                          onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                          aria-label={`m-select-${row.taskCode}`}
                          className="mt-1"
                        />
                      </div>
                    )}
                  </div>
                  <div className="mt-2">{renderStatusBadge(row)}</div>
                  {row.predecessors.length > 0 && (
                    <div className="mt-2 text-[11px] text-slate-500">
                      ← {row.predecessors.map((p) => p.predecessorTaskCode).join(", ")}
                    </div>
                  )}
                  <div className="mt-3 flex items-center gap-2">
                    <Progress value={row.progressPercent} className="h-2 flex-1" />
                    <span className="w-10 text-right text-xs font-medium tabular-nums text-slate-700">
                      {row.progressPercent}%
                    </span>
                  </div>
                  <div className="mt-3 grid grid-cols-2 gap-x-2 gap-y-1 text-xs text-slate-600">
                    <div>
                      <div className="text-[10px] uppercase tracking-wide text-slate-400">
                        {t("constructionTasks.field.plannedStart")}
                      </div>
                      <div>{formatDate(row.plannedStart, lang)}</div>
                    </div>
                    <div>
                      <div className="text-[10px] uppercase tracking-wide text-slate-400">
                        {t("constructionTasks.field.plannedEnd")}
                      </div>
                      <div>{formatDate(row.plannedEnd, lang)}</div>
                    </div>
                    <div className="col-span-2">
                      <div className="text-[10px] uppercase tracking-wide text-slate-400">
                        {t("constructionTasks.field.owner")}
                      </div>
                      <div>{row.ownerName ?? "—"}</div>
                    </div>
                  </div>
                  {canManage && (
                    <div className="mt-3 flex items-center justify-end border-t border-slate-100 pt-3">
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-rose-600 hover:text-rose-700"
                        onClick={(e) => {
                          e.stopPropagation();
                          setDeleting(row);
                        }}
                        aria-label={`m-delete-${row.taskCode}`}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </div>
                  )}
                </div>
              ))}
              <div className="flex items-center justify-between px-1 pt-1 text-xs text-slate-500">
                <span>
                  {t("common.showing")} {rows.length} / {total}
                </span>
                <div className="flex gap-1">
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    ←
                  </Button>
                  <span className="px-2 py-1">
                    {page} / {totalPages}
                  </span>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  >
                    →
                  </Button>
                </div>
              </div>
            </div>

            {/* Desktop table (md+) — table has too many columns to fit
                on a phone; hidden below `md` in favour of the cards. */}
            <div className="hidden overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm md:block">
              <div className="max-h-[70vh] overflow-auto">
                <table className="min-w-full divide-y divide-slate-200 text-sm">
                  <thead className="sticky top-0 z-10 bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                    <tr>
                      {canManage && (
                        <th className="w-8 px-3 py-2">
                          <Checkbox
                            aria-label={t("constructionTasks.action.selectAll")}
                            checked={rows.length > 0 && selected.size === rows.length}
                            onCheckedChange={(v) => toggleSelectAll(!!v)}
                            data-testid="construction-select-all"
                          />
                        </th>
                      )}
                      <th className="px-3 py-2">{t("constructionTasks.field.taskCode")}</th>
                      <th className="px-3 py-2">{t("constructionTasks.field.name")}</th>
                      <th className="px-3 py-2">{t("constructionTasks.field.status")}</th>
                      <th className="px-3 py-2">{t("constructionTasks.field.progress")}</th>
                      <th className="px-3 py-2">{t("constructionTasks.field.plannedStart")}</th>
                      <th className="px-3 py-2">{t("constructionTasks.field.plannedEnd")}</th>
                      <th className="px-3 py-2">{t("constructionTasks.field.owner")}</th>
                      <th className="px-3 py-2" />
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {rows.map((row) => (
                      <tr
                        key={row.id}
                        className={cn(
                          "cursor-pointer hover:bg-slate-50",
                          row.isOverdue && "bg-amber-50/40",
                        )}
                        onClick={() => void openDetail(row)}
                        data-testid={`construction-row-${row.id}`}
                      >
                        {canManage && (
                          <td
                            className="px-3 py-2"
                            onClick={(e) => e.stopPropagation()}
                          >
                            <Checkbox
                              checked={selected.has(row.id)}
                              onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                              aria-label={`select-${row.taskCode}`}
                            />
                          </td>
                        )}
                        <td className="px-3 py-2 font-mono text-xs text-slate-600">
                          {row.taskCode}
                          {row.wbs && (
                            <div className="text-[10px] text-slate-400">WBS {row.wbs}</div>
                          )}
                        </td>
                        <td className="px-3 py-2">
                          <span
                            className="font-medium text-slate-900"
                            data-testid={`construction-detail-${row.id}`}
                          >
                            {row.name}
                          </span>
                          {row.predecessors.length > 0 && (
                            <div className="mt-0.5 text-[11px] text-slate-400">
                              ← {row.predecessors.map((p) => p.predecessorTaskCode).join(", ")}
                            </div>
                          )}
                        </td>
                        <td className="px-3 py-2">{renderStatusBadge(row)}</td>
                        <td className="px-3 py-2">
                          <div className="flex items-center gap-2">
                            <Progress value={row.progressPercent} className="h-2 w-20" />
                            <span className="text-xs tabular-nums text-slate-600">
                              {row.progressPercent}%
                            </span>
                          </div>
                        </td>
                        <td className="px-3 py-2 text-xs text-slate-600">
                          {formatDate(row.plannedStart, lang)}
                        </td>
                        <td className="px-3 py-2 text-xs text-slate-600">
                          {formatDate(row.plannedEnd, lang)}
                        </td>
                        <td className="px-3 py-2 text-xs text-slate-600">
                          {row.ownerName ?? "—"}
                        </td>
                        <td
                          className="px-3 py-2 text-right"
                          onClick={(e) => e.stopPropagation()}
                        >
                          {canManage && (
                            <Button
                              size="sm"
                              variant="ghost"
                              className="text-rose-600 hover:text-rose-700"
                              onClick={() => setDeleting(row)}
                              data-testid={`construction-delete-${row.id}`}
                              aria-label={`delete-${row.taskCode}`}
                            >
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="flex items-center justify-between border-t border-slate-100 bg-slate-50 px-3 py-2 text-xs text-slate-500">
                <span>
                  {t("common.showing")} {rows.length} / {total}
                </span>
                <div className="flex gap-1">
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={page <= 1}
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                  >
                    ←
                  </Button>
                  <span className="px-2 py-1">
                    {page} / {totalPages}
                  </span>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={page >= totalPages}
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                  >
                    →
                  </Button>
                </div>
              </div>
            </div>
          </>
        ) : (
          <GanttChart data={ganttData} lang={lang} t={t} onOpen={openDetail} />
        )}

        {/* create dialog */}
        <Dialog open={creating} onOpenChange={setCreating}>
          <DialogContent className="max-h-[90vh] max-w-lg overflow-y-auto">
            <DialogHeader>
              <DialogTitle>{t("constructionTasks.new")}</DialogTitle>
              <DialogDescription>{t("constructionTasks.form.hint")}</DialogDescription>
            </DialogHeader>
            <div className="space-y-3">
              <div>
                <Label>{t("constructionTasks.field.project")}</Label>
                <SearchableSelect
                  value={
                    createForm.designProjectId == null ? "" : String(createForm.designProjectId)
                  }
                  onChange={(v) =>
                    setCreateForm((f) => ({ ...f, designProjectId: v ? Number(v) : null }))
                  }
                  options={projectOptions.filter((o) => o.value !== "")}
                  placeholder={t("constructionTasks.selectProject")}
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>{t("constructionTasks.field.taskCode")}</Label>
                  <Input
                    value={createForm.taskCode}
                    onChange={(e) => setCreateForm((f) => ({ ...f, taskCode: e.target.value }))}
                    placeholder="T-005"
                  />
                </div>
                <div>
                  <Label>{t("constructionTasks.field.wbs")}</Label>
                  <Input
                    value={createForm.wbs}
                    onChange={(e) => setCreateForm((f) => ({ ...f, wbs: e.target.value }))}
                    placeholder="1.2.3"
                  />
                </div>
              </div>
              <div>
                <Label>{t("constructionTasks.field.name")}</Label>
                <Input
                  value={createForm.name}
                  onChange={(e) => setCreateForm((f) => ({ ...f, name: e.target.value }))}
                  data-testid="construction-new-name"
                />
              </div>
              <div>
                <Label>{t("constructionTasks.field.description")}</Label>
                <Textarea
                  rows={2}
                  value={createForm.description}
                  onChange={(e) =>
                    setCreateForm((f) => ({ ...f, description: e.target.value }))
                  }
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>{t("constructionTasks.field.plannedStart")}</Label>
                  <Input
                    type="date"
                    value={createForm.plannedStart}
                    onChange={(e) =>
                      setCreateForm((f) => ({ ...f, plannedStart: e.target.value }))
                    }
                  />
                </div>
                <div>
                  <Label>{t("constructionTasks.field.plannedEnd")}</Label>
                  <Input
                    type="date"
                    value={createForm.plannedEnd}
                    onChange={(e) =>
                      setCreateForm((f) => ({ ...f, plannedEnd: e.target.value }))
                    }
                  />
                </div>
              </div>
              <div>
                <Label>{t("constructionTasks.field.owner")}</Label>
                {canListUsers ? (
                  <SearchableSelect
                    value={
                      createForm.ownerUserId == null ? "" : String(createForm.ownerUserId)
                    }
                    onChange={(v) =>
                      setCreateForm((f) => ({ ...f, ownerUserId: v ? Number(v) : null }))
                    }
                    options={userOptions.filter((o) => o.value !== "")}
                    placeholder="—"
                  />
                ) : (
                  <Input value="—" disabled />
                )}
              </div>
              {createError && <div className="text-sm text-rose-600">{createError}</div>}
            </div>
            <DialogFooter>
              <Button variant="ghost" onClick={() => setCreating(false)} disabled={saving}>
                {t("common.cancel")}
              </Button>
              <Button onClick={submitCreate} disabled={saving} data-testid="construction-new-save">
                {saving ? t("common.saving") : t("common.save")}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* detail sheet */}
        <Sheet
          open={detail !== null}
          onOpenChange={(open) => {
            if (!open) closeDetail();
          }}
        >
          <SheetContent
            side="right"
            className="w-full max-w-xl overflow-y-auto sm:max-w-xl"
          >
            {detail && detailForm && (
              <>
                <SheetHeader>
                  <SheetTitle>
                    {detail.taskCode} — {detail.name}
                  </SheetTitle>
                  <SheetDescription className="text-xs text-slate-500">
                    {detail.designProjectCode} · {detail.designProjectName}
                  </SheetDescription>
                </SheetHeader>
                <div className="mt-4 space-y-4">
                  <div className="flex flex-wrap items-center gap-2">
                    {renderStatusBadge(detail)}
                  </div>
                  <div>
                    <Label>{t("constructionTasks.field.name")}</Label>
                    <Input
                      value={detailForm.name}
                      onChange={(e) =>
                        setDetailForm((f) => f && { ...f, name: e.target.value })
                      }
                      disabled={!canManage}
                    />
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label>{t("constructionTasks.field.wbs")}</Label>
                      <Input
                        value={detailForm.wbs}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, wbs: e.target.value })
                        }
                        disabled={!canManage}
                      />
                    </div>
                    <div>
                      <Label>{t("constructionTasks.field.status")}</Label>
                      <Select
                        value={detailForm.status}
                        onValueChange={(v) =>
                          setDetailForm(
                            (f) => f && { ...f, status: v as ConstructionTaskStatus },
                          )
                        }
                        disabled={!canManage}
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {CONSTRUCTION_STATUSES.map((s) => (
                            <SelectItem key={s} value={s}>
                              {t(`constructionTasks.status.${s}`)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                  <div>
                    <Label>{t("constructionTasks.field.description")}</Label>
                    <Textarea
                      rows={2}
                      value={detailForm.description}
                      onChange={(e) =>
                        setDetailForm((f) => f && { ...f, description: e.target.value })
                      }
                      disabled={!canManage}
                    />
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label>{t("constructionTasks.field.plannedStart")}</Label>
                      <Input
                        type="date"
                        value={detailForm.plannedStart}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, plannedStart: e.target.value })
                        }
                        disabled={!canManage}
                      />
                    </div>
                    <div>
                      <Label>{t("constructionTasks.field.plannedEnd")}</Label>
                      <Input
                        type="date"
                        value={detailForm.plannedEnd}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, plannedEnd: e.target.value })
                        }
                        disabled={!canManage}
                      />
                    </div>
                    <div>
                      <Label>{t("constructionTasks.field.actualStart")}</Label>
                      <Input
                        type="date"
                        value={detailForm.actualStart}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, actualStart: e.target.value })
                        }
                        disabled={!canManage}
                      />
                    </div>
                    <div>
                      <Label>{t("constructionTasks.field.actualEnd")}</Label>
                      <Input
                        type="date"
                        value={detailForm.actualEnd}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, actualEnd: e.target.value })
                        }
                        disabled={!canManage}
                      />
                    </div>
                  </div>
                  <div>
                    <Label>
                      {t("constructionTasks.field.progress")}: {detailForm.progressPercent}%
                    </Label>
                    <Input
                      type="range"
                      min={0}
                      max={100}
                      step={1}
                      value={detailForm.progressPercent}
                      onChange={(e) =>
                        setDetailForm(
                          (f) => f && { ...f, progressPercent: Number(e.target.value) },
                        )
                      }
                      disabled={!canManage}
                      data-testid="construction-progress-slider"
                    />
                  </div>
                  <div>
                    <Label>{t("constructionTasks.field.owner")}</Label>
                    {canListUsers ? (
                      <SearchableSelect
                        value={
                          detailForm.ownerUserId == null
                            ? ""
                            : String(detailForm.ownerUserId)
                        }
                        onChange={(v) =>
                          setDetailForm(
                            (f) => f && { ...f, ownerUserId: v ? Number(v) : null },
                          )
                        }
                        options={userOptions.filter((o) => o.value !== "")}
                        placeholder="—"
                      />
                    ) : (
                      <Input value={detail.ownerName ?? "—"} disabled />
                    )}
                  </div>
                  <div>
                    <Label>{t("constructionTasks.field.predecessors")}</Label>
                    <p className="text-xs text-slate-500">
                      {t("constructionTasks.detail.selectPredecessors")}
                    </p>
                    <div className="mt-2 max-h-40 overflow-auto rounded-md border border-slate-200 p-2">
                      {predecessorOptions.length === 0 ? (
                        <div className="text-xs text-slate-400">
                          {t("constructionTasks.detail.noPredecessors")}
                        </div>
                      ) : (
                        predecessorOptions.map((opt) => {
                          const id = Number(opt.value);
                          const checked = detailPreds.includes(id);
                          return (
                            <label
                              key={opt.value}
                              className="flex cursor-pointer items-center gap-2 py-1 text-sm"
                            >
                              <Checkbox
                                checked={checked}
                                onCheckedChange={(v) => {
                                  if (!canManage) return;
                                  setDetailPreds((prev) =>
                                    v ? [...prev, id] : prev.filter((x) => x !== id),
                                  );
                                }}
                                disabled={!canManage}
                              />
                              {opt.label}
                            </label>
                          );
                        })
                      )}
                    </div>
                  </div>
                  {detailError && <div className="text-sm text-rose-600">{detailError}</div>}
                  <div className="flex items-center justify-between pt-2">
                    {canManage ? (
                      <Button
                        variant="destructive"
                        size="sm"
                        onClick={() => setDeleting(detail)}
                        data-testid="construction-detail-delete"
                      >
                        <Trash2 className="mr-2 h-4 w-4" />
                        {t("constructionTasks.action.delete")}
                      </Button>
                    ) : (
                      <span />
                    )}
                    <div className="flex gap-2">
                      <Button variant="ghost" onClick={closeDetail} disabled={detailSaving}>
                        {t("common.close")}
                      </Button>
                      {canManage && (
                        <Button
                          onClick={submitDetail}
                          disabled={detailSaving}
                          data-testid="construction-detail-save"
                        >
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

        {/* delete confirm */}
        <AlertDialog open={deleting !== null} onOpenChange={(v) => !v && setDeleting(null)}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t("constructionTasks.action.delete")}</AlertDialogTitle>
              <AlertDialogDescription>
                {deleting &&
                  t("constructionTasks.confirmDelete").replace("{code}", deleting.taskCode)}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={deletingBusy}>{t("common.cancel")}</AlertDialogCancel>
              <AlertDialogAction
                onClick={submitDelete}
                disabled={deletingBusy}
                data-testid="construction-delete-confirm"
              >
                {deletingBusy ? t("common.saving") : t("common.delete")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* bulk delete confirm */}
        <AlertDialog open={confirmBulkDelete} onOpenChange={setConfirmBulkDelete}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>
                {t("constructionTasks.action.bulkDelete")}
              </AlertDialogTitle>
              <AlertDialogDescription>
                {t("constructionTasks.confirmBulkDelete").replace(
                  "{count}",
                  String(selected.size),
                )}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={bulkDeleting}>
                {t("common.cancel")}
              </AlertDialogCancel>
              <AlertDialogAction
                onClick={submitBulkDelete}
                disabled={bulkDeleting}
                data-testid="construction-bulk-delete-confirm"
              >
                {bulkDeleting ? t("common.saving") : t("common.delete")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </AdminLayout>
  );
};

// -------------------- small building blocks --------------------

interface StatTileProps {
  icon: React.ReactNode;
  label: string;
  value: number;
  /** Tailwind `from-…` `to-…` classes; drives the card background. */
  gradient: string;
}

/**
 * Bold gradient stat card — matches the marketing dashboard styling
 * (icon in a translucent square on the right, big white number). Kept
 * responsive: the label stays legible when the card halves at
 * `grid-cols-2` on mobile.
 */
const StatTile = ({ icon, label, value, gradient }: StatTileProps) => (
  <div
    className={cn(
      "relative overflow-hidden rounded-xl bg-gradient-to-br p-4 text-white shadow-sm",
      gradient,
    )}
  >
    <div className="relative z-10 flex items-start justify-between gap-2">
      <div className="min-w-0">
        <div className="text-sm font-medium text-white/85">{label}</div>
        <div className="mt-1 text-3xl font-bold tabular-nums leading-none">
          {value}
        </div>
      </div>
      <div className="grid h-11 w-11 shrink-0 place-items-center rounded-lg bg-white/20 backdrop-blur-sm">
        {icon}
      </div>
    </div>
    {/* Decorative background bubble — pure CSS, non-interactive. */}
    <div
      aria-hidden
      className="pointer-events-none absolute -right-6 -top-6 h-24 w-24 rounded-full bg-white/10"
    />
  </div>
);

interface GanttChartProps {
  data: { rows: GanttRow[]; totalDays: number; startIso: string };
  lang: string;
  t: (key: string) => string;
  onOpen: (row: ConstructionTaskResponse) => void;
}

const DAY_WIDTH = 14; // px per day — keeps a 3-month window under ~1300px

const GanttChart = ({ data, lang, t, onOpen }: GanttChartProps) => {
  if (data.rows.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-slate-300 bg-white p-10 text-center text-sm text-slate-500">
        {t("constructionTasks.empty")}
      </div>
    );
  }
  const totalWidth = Math.max(600, data.totalDays * DAY_WIDTH);
  return (
    <div
      className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm"
      data-testid="construction-gantt"
    >
      <div className="flex items-center justify-between border-b border-slate-100 bg-slate-50 px-3 py-2 text-xs text-slate-500">
        <span>
          {formatDate(data.startIso, lang)} — {data.totalDays} {t("common.days")}
        </span>
      </div>
      <div className="max-h-[70vh] overflow-auto">
        <div className="min-w-[720px]">
          <div className="grid" style={{ gridTemplateColumns: "240px 1fr" }}>
            <div className="border-b border-r border-slate-100 bg-slate-50 px-3 py-2 text-xs font-semibold uppercase text-slate-500">
              {t("constructionTasks.field.name")}
            </div>
            <div className="border-b border-slate-100 bg-slate-50 px-3 py-2 text-xs font-semibold uppercase text-slate-500">
              {t("constructionTasks.gantt.timeline")}
            </div>
            {data.rows.map(({ task, offsetDays, spanDays }) => (
              <Fragment key={task.id}>
                <button
                  type="button"
                  className="border-b border-r border-slate-100 px-3 py-2 text-left text-sm hover:bg-slate-50"
                  onClick={() => onOpen(task)}
                >
                  <div className="font-mono text-xs text-slate-500">{task.taskCode}</div>
                  <div className="line-clamp-1 text-slate-900">{task.name}</div>
                </button>
                <div
                  className="relative border-b border-slate-100 px-3 py-3"
                  style={{ minWidth: totalWidth }}
                >
                  <div
                    className={cn(
                      "absolute top-2 h-6 rounded-md border text-[10px] font-semibold text-slate-900",
                      STATUS_BADGE[task.status],
                      task.isOverdue && "ring-2 ring-amber-400",
                    )}
                    style={{
                      left: offsetDays * DAY_WIDTH + 8,
                      width: Math.max(24, spanDays * DAY_WIDTH),
                    }}
                    title={`${task.taskCode} — ${task.progressPercent}%`}
                    data-testid={`construction-gantt-bar-${task.id}`}
                  >
                    <div
                      className="h-full rounded-md bg-slate-900/10"
                      style={{ width: `${task.progressPercent}%` }}
                    />
                    <div className="absolute inset-0 flex items-center px-2">
                      <span className="truncate">
                        {task.taskCode} · {task.progressPercent}%
                      </span>
                    </div>
                  </div>
                </div>
              </Fragment>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};

export default AdminConstructionTasks;
