import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useRequestGuard } from "@/hooks/useRequestGuard";
import { collectLookupPages } from "@/lib/pagination";
import StatTile from "./ConstructionStatTile";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { FieldScope, FieldControl } from "@/components/ui/field-scope";
import { personOption, type PersonIdentity } from "@/lib/people";
import { useCallback, useEffect, useMemo, useRef, useState, Fragment } from "react";
import { Link, useSearchParams } from "react-router-dom";
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
import { type DesignProjectScopeProps } from "./constructionScope";
import ConstructionTaskDrawings from "./ConstructionTaskDrawings";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { ListPagination } from "@/components/admin/ListPagination";
import { PageEmpty, PageLoading, PageError } from "@/components/PageState";
import { ConstructionStatusBadge } from "./constructionUi";
import { OVERDUE_TONE, STATUS_SURFACE_CLASS, TASK_STATUS_TONE } from "./constructionStatus";
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
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import {
  adminApi,
  type ConstructionTaskListParams,
  type ConstructionTaskResponse,
  type ConstructionTaskStatus,
  type CreateConstructionTaskRequest,
  type DesignProjectListItemResponse,
  type ProjectScheduleItemResponse,
  type UpdateConstructionTaskRequest,
} from "@/services/adminApi";
import { formatDate as formatDateInZone } from "@/lib/displaySettings";

const CONSTRUCTION_STATUSES: ConstructionTaskStatus[] = [
  "Planned",
  "InProgress",
  "Completed",
  "Cancelled",
  "OnHold",
  "WaitingForDepartment",
];

interface UserOption extends PersonIdentity {
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
    return formatDateInZone(iso, lang);
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
  projectScheduleNodeId: string;
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
  projectScheduleNodeId: string;
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
  projectScheduleNodeId: "",
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
  projectScheduleNodeId: row.projectScheduleNodeId ?? "",
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
    Math.max(new Date(t.plannedEnd).getTime(), t.actualEnd ? new Date(t.actualEnd).getTime() : 0),
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

export function ConstructionTasksPanel({
  designProjectId,
  designProjects = [],
  scope = "review",
}: DesignProjectScopeProps) {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const [searchParams] = useSearchParams();
  const canManage = scope === "review" && has(ADMIN_PERMS.constructionTasksManage);
  const canListUsers = has(ADMIN_PERMS.users);

  // list state
  const [rows, setRows] = useState<ConstructionTaskResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<ConstructionTaskStatus, number>>>(
    {},
  );
  const [overdueCount, setOverdueCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const taskIdParam = Number(searchParams.get("taskId"));
  const requestedTaskId = Number.isInteger(taskIdParam) && taskIdParam > 0 ? taskIdParam : null;
  const openedTaskIdRef = useRef<number | null>(null);
  const projectId = designProjectId;
  const [status, setStatus] = useState<ConstructionTaskStatus | "">("");
  const [ownerUserId, setOwnerUserId] = useState<number | null>(null);
  const [search, setSearch] = useState("");
  const settledSearch = useDebouncedValue(search);
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
  const [createForm, setCreateForm] = useState<CreateForm>(() => emptyCreateForm(designProjectId));
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
  const [createScheduleItems, setCreateScheduleItems] = useState<ProjectScheduleItemResponse[]>([]);
  const [detailScheduleItems, setDetailScheduleItems] = useState<ProjectScheduleItemResponse[]>([]);
  const [createScheduleFailed, setCreateScheduleFailed] = useState(false);
  const [detailScheduleFailed, setDetailScheduleFailed] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await collectLookupPages((page) =>
          adminApi.listDesignProjects({ pageSize: 200, page }),
        );
        if (cancelled) return;
        setProjects(data.items ?? []);
      } catch {
        // non-fatal
      }
      if (canListUsers) {
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
          /* ignore */
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canListUsers]);

  useEffect(() => {
    let cancelled = false;
    const operationalProjectId = projects.find(
      (item) => item.id === createForm.designProjectId,
    )?.operationalProjectId;
    setCreateScheduleFailed(false);
    if (!operationalProjectId) {
      setCreateScheduleItems([]);
      return () => {
        cancelled = true;
      };
    }
    void adminApi
      .getProjectSchedule(operationalProjectId)
      .then(({ data }) => {
        if (cancelled) return;
        setCreateScheduleFailed(false);
        const revision = data.revisions.find((item) => item.id === data.publishedRevisionId);
        const scheduleItems = revision?.items ?? [];
        setCreateScheduleItems(scheduleItems);
        setCreateForm((form) =>
          form.projectScheduleNodeId &&
          !scheduleItems.some((item) => item.nodeId === form.projectScheduleNodeId)
            ? { ...form, projectScheduleNodeId: "" }
            : form,
        );
      })
      .catch(() => {
        if (cancelled) return;
        setCreateScheduleItems([]);
        setCreateScheduleFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, [createForm.designProjectId, projects]);

  useEffect(() => {
    let cancelled = false;
    const operationalProjectId = projects.find(
      (item) => item.id === detail?.designProjectId,
    )?.operationalProjectId;
    setDetailScheduleFailed(false);
    if (!operationalProjectId) {
      setDetailScheduleItems([]);
      return () => {
        cancelled = true;
      };
    }
    void adminApi
      .getProjectSchedule(operationalProjectId)
      .then(({ data }) => {
        if (cancelled) return;
        setDetailScheduleFailed(false);
        const revision = data.revisions.find((item) => item.id === data.publishedRevisionId);
        const scheduleItems = revision?.items ?? [];
        setDetailScheduleItems(scheduleItems);
        setDetailForm((form) =>
          form &&
          form.projectScheduleNodeId &&
          !scheduleItems.some((item) => item.nodeId === form.projectScheduleNodeId)
            ? { ...form, projectScheduleNodeId: "" }
            : form,
        );
      })
      .catch(() => {
        if (cancelled) return;
        setDetailScheduleItems([]);
        setDetailScheduleFailed(true);
      });
    return () => {
      cancelled = true;
    };
  }, [detail?.designProjectId, projects]);

  const startListRequest = useRequestGuard();
  const fetchList = useCallback(async () => {
    const isCurrent = startListRequest();
    setLoading(true);
    setError(null);
    try {
      const params: ConstructionTaskListParams = { page, pageSize };
      if (projectId != null) params.designProjectId = projectId;
      if (status) params.status = status;
      if (ownerUserId != null) params.ownerUserId = ownerUserId;
      if (settledSearch.trim()) params.search = settledSearch.trim();
      if (overdueOnly) params.overdueOnly = true;
      const { data } = await adminApi.listConstructionTasks(params);
      if (!isCurrent()) return;
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
    projectId,
    status,
    ownerUserId,
    settledSearch,
    overdueOnly,
    uiError,
    toast,
    t,
  ]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const projectOptions = useMemo(
    () =>
      projects.map((p) => ({
        value: String(p.id),
        label: `${p.projectCode} — ${p.name}`,
      })),
    [projects],
  );
  const projectLabel = useMemo(() => {
    if (designProjectId == null) return t("common.noData");
    const match = designProjects.find((p) => p.id === designProjectId);
    return match ? `${match.projectCode} — ${match.name}` : t("common.noData");
  }, [designProjectId, designProjects, t]);
  const userOptions = useMemo(
    () => [
      { value: "", label: t("constructionTasks.filter.allOwners") },
      ...users.map((u) => personOption(u, t)),
    ],
    [users, t],
  );

  const resetFilters = () => {
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
    if (createScheduleItems.length > 0 && !createForm.projectScheduleNodeId) {
      setCreateError(t("constructionTasks.form.masterWbsRequired"));
      return;
    }
    const createMilestone = createScheduleItems.find(
      (item) => item.nodeId === createForm.projectScheduleNodeId,
    );
    if (
      createMilestone &&
      (createForm.plannedStart < createMilestone.plannedStart ||
        createForm.plannedEnd > createMilestone.plannedEnd)
    ) {
      setCreateError(t("constructionTasks.form.masterWbsDates", { wbs: createMilestone.wbsCode }));
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
        projectScheduleNodeId: createForm.projectScheduleNodeId || undefined,
      };
      await adminApi.createConstructionTask(body);
      toast({ title: t("constructionTasks.created") });
      setCreating(false);
      await fetchList();
    } catch (err) {
      setCreateError(uiError(err));
    } finally {
      setSaving(false);
    }
  };

  // -------- detail / edit --------
  const openDetail = useCallback(async (row: ConstructionTaskResponse) => {
    setDetail(row);
    setDetailForm(editFormFrom(row));
    setDetailPreds(row.predecessors.map((p) => p.predecessorTaskId));
    setDetailError(null);
    // Pull the whole project’s task set for the predecessor picker
    // (independent of the current filter/pagination). Fire-and-forget
    // — the picker falls back to the visible rows while it loads.
    try {
      const { data } = await collectLookupPages((page) =>
        adminApi.listConstructionTasks({
          designProjectId: row.designProjectId,
          pageSize: 200,
          page,
        }),
      );
      setDetailSiblings(data.items ?? []);
    } catch {
      setDetailSiblings([]);
    }
  }, []);

  useEffect(() => {
    if (requestedTaskId == null || openedTaskIdRef.current === requestedTaskId) return;
    openedTaskIdRef.current = requestedTaskId;
    void adminApi
      .getConstructionTask(requestedTaskId)
      .then(({ data }) => openDetail(data))
      .catch((err) =>
        toast({
          title: t("common.error"),
          description: uiError(err),
          variant: "destructive",
        }),
      );
  }, [openDetail, requestedTaskId, t, toast, uiError]);
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
    if (detailScheduleItems.length > 0 && !detailForm.projectScheduleNodeId) {
      setDetailError(t("constructionTasks.form.masterWbsRequired"));
      return;
    }
    const detailMilestone = detailScheduleItems.find(
      (item) => item.nodeId === detailForm.projectScheduleNodeId,
    );
    if (
      detailMilestone &&
      (detailForm.plannedStart < detailMilestone.plannedStart ||
        detailForm.plannedEnd > detailMilestone.plannedEnd)
    ) {
      setDetailError(t("constructionTasks.form.masterWbsDates", { wbs: detailMilestone.wbsCode }));
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
        projectScheduleNodeId: detailForm.projectScheduleNodeId || undefined,
        status: detailForm.status,
      };
      const { data: updated } = await adminApi.updateConstructionTask(detail.id, body);
      // Only push predecessors if the operator actually changed the set,
      // so we don't add a redundant audit entry on plain edits.
      const originalPreds = new Set(detail.predecessors.map((p) => p.predecessorTaskId));
      const nextPreds = new Set(detailPreds);
      const changed =
        originalPreds.size !== nextPreds.size ||
        [...nextPreds].some((id) => !originalPreds.has(id));
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
      setDetailError(uiError(err));
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
        description: uiError(err),
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
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      setBulkDeleting(false);
    }
  };

  // -------- render helpers --------
  const hasActiveFilter =
    status !== "" || ownerUserId != null || search.trim().length > 0 || overdueOnly;

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
      <ConstructionStatusBadge tone={TASK_STATUS_TONE[row.status]}>
        {t(`constructionTasks.status.${row.status}`)}
      </ConstructionStatusBadge>
      {row.isOverdue && (
        <ConstructionStatusBadge tone={OVERDUE_TONE}>
          {t("constructionTasks.status.Overdue")}
        </ConstructionStatusBadge>
      )}
    </div>
  );

  if (designProjectId == null) {
    return (
      <div data-testid="construction-tasks-no-project">
        <PageEmpty message={t("construction.common.noDesignProject")} />
      </div>
    );
  }

  return (
    <>
      <div className="space-y-6" data-testid="construction-tasks-page">
        <Button variant="outline" asChild>
          <Link to="/admin/construction/cm/schedule">{t("cmSchedule.title")}</Link>
        </Button>
        <div className="flex justify-end">
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
            <Button type="button" variant="outline" size="sm" onClick={() => void fetchList()}>
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button type="button" size="sm" onClick={openCreate} data-testid="construction-new">
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
          />
          <StatTile
            icon={<CircleDot className="h-6 w-6" />}
            label={t("constructionTasks.stat.inProgress")}
            value={statusPillCount("InProgress")}
          />
          <StatTile
            icon={<CheckCircle2 className="h-6 w-6" />}
            label={t("constructionTasks.stat.completed")}
            value={statusPillCount("Completed")}
          />
          <StatTile
            icon={<AlertTriangle className="h-6 w-6" />}
            label={t("constructionTasks.stat.overdue")}
            value={overdueCount}
          />
        </div>

        {/* filters */}
        <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
          {/* `min-w-0` on every grid item so long option labels (project
              codes + names) truncate via SearchableSelect's own `truncate`
              instead of blowing past the viewport on narrow screens. */}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 [&>div]:min-w-0">
            <FieldScope>
              <Label className="text-xs text-muted-foreground">
                {t("constructionTasks.field.project")}
              </Label>
              <Input value={projectLabel} readOnly />
            </FieldScope>
            <FieldScope>
              <Label className="text-xs text-muted-foreground">
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
            </FieldScope>
            <FieldScope>
              <Label className="text-xs text-muted-foreground">
                {t("constructionTasks.field.owner")}
              </Label>
              {canListUsers ? (
                <SearchableSelect
                  ariaLabel={t("constructionTasks.field.owner")}
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
            </FieldScope>
            <FieldScope>
              <Label className="text-xs text-muted-foreground">
                {t("constructionTasks.filter.search")}
              </Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
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
            </FieldScope>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-3">
            <label className="flex cursor-pointer items-center gap-2 text-sm text-muted-foreground">
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
          <PageEmpty
            message={t("constructionTasks.empty")}
            action={
              canManage && (
                <Button
                  type="button"
                  size="sm"
                  onClick={openCreate}
                  data-testid="construction-empty-new"
                >
                  <Plus className="mr-2 h-4 w-4" />
                  {t("constructionTasks.new")}
                </Button>
              )
            }
          />
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
                    "relative rounded-xl border border-border bg-card p-4 text-left shadow-sm transition hover:border-input hover:shadow-md focus:outline-none focus:ring-2 focus:ring-ring",
                    row.isOverdue && "border-warning/50 bg-warning-soft/50",
                  )}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <span className="font-mono text-xs text-muted-foreground">
                          {row.taskCode}
                        </span>
                        {row.wbs && (
                          <span className="rounded bg-muted px-1.5 py-0.5 text-[10px] text-muted-foreground">
                            WBS {row.wbs}
                          </span>
                        )}
                      </div>
                      <div className="mt-1 line-clamp-2 text-base font-semibold text-foreground">
                        {row.name}
                      </div>
                    </div>
                    {canManage && (
                      <div onClick={(e) => e.stopPropagation()} role="presentation">
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
                    <div className="mt-2 text-[11px] text-muted-foreground">
                      ← {row.predecessors.map((p) => p.predecessorTaskCode).join(", ")}
                    </div>
                  )}
                  <div className="mt-3 flex items-center gap-2">
                    <Progress value={row.progressPercent} className="h-2 flex-1" />
                    <span className="w-10 text-right text-xs font-medium tabular-nums text-foreground">
                      {row.progressPercent}%
                    </span>
                  </div>
                  <div className="mt-3 grid grid-cols-2 gap-x-2 gap-y-1 text-xs text-muted-foreground">
                    <div>
                      <div className="text-[10px] uppercase tracking-wide text-muted-foreground">
                        {t("constructionTasks.field.plannedStart")}
                      </div>
                      <div>{formatDate(row.plannedStart, lang)}</div>
                    </div>
                    <div>
                      <div className="text-[10px] uppercase tracking-wide text-muted-foreground">
                        {t("constructionTasks.field.plannedEnd")}
                      </div>
                      <div>{formatDate(row.plannedEnd, lang)}</div>
                    </div>
                    <div className="col-span-2">
                      <div className="text-[10px] uppercase tracking-wide text-muted-foreground">
                        {t("constructionTasks.field.owner")}
                      </div>
                      <div>{row.ownerName ?? "—"}</div>
                    </div>
                  </div>
                  {canManage && (
                    <div className="mt-3 flex items-center justify-end border-t border-border pt-3">
                      <Button
                        size="sm"
                        variant="ghost"
                        className="text-destructive hover:text-destructive/80"
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
            </div>

            {/* Desktop table (md+) — table has too many columns to fit
                on a phone; hidden below `md` in favour of the cards. */}
            <div className="hidden overflow-hidden rounded-lg border border-border bg-card shadow-sm md:block">
              <div className="max-h-[70vh] overflow-auto">
                <table className="min-w-full divide-y divide-border text-sm">
                  <thead className="sticky top-0 z-10 bg-muted/30 text-left text-xs uppercase tracking-wide text-muted-foreground">
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
                  <tbody className="divide-y divide-border">
                    {rows.map((row) => (
                      <tr
                        key={row.id}
                        className={cn(
                          "cursor-pointer hover:bg-muted/30",
                          row.isOverdue && "bg-warning-soft/50",
                        )}
                        onClick={() => void openDetail(row)}
                        data-testid={`construction-row-${row.id}`}
                      >
                        {canManage && (
                          <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                            <Checkbox
                              checked={selected.has(row.id)}
                              onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                              aria-label={`select-${row.taskCode}`}
                            />
                          </td>
                        )}
                        <td className="px-3 py-2 font-mono text-xs text-muted-foreground">
                          {row.taskCode}
                          {row.wbs && (
                            <div className="text-[10px] text-muted-foreground">WBS {row.wbs}</div>
                          )}
                        </td>
                        <td className="px-3 py-2">
                          <span
                            className="font-medium text-foreground"
                            data-testid={`construction-detail-${row.id}`}
                          >
                            {row.name}
                          </span>
                          {row.predecessors.length > 0 && (
                            <div className="mt-0.5 text-[11px] text-muted-foreground">
                              ← {row.predecessors.map((p) => p.predecessorTaskCode).join(", ")}
                            </div>
                          )}
                        </td>
                        <td className="px-3 py-2">{renderStatusBadge(row)}</td>
                        <td className="px-3 py-2">
                          <div className="flex items-center gap-2">
                            <Progress value={row.progressPercent} className="h-2 w-20" />
                            <span className="text-xs tabular-nums text-muted-foreground">
                              {row.progressPercent}%
                            </span>
                          </div>
                        </td>
                        <td className="px-3 py-2 text-xs text-muted-foreground">
                          {formatDate(row.plannedStart, lang)}
                        </td>
                        <td className="px-3 py-2 text-xs text-muted-foreground">
                          {formatDate(row.plannedEnd, lang)}
                        </td>
                        <td className="px-3 py-2 text-xs text-muted-foreground">
                          {row.ownerName ?? "—"}
                        </td>
                        <td className="px-3 py-2 text-right" onClick={(e) => e.stopPropagation()}>
                          {canManage && (
                            <Button
                              size="sm"
                              variant="ghost"
                              className="text-destructive hover:text-destructive/80"
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
            </div>
            <ListPagination
              page={page}
              pageSize={pageSize}
              total={total}
              onPageChange={setPage}
              disabled={loading}
            />
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
              <FieldScope>
                <Label>{t("constructionTasks.field.project")}</Label>
                <SearchableSelect
                  value={
                    createForm.designProjectId == null ? "" : String(createForm.designProjectId)
                  }
                  onChange={(v) =>
                    setCreateForm((f) => ({ ...f, designProjectId: v ? Number(v) : null }))
                  }
                  options={projectOptions.filter((o) => o.value === String(designProjectId))}
                  placeholder={t("constructionTasks.selectProject")}
                />
              </FieldScope>
              <div className="grid grid-cols-2 gap-3">
                <FieldScope>
                  <Label>{t("constructionTasks.field.taskCode")}</Label>
                  <Input
                    value={createForm.taskCode}
                    onChange={(e) => setCreateForm((f) => ({ ...f, taskCode: e.target.value }))}
                    placeholder="T-005"
                  />
                </FieldScope>
                <FieldScope>
                  <Label>{t("constructionTasks.field.wbs")}</Label>
                  <Input
                    value={createForm.wbs}
                    onChange={(e) => setCreateForm((f) => ({ ...f, wbs: e.target.value }))}
                    placeholder="1.2.3"
                  />
                </FieldScope>
              </div>
              <FieldScope>
                <Label>{t("constructionTasks.field.name")}</Label>
                <Input
                  value={createForm.name}
                  onChange={(e) => setCreateForm((f) => ({ ...f, name: e.target.value }))}
                  data-testid="construction-new-name"
                />
              </FieldScope>
              <FieldScope>
                <Label>{t("constructionTasks.field.description")}</Label>
                <Textarea
                  rows={2}
                  value={createForm.description}
                  onChange={(e) => setCreateForm((f) => ({ ...f, description: e.target.value }))}
                />
              </FieldScope>
              <div className="grid grid-cols-2 gap-3">
                <FieldScope>
                  <Label>{t("constructionTasks.field.plannedStart")}</Label>
                  <Input
                    type="date"
                    value={createForm.plannedStart}
                    onChange={(e) => setCreateForm((f) => ({ ...f, plannedStart: e.target.value }))}
                  />
                </FieldScope>
                <FieldScope>
                  <Label>{t("constructionTasks.field.plannedEnd")}</Label>
                  <Input
                    type="date"
                    value={createForm.plannedEnd}
                    onChange={(e) => setCreateForm((f) => ({ ...f, plannedEnd: e.target.value }))}
                  />
                </FieldScope>
              </div>
              {createScheduleItems.length > 0 && (
                <FieldScope>
                  <Label>{t("constructionTasks.field.masterWbs")}</Label>
                  <Select
                    value={createForm.projectScheduleNodeId}
                    onValueChange={(value) =>
                      setCreateForm((form) => ({
                        ...form,
                        projectScheduleNodeId: value,
                      }))
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={t("constructionTasks.field.chooseMasterWbs")} />
                    </SelectTrigger>
                    <SelectContent>
                      {createScheduleItems.map((item) => (
                        <SelectItem key={item.nodeId} value={item.nodeId}>
                          {item.wbsCode} · {item.title} ({item.plannedStart}–{item.plannedEnd})
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FieldScope>
              )}
              {createScheduleFailed && (
                <p className="text-sm text-destructive">
                  {t("constructionTasks.form.masterWbsLoadFailed")}
                </p>
              )}
              <FieldScope>
                <Label>{t("constructionTasks.field.owner")}</Label>
                {canListUsers ? (
                  <SearchableSelect
                    ariaLabel={t("constructionTasks.field.owner")}
                    value={createForm.ownerUserId == null ? "" : String(createForm.ownerUserId)}
                    onChange={(v) =>
                      setCreateForm((f) => ({ ...f, ownerUserId: v ? Number(v) : null }))
                    }
                    options={userOptions.filter((o) => o.value !== "")}
                    placeholder="—"
                  />
                ) : (
                  <Input value="—" disabled />
                )}
              </FieldScope>
              {createError && <div className="text-sm text-destructive">{createError}</div>}
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
          <SheetContent side="right" className="w-full max-w-xl overflow-y-auto sm:max-w-xl">
            {detail && detailForm && (
              <>
                <SheetHeader>
                  <SheetTitle>
                    {detail.taskCode} — {detail.name}
                  </SheetTitle>
                  <SheetDescription className="text-xs text-muted-foreground">
                    {detail.designProjectCode} · {detail.designProjectName}
                  </SheetDescription>
                </SheetHeader>
                <div className="mt-4 space-y-4">
                  <div className="flex flex-wrap items-center gap-2">
                    {renderStatusBadge(detail)}
                  </div>
                  <FieldScope>
                    <Label>{t("constructionTasks.field.name")}</Label>
                    <Input
                      value={detailForm.name}
                      onChange={(e) => setDetailForm((f) => f && { ...f, name: e.target.value })}
                      disabled={!canManage}
                    />
                  </FieldScope>
                  <div className="grid grid-cols-2 gap-3">
                    <FieldScope>
                      <Label>{t("constructionTasks.field.wbs")}</Label>
                      <Input
                        value={detailForm.wbs}
                        onChange={(e) => setDetailForm((f) => f && { ...f, wbs: e.target.value })}
                        disabled={!canManage}
                      />
                    </FieldScope>
                    <FieldScope>
                      <Label>{t("constructionTasks.field.status")}</Label>
                      <Select
                        value={detailForm.status}
                        onValueChange={(v) =>
                          setDetailForm((f) => f && { ...f, status: v as ConstructionTaskStatus })
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
                    </FieldScope>
                  </div>
                  {detailScheduleItems.length > 0 && (
                    <FieldScope>
                      <Label>{t("constructionTasks.field.masterWbs")}</Label>
                      <Select
                        value={detailForm.projectScheduleNodeId}
                        onValueChange={(value) =>
                          setDetailForm(
                            (form) =>
                              form && {
                                ...form,
                                projectScheduleNodeId: value,
                              },
                          )
                        }
                        disabled={!canManage}
                      >
                        <SelectTrigger>
                          <SelectValue placeholder={t("constructionTasks.field.chooseMasterWbs")} />
                        </SelectTrigger>
                        <SelectContent>
                          {detailScheduleItems.map((item) => (
                            <SelectItem key={item.nodeId} value={item.nodeId}>
                              {item.wbsCode} · {item.title} ({item.plannedStart}–{item.plannedEnd})
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </FieldScope>
                  )}
                  {detailScheduleFailed && (
                    <p className="text-sm text-destructive">
                      {t("constructionTasks.form.masterWbsLoadFailed")}
                    </p>
                  )}
                  <FieldScope>
                    <Label>{t("constructionTasks.field.description")}</Label>
                    <Textarea
                      rows={2}
                      value={detailForm.description}
                      onChange={(e) =>
                        setDetailForm((f) => f && { ...f, description: e.target.value })
                      }
                      disabled={!canManage}
                    />
                  </FieldScope>
                  <div className="grid grid-cols-2 gap-3">
                    <FieldScope>
                      <Label>{t("constructionTasks.field.plannedStart")}</Label>
                      <Input
                        type="date"
                        value={detailForm.plannedStart}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, plannedStart: e.target.value })
                        }
                        disabled={!canManage}
                      />
                    </FieldScope>
                    <FieldScope>
                      <Label>{t("constructionTasks.field.plannedEnd")}</Label>
                      <Input
                        type="date"
                        value={detailForm.plannedEnd}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, plannedEnd: e.target.value })
                        }
                        disabled={!canManage}
                      />
                    </FieldScope>
                    <FieldScope>
                      <Label>{t("constructionTasks.field.actualStart")}</Label>
                      <Input
                        type="date"
                        value={detailForm.actualStart}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, actualStart: e.target.value })
                        }
                        disabled={!canManage}
                      />
                    </FieldScope>
                    <FieldScope>
                      <Label>{t("constructionTasks.field.actualEnd")}</Label>
                      <Input
                        type="date"
                        value={detailForm.actualEnd}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, actualEnd: e.target.value })
                        }
                        disabled={!canManage}
                      />
                    </FieldScope>
                  </div>
                  <FieldScope>
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
                        setDetailForm((f) => f && { ...f, progressPercent: Number(e.target.value) })
                      }
                      disabled={!canManage}
                      data-testid="construction-progress-slider"
                    />
                  </FieldScope>
                  <FieldScope>
                    <Label>{t("constructionTasks.field.owner")}</Label>
                    {canListUsers ? (
                      <SearchableSelect
                        ariaLabel={t("constructionTasks.field.owner")}
                        value={detailForm.ownerUserId == null ? "" : String(detailForm.ownerUserId)}
                        onChange={(v) =>
                          setDetailForm((f) => f && { ...f, ownerUserId: v ? Number(v) : null })
                        }
                        options={userOptions.filter((o) => o.value !== "")}
                        placeholder="—"
                      />
                    ) : (
                      <Input value={detail.ownerName ?? "—"} disabled />
                    )}
                  </FieldScope>
                  <FieldScope>
                    <Label>{t("constructionTasks.field.predecessors")}</Label>
                    <p className="text-xs text-muted-foreground">
                      {t("constructionTasks.detail.selectPredecessors")}
                    </p>
                    <div className="mt-2 max-h-40 overflow-auto rounded-md border border-border p-2">
                      {predecessorOptions.length === 0 ? (
                        <div className="text-xs text-muted-foreground">
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
                              <FieldControl>
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
                              </FieldControl>
                              {opt.label}
                            </label>
                          );
                        })
                      )}
                    </div>
                  </FieldScope>
                  <ConstructionTaskDrawings
                    key={detail.id}
                    taskId={detail.id}
                    canManage={canManage}
                  />
                  {detailError && <div className="text-sm text-destructive">{detailError}</div>}
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
              <AlertDialogTitle>{t("constructionTasks.action.bulkDelete")}</AlertDialogTitle>
              <AlertDialogDescription>
                {t("constructionTasks.confirmBulkDelete").replace("{count}", String(selected.size))}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={bulkDeleting}>{t("common.cancel")}</AlertDialogCancel>
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
    </>
  );
}

interface GanttChartProps {
  data: { rows: GanttRow[]; totalDays: number; startIso: string };
  lang: string;
  t: (key: string) => string;
  onOpen: (row: ConstructionTaskResponse) => void;
}

const DAY_WIDTH = 14; // px per day — keeps a 3-month window under ~1300px

const GanttChart = ({ data, lang, t, onOpen }: GanttChartProps) => {
  if (data.rows.length === 0) {
    return <PageEmpty message={t("constructionTasks.empty")} />;
  }
  const totalWidth = Math.max(600, data.totalDays * DAY_WIDTH);
  return (
    <div
      className="overflow-hidden rounded-lg border border-border bg-card shadow-sm"
      data-testid="construction-gantt"
    >
      <div className="flex items-center justify-between border-b border-border bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
        <span>
          {formatDate(data.startIso, lang)} — {data.totalDays} {t("common.days")}
        </span>
      </div>
      <div className="max-h-[70vh] overflow-auto">
        <div className="min-w-[720px]">
          <div className="grid" style={{ gridTemplateColumns: "240px 1fr" }}>
            <div className="border-b border-r border-border bg-muted/30 px-3 py-2 text-xs font-semibold uppercase text-muted-foreground">
              {t("constructionTasks.field.name")}
            </div>
            <div className="border-b border-border bg-muted/30 px-3 py-2 text-xs font-semibold uppercase text-muted-foreground">
              {t("constructionTasks.gantt.timeline")}
            </div>
            {data.rows.map(({ task, offsetDays, spanDays }) => (
              <Fragment key={task.id}>
                <button
                  type="button"
                  className="border-b border-r border-border px-3 py-2 text-left text-sm hover:bg-muted/30"
                  onClick={() => onOpen(task)}
                >
                  <div className="font-mono text-xs text-muted-foreground">{task.taskCode}</div>
                  <div className="line-clamp-1 text-foreground">{task.name}</div>
                </button>
                <div
                  className="relative border-b border-border px-3 py-3"
                  style={{ minWidth: totalWidth }}
                >
                  <div
                    className={cn(
                      "absolute top-2 h-6 rounded-md border text-[10px] font-semibold text-foreground",
                      STATUS_SURFACE_CLASS[TASK_STATUS_TONE[task.status]],
                      task.isOverdue && "ring-2 ring-warning/60",
                    )}
                    style={{
                      left: offsetDays * DAY_WIDTH + 8,
                      width: Math.max(24, spanDays * DAY_WIDTH),
                    }}
                    title={`${task.taskCode} — ${task.progressPercent}%`}
                    data-testid={`construction-gantt-bar-${task.id}`}
                  >
                    <div
                      className="h-full rounded-md bg-foreground/10"
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
