import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  ClipboardCheck,
  ClipboardList,
  Eye,
  FileCheck,
  FileText,
  Pencil,
  Plus,
  RefreshCcw,
  Search,
  Trash2,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import AdminFilePreview from "@/components/admin/AdminFilePreview";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { resolveSafeLinkUrl } from "@/lib/url";
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
  type AcceptanceRecordListParams,
  type AcceptanceRecordResponse,
  type AcceptanceStatus,
  type CreateAcceptanceRecordRequest,
  type ConstructionTaskResponse,
  type DesignProjectListItemResponse,
  type UpdateAcceptanceRecordRequest,
} from "@/services/adminApi";

const ACCEPTANCE_STATUSES: AcceptanceStatus[] = ["Draft", "Submitted", "Approved", "Rejected", "Cancelled"];

const STATUS_BADGE: Record<AcceptanceStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  Cancelled: "border-zinc-200 bg-zinc-50 text-zinc-600",
};

const OVERDUE_BADGE = "border-amber-300 bg-amber-50 text-amber-800";

const OPEN_STATUSES = new Set<AcceptanceStatus>(["Draft", "Submitted", "Rejected"]);

const parseDocuments = (value: string | null | undefined): string[] => {
  if (!value) return [];
  try {
    const parsed: unknown = JSON.parse(value);
    return Array.isArray(parsed) ? parsed.filter((item): item is string => typeof item === "string") : [];
  } catch {
    return [];
  }
};

const documentPaths = (value: string) =>
  value.split("\n").map((path) => path.trim()).filter(Boolean);

const serializeDocuments = (paths: string[]) => JSON.stringify(paths);

interface StatTile {
  key: string;
  label: string;
  value: number;
  gradient: string;
  icon: typeof ClipboardCheck;
}

const formatDate = (iso: string | null | undefined) => {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString();
};

const formatDateTime = (iso: string | null | undefined) => {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString();
};

export default function AcceptanceRecordsPage() {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionAcceptanceManage);
  const canApprove = has(ADMIN_PERMS.constructionAcceptanceApprove);

  const [projects, setProjects] = useState<DesignProjectListItemResponse[]>([]);
  const [tasks, setTasks] = useState<ConstructionTaskResponse[]>([]);
  const [projectId, setProjectId] = useState<number | undefined>();
  const [taskId, setTaskId] = useState<number | undefined>();
  const [responsibleUserId, setResponsibleUserId] = useState<number | undefined>();
  const [acceptanceFrom, setAcceptanceFrom] = useState("");
  const [acceptanceTo, setAcceptanceTo] = useState("");
  const [status, setStatus] = useState<AcceptanceStatus | "">("");
  const [openOnly, setOpenOnly] = useState(false);
  const [overdueOnly, setOverdueOnly] = useState(false);
  const [search, setSearch] = useState("");
  const [sortBy, setSortBy] = useState<NonNullable<AcceptanceRecordListParams["sortBy"]>>("date");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("desc");
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [rows, setRows] = useState<AcceptanceRecordResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<AcceptanceStatus, number>>>({});
  const [overdueCount, setOverdueCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<Set<number>>(new Set());

  const [detail, setDetail] = useState<AcceptanceRecordResponse | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);
  const [detailId, setDetailId] = useState<number | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [formOpen, setFormOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const [pendingTransition, setPendingTransition] = useState<{
    id: number;
    title: string;
    next: AcceptanceStatus;
  } | null>(null);
  const [pendingDelete, setPendingDelete] = useState<AcceptanceRecordResponse | null>(null);
  const [pendingBulk, setPendingBulk] = useState(false);
  const [transitionNote, setTransitionNote] = useState("");

  const [form, setForm] = useState<{
    title: string;
    description: string;
    constructionTaskId: string;
    acceptanceDate: string;
    location: string;
    participants: string;
    findings: string;
    documents: string;
  }>({
    title: "",
    description: "",
    constructionTaskId: "",
    acceptanceDate: new Date().toISOString().slice(0, 10),
    location: "",
    participants: "",
    findings: "",
    documents: "",
  });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await adminApi.listDesignProjects({ pageSize: 200 });
        if (!cancelled) setProjects(res.data.items ?? []);
      } catch {
        // Project dropdown is optional — the list still loads without it.
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await adminApi.listConstructionTasks({
          designProjectId: projectId,
          pageSize: 200,
        });
        if (!cancelled) setTasks(res.data.items ?? []);
      } catch {
        if (!cancelled) setTasks([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [projectId]);

  const responsibleOptions = useMemo(() => {
    const options = new Map<number, string>();
    projects.forEach((project) => {
      if (project.projectManagerUserId && project.projectManagerName)
        options.set(project.projectManagerUserId, project.projectManagerName);
      if (project.designLeadUserId && project.designLeadName)
        options.set(project.designLeadUserId, project.designLeadName);
    });
    tasks.forEach((task) => {
      if (task.ownerUserId && task.ownerName) options.set(task.ownerUserId, task.ownerName);
    });
    return Array.from(options, ([value, label]) => ({ value: String(value), label }));
  }, [projects, tasks]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: AcceptanceRecordListParams = {
        designProjectId: projectId,
        constructionTaskId: taskId,
        responsibleUserId,
        acceptanceFrom: acceptanceFrom || undefined,
        acceptanceTo: acceptanceTo || undefined,
        status: status || undefined,
        search: search.trim() || undefined,
        openOnly: openOnly || undefined,
        overdueOnly: overdueOnly || undefined,
        sortBy,
        sortDirection,
        page,
        pageSize,
      };
      const res = await adminApi.listAcceptanceRecords(params);
      setRows(res.data.items ?? []);
      setTotal(res.data.total ?? 0);
      setStatusCounts(res.data.statusCounts ?? {});
      setOverdueCount(res.data.overdueCount ?? 0);
      setSelected(new Set());
    } catch (e) {
      setError(extractApiError(e) || t("acceptance.error"));
    } finally {
      setLoading(false);
    }
  }, [projectId, taskId, responsibleUserId, acceptanceFrom, acceptanceTo, status, search, openOnly, overdueOnly, sortBy, sortDirection, page, pageSize, t]);

  useEffect(() => {
    load();
  }, [load]);

  // Keep the detail sheet in sync when the row it points at is refreshed.
  useEffect(() => {
    if (!detail) return;
    const refreshed = rows.find((r) => r.id === detail.id);
    if (refreshed && refreshed !== detail) setDetail(refreshed);
  }, [rows, detail]);

  const openDetail = useCallback(async (id: number) => {
    setDetailOpen(true);
    setDetailId(id);
    setDetail(null);
    setDetailError(null);
    setDetailLoading(true);
    try {
      const response = await adminApi.getAcceptanceRecord(id);
      setDetail(response.data);
    } catch (error) {
      setDetailError(extractApiError(error) || t("acceptance.detail.error"));
    } finally {
      setDetailLoading(false);
    }
  }, [t]);

  const openCreate = () => {
    setEditingId(null);
    setForm({
      title: "",
      description: "",
      constructionTaskId: "",
      acceptanceDate: new Date().toISOString().slice(0, 10),
      location: "",
      participants: "",
      findings: "",
      documents: "",
    });
    setFormError(null);
    setFormOpen(true);
  };

  const openEdit = (r: AcceptanceRecordResponse) => {
    setEditingId(r.id);
    setForm({
      title: r.title,
      description: r.description ?? "",
      constructionTaskId: r.constructionTaskId != null ? String(r.constructionTaskId) : "",
      acceptanceDate: r.acceptanceDate,
      location: r.location ?? "",
      participants: r.participants ?? "",
      findings: r.findings ?? "",
      documents: parseDocuments(r.documents).join("\n"),
    });
    setFormError(null);
    setFormOpen(true);
  };

  const handleSave = async () => {
    setFormError(null);
    const title = form.title.trim();
    if (!title) {
      setFormError(t("acceptance.form.required.title"));
      return;
    }
    if (!editingId && projectId == null) {
      setFormError(t("acceptance.form.required.project"));
      return;
    }
    if (!form.acceptanceDate) {
      setFormError(t("acceptance.form.required.date"));
      return;
    }
    const documents = documentPaths(form.documents);
    const serializedDocuments = serializeDocuments(documents);
    if (
      documents.length > 20
      || documents.some((path) => path.length > 500 || !resolveSafeLinkUrl(path))
      || serializedDocuments.length > 4000
    ) {
      setFormError(t("acceptance.form.documentsInvalid"));
      return;
    }

    setSaving(true);
    try {
      const constructionTaskId = form.constructionTaskId
        ? Number(form.constructionTaskId)
        : null;
      const payload = {
        title,
        description: form.description.trim() || null,
        constructionTaskId,
        acceptanceDate: form.acceptanceDate,
        location: form.location.trim() || null,
        participants: form.participants.trim() || null,
        findings: form.findings.trim() || null,
        documents: serializedDocuments,
      };
      if (editingId) {
        await adminApi.updateAcceptanceRecord(editingId, payload as UpdateAcceptanceRecordRequest);
      } else {
        await adminApi.createAcceptanceRecord({
          ...payload,
          designProjectId: projectId!,
        } as CreateAcceptanceRecordRequest);
      }
      toast({ title: t("acceptance.form.saved") });
      setFormOpen(false);
      await load();
    } catch (e) {
      setFormError(extractApiError(e) || t("acceptance.error"));
    } finally {
      setSaving(false);
    }
  };

  const runTransition = async (id: number, next: AcceptanceStatus, note: string) => {
    try {
      if (next === "Approved") {
        await adminApi.approveAcceptanceRecord(id, { status: "Approved", resolutionNote: note || null });
      } else {
        await adminApi.transitionAcceptanceStatus(id, { status: next, resolutionNote: note || null });
      }
      toast({ title: t("acceptance.toast.transition.success") });
      await load();
    } catch (e) {
      toast({
        variant: "destructive",
        title: extractApiError(e) || t("acceptance.error"),
      });
    }
  };

  const confirmTransition = (r: AcceptanceRecordResponse, next: AcceptanceStatus) => {
    setTransitionNote("");
    setPendingTransition({ id: r.id, title: r.title, next });
  };

  const handleTransitionConfirm = async () => {
    if (!pendingTransition) return;
    const { id, next } = pendingTransition;
    setPendingTransition(null);
    await runTransition(id, next, transitionNote.trim());
  };

  const handleDelete = async () => {
    if (!pendingDelete) return;
    const id = pendingDelete.id;
    setPendingDelete(null);
    try {
      await adminApi.deleteAcceptanceRecord(id);
      toast({ title: t("acceptance.toast.deleted") });
      if (detail?.id === id) setDetail(null);
      if (detail?.id === id) setDetailOpen(false);
      await load();
    } catch (e) {
      toast({
        variant: "destructive",
        title: extractApiError(e) || t("acceptance.error"),
      });
    }
  };

  const handleBulkDelete = async () => {
    setPendingBulk(false);
    if (selected.size === 0) return;
    try {
      const res = await adminApi.bulkDeleteAcceptanceRecords({ ids: Array.from(selected) });
      const deletedCount = res.data.deletedIds?.length ?? 0;
      const skippedCount = res.data.skippedIds?.length ?? 0;
      toast({
        title: t("acceptance.toast.bulkDeleted").replace("{count}", String(deletedCount)),
        description:
          skippedCount > 0
            ? t("acceptance.toast.bulkSkipped").replace("{count}", String(skippedCount))
            : undefined,
      });
      await load();
    } catch (e) {
      toast({
        variant: "destructive",
        title: extractApiError(e) || t("acceptance.error"),
      });
    }
  };

  // Which workflow actions are available for a row given status + perms.
  const availableTransitions = (r: AcceptanceRecordResponse): Array<{
    next: AcceptanceStatus;
    label: string;
    testId: string;
    requiresPerm: boolean;
  }> => {
    const out: Array<{ next: AcceptanceStatus; label: string; testId: string; requiresPerm: boolean }> = [];
    switch (r.status) {
      case "Draft":
        if (canManage) out.push({ next: "Submitted", label: t("acceptance.action.submit"), testId: "acceptance-submit", requiresPerm: true });
        if (canManage) out.push({ next: "Cancelled", label: t("acceptance.action.cancel"), testId: "acceptance-cancel", requiresPerm: true });
        break;
      case "Submitted":
        if (canApprove) out.push({ next: "Approved", label: t("acceptance.action.approve"), testId: "acceptance-approve", requiresPerm: true });
        if (canManage) out.push({ next: "Rejected", label: t("acceptance.action.reject"), testId: "acceptance-reject", requiresPerm: true });
        if (canManage) out.push({ next: "Cancelled", label: t("acceptance.action.cancel"), testId: "acceptance-cancel", requiresPerm: true });
        break;
      case "Rejected":
        if (canManage) out.push({ next: "Draft", label: t("acceptance.action.revise"), testId: "acceptance-revise", requiresPerm: true });
        if (canManage) out.push({ next: "Cancelled", label: t("acceptance.action.cancel"), testId: "acceptance-cancel", requiresPerm: true });
        break;
      case "Approved":
        if (canManage) out.push({ next: "Cancelled", label: t("acceptance.action.cancel"), testId: "acceptance-cancel", requiresPerm: true });
        break;
      case "Cancelled":
        break;
    }
    return out;
  };

  const stats: StatTile[] = [
    {
      key: "total",
      label: t("acceptance.stats.total"),
      value: total,
      gradient: "from-indigo-500 to-violet-500",
      icon: ClipboardList,
    },
    {
      key: "open",
      label: t("acceptance.stats.open"),
      value: (statusCounts.Draft ?? 0) + (statusCounts.Submitted ?? 0) + (statusCounts.Rejected ?? 0),
      gradient: "from-slate-500 to-slate-700",
      icon: ClipboardCheck,
    },
    {
      key: "submitted",
      label: t("acceptance.stats.submitted"),
      value: statusCounts.Submitted ?? 0,
      gradient: "from-sky-500 to-cyan-500",
      icon: FileCheck,
    },
    {
      key: "overdue",
      label: t("acceptance.stats.overdue"),
      value: overdueCount,
      gradient: "from-amber-500 to-orange-500",
      icon: AlertTriangle,
    },
  ];

  const hasActiveFilters = Boolean(
    projectId || taskId || responsibleUserId || acceptanceFrom || acceptanceTo
      || status || search.trim() || openOnly || overdueOnly,
  );

  const rowClickable = useCallback(
    (r: AcceptanceRecordResponse) => ({
      role: "button" as const,
      tabIndex: 0,
      onClick: () => void openDetail(r.id),
      onKeyDown: (e: React.KeyboardEvent) => {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          void openDetail(r.id);
        }
      },
    }),
    [openDetail],
  );

  return (
    <AdminLayout>
      <div className="space-y-6 p-4 md:p-6" data-testid="acceptance-page">
        <header className="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 className="text-2xl font-bold">{t("acceptance.title")}</h1>
            <p className="text-sm text-muted-foreground">{t("acceptance.subtitle")}</p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={load} disabled={loading}>
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button size="sm" onClick={openCreate} data-testid="acceptance-new" disabled={projectId == null}>
                <Plus className="mr-2 h-4 w-4" />
                {t("acceptance.action.new")}
              </Button>
            )}
          </div>
        </header>

        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          {stats.map((s) => {
            const Icon = s.icon;
            return (
              <div
                key={s.key}
                className={cn(
                  "relative overflow-hidden rounded-xl p-4 text-white shadow-md",
                  `bg-gradient-to-br ${s.gradient}`,
                )}
                data-testid={`acceptance-stat-${s.key}`}
              >
                <div className="flex items-center justify-between">
                  <span className="text-xs font-medium uppercase tracking-wider opacity-90">
                    {s.label}
                  </span>
                  <Icon className="h-5 w-5 opacity-80" />
                </div>
                <div className="mt-2 text-3xl font-bold">{s.value}</div>
              </div>
            );
          })}
        </div>

        <div className="rounded-lg border bg-card p-3 md:p-4">
          <div className="grid grid-cols-1 gap-3 md:grid-cols-4 [&>div]:min-w-0">
            <div>
              <Label>{t("acceptance.field.project")}</Label>
              <SearchableSelect
                options={[
                  { value: "", label: t("acceptance.filter.project.all") },
                  ...projects.map((p) => ({ value: String(p.id), label: p.name })),
                ]}
                value={projectId != null ? String(projectId) : ""}
                onChange={(v) => {
                  setProjectId(v ? Number(v) : undefined);
                  setTaskId(undefined);
                  setPage(1);
                }}
                placeholder={t("acceptance.filter.project.all")}
              />
            </div>
            <div>
              <Label>{t("acceptance.field.task")}</Label>
              <SearchableSelect
                options={[
                  { value: "", label: t("acceptance.filter.task.all") },
                  ...tasks.map((task) => ({
                    value: String(task.id),
                    label: `${task.taskCode} — ${task.name}`,
                  })),
                ]}
                value={taskId != null ? String(taskId) : ""}
                onChange={(value) => {
                  setTaskId(value ? Number(value) : undefined);
                  setPage(1);
                }}
                placeholder={t("acceptance.filter.task.all")}
              />
            </div>
            <div>
              <Label>{t("acceptance.filter.responsible")}</Label>
              <SearchableSelect
                options={[
                  { value: "", label: t("acceptance.filter.responsible.all") },
                  ...responsibleOptions,
                ]}
                value={responsibleUserId != null ? String(responsibleUserId) : ""}
                onChange={(value) => {
                  setResponsibleUserId(value ? Number(value) : undefined);
                  setPage(1);
                }}
                placeholder={t("acceptance.filter.responsible.all")}
              />
            </div>
            <div>
              <Label>{t("acceptance.field.status")}</Label>
              <Select
                value={status || "__all__"}
                onValueChange={(v) => {
                  setStatus(v === "__all__" ? "" : (v as AcceptanceStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger data-testid="acceptance-filter-status">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("acceptance.filter.status.all")}</SelectItem>
                  {ACCEPTANCE_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`acceptance.status.${s.toLowerCase()}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>{t("acceptance.filter.search")}</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-2 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder={t("acceptance.filter.search")}
                  className="pl-8"
                  data-testid="acceptance-search"
                />
              </div>
            </div>
            <div className="flex items-end gap-4">
              <label className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={openOnly}
                  onCheckedChange={(v) => {
                    setOpenOnly(!!v);
                    setPage(1);
                  }}
                />
                {t("acceptance.filter.openOnly")}
              </label>
              <label className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={overdueOnly}
                  onCheckedChange={(v) => {
                    setOverdueOnly(!!v);
                    setPage(1);
                  }}
                />
                {t("acceptance.filter.overdueOnly")}
              </label>
            </div>
            <div>
              <Label>{t("acceptance.filter.from")}</Label>
              <Input
                type="date"
                value={acceptanceFrom}
                onChange={(event) => {
                  setAcceptanceFrom(event.target.value);
                  setPage(1);
                }}
              />
            </div>
            <div>
              <Label>{t("acceptance.filter.to")}</Label>
              <Input
                type="date"
                value={acceptanceTo}
                onChange={(event) => {
                  setAcceptanceTo(event.target.value);
                  setPage(1);
                }}
              />
            </div>
            <div>
              <Label>{t("acceptance.filter.sort")}</Label>
              <Select value={sortBy} onValueChange={(value) => setSortBy(value as typeof sortBy)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="date">{t("acceptance.field.date")}</SelectItem>
                  <SelectItem value="code">{t("acceptance.field.code")}</SelectItem>
                  <SelectItem value="title">{t("acceptance.field.title")}</SelectItem>
                  <SelectItem value="project">{t("acceptance.field.project")}</SelectItem>
                  <SelectItem value="status">{t("acceptance.field.status")}</SelectItem>
                  <SelectItem value="updatedAt">{t("acceptance.field.updatedAt")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>{t("acceptance.filter.direction")}</Label>
              <Select value={sortDirection} onValueChange={(value) => setSortDirection(value as "asc" | "desc")}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="asc">{t("acceptance.filter.asc")}</SelectItem>
                  <SelectItem value="desc">{t("acceptance.filter.desc")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
        </div>

        {canManage && selected.size > 0 && (
          <div className="flex items-center justify-between rounded-lg border bg-muted/40 px-4 py-2">
            <span className="text-sm">
              {selected.size} / {rows.length}
            </span>
            <Button
              variant="destructive"
              size="sm"
              onClick={() => setPendingBulk(true)}
              data-testid="acceptance-bulk-delete"
            >
              <Trash2 className="mr-2 h-4 w-4" />
              {t("acceptance.action.bulkDelete").replace("{count}", String(selected.size))}
            </Button>
          </div>
        )}

        {loading ? (
          <PageLoading label={t("acceptance.loading")} />
        ) : error ? (
          <PageError message={error} onRetry={load} />
        ) : rows.length === 0 ? (
          <div className="rounded-lg border bg-card p-8 text-center text-sm text-muted-foreground">
            {t(hasActiveFilters ? "acceptance.noResults" : "acceptance.empty")}
          </div>
        ) : (
          <>
            {/* Desktop table */}
            <div className="hidden overflow-hidden rounded-lg border bg-card md:block">
              <table className="w-full text-sm">
                <thead className="bg-muted/40 text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    {canManage && (
                      <th className="w-10 px-3 py-2">
                        <Checkbox
                          checked={selected.size === rows.length && rows.length > 0}
                          onCheckedChange={(v) => {
                            if (v) setSelected(new Set(rows.map((r) => r.id)));
                            else setSelected(new Set());
                          }}
                          aria-label={t("acceptance.action.selectAll")}
                        />
                      </th>
                    )}
                    <th className="px-3 py-2">{t("acceptance.field.code")}</th>
                    <th className="px-3 py-2">{t("acceptance.field.title")}</th>
                    <th className="px-3 py-2">{t("acceptance.field.project")}</th>
                    <th className="px-3 py-2">{t("acceptance.field.date")}</th>
                    <th className="px-3 py-2">{t("acceptance.field.status")}</th>
                    <th className="px-3 py-2 text-right">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr
                      key={r.id}
                      className="cursor-pointer border-t hover:bg-muted/40"
                      data-testid={`acceptance-row-${r.id}`}
                      {...rowClickable(r)}
                    >
                      {canManage && (
                        <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                          <Checkbox
                            checked={selected.has(r.id)}
                            onCheckedChange={(v) => {
                              const s = new Set(selected);
                              if (v) s.add(r.id);
                              else s.delete(r.id);
                              setSelected(s);
                            }}
                            aria-label={`select-${r.id}`}
                          />
                        </td>
                      )}
                      <td className="px-3 py-2 font-mono text-xs">{r.acceptanceCode}</td>
                      <td className="px-3 py-2">
                        <div className="font-medium">{r.title}</div>
                        {r.location && (
                          <div className="text-xs text-muted-foreground">{r.location}</div>
                        )}
                      </td>
                      <td className="px-3 py-2 text-xs text-muted-foreground">
                        {r.designProjectName}
                      </td>
                      <td className="px-3 py-2 text-xs">{formatDate(r.acceptanceDate)}</td>
                      <td className="px-3 py-2">
                        <div className="flex flex-wrap items-center gap-1">
                          <Badge variant="outline" className={STATUS_BADGE[r.status]}>
                            {t(`acceptance.status.${r.status.toLowerCase()}`)}
                          </Badge>
                          {r.isOverdue && (
                            <Badge variant="outline" className={OVERDUE_BADGE}>
                              {t("acceptance.stats.overdue")}
                            </Badge>
                          )}
                        </div>
                      </td>
                      <td className="px-3 py-2 text-right" onClick={(e) => e.stopPropagation()}>
                        <div className="flex justify-end gap-1">
                          <Button variant="ghost" size="icon" title={t("common.view")} aria-label={t("common.view")} onClick={() => void openDetail(r.id)} data-testid={`acceptance-row-view-${r.id}`}>
                            <Eye className="h-4 w-4" />
                          </Button>
                          {canManage && (
                            <>
                              <Button
                                variant="ghost"
                                size="icon"
                                title={t("common.edit")}
                                aria-label={t("common.edit")}
                                disabled={!OPEN_STATUSES.has(r.status)}
                                onClick={() => openEdit(r)}
                                data-testid={`acceptance-row-edit-${r.id}`}
                              >
                                <Pencil className="h-4 w-4" />
                              </Button>
                              <Button
                                variant="ghost"
                                size="icon"
                                title={t("common.delete")}
                                aria-label={t("common.delete")}
                                onClick={() => setPendingDelete(r)}
                                data-testid={`acceptance-row-delete-${r.id}`}
                              >
                                <Trash2 className="h-4 w-4 text-rose-500" />
                              </Button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Mobile cards */}
            <div className="grid grid-cols-1 gap-3 md:hidden">
              {rows.map((r) => (
                <article
                  key={r.id}
                  className="rounded-lg border bg-card p-3 shadow-sm"
                  data-testid={`acceptance-card-${r.id}`}
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <div className="font-mono text-xs text-muted-foreground">{r.acceptanceCode}</div>
                      <div className="truncate font-medium">{r.title}</div>
                      <div className="mt-1 text-xs text-muted-foreground">{r.designProjectName}</div>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <Badge variant="outline" className={STATUS_BADGE[r.status]}>
                        {t(`acceptance.status.${r.status.toLowerCase()}`)}
                      </Badge>
                      {r.isOverdue && (
                        <Badge variant="outline" className={OVERDUE_BADGE}>
                          {t("acceptance.stats.overdue")}
                        </Badge>
                      )}
                    </div>
                  </div>
                  <div className="mt-2 text-xs text-muted-foreground">{formatDate(r.acceptanceDate)}</div>
                  <div className="mt-3 flex flex-wrap justify-end gap-1 border-t pt-2">
                    <Button variant="ghost" size="sm" onClick={() => void openDetail(r.id)} data-testid={`acceptance-card-view-${r.id}`}>
                      <Eye className="mr-1 h-4 w-4" />{t("common.view")}
                    </Button>
                    {canManage && (
                      <>
                        <Button variant="ghost" size="sm" disabled={!OPEN_STATUSES.has(r.status)} onClick={() => openEdit(r)} data-testid={`acceptance-card-edit-${r.id}`}>
                          <Pencil className="mr-1 h-4 w-4" />{t("common.edit")}
                        </Button>
                        <Button variant="ghost" size="sm" className="text-destructive hover:text-destructive" onClick={() => setPendingDelete(r)} data-testid={`acceptance-card-delete-${r.id}`}>
                          <Trash2 className="mr-1 h-4 w-4" />{t("common.delete")}
                        </Button>
                      </>
                    )}
                  </div>
                </article>
              ))}
            </div>

            <div className="flex items-center justify-between text-xs text-muted-foreground">
              <div>
                {t("common.pagination.total").replace("{total}", String(total))}
              </div>
              <div className="flex gap-1">
                <Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                  {t("common.pagination.prev")}
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page * pageSize >= total}
                  onClick={() => setPage(page + 1)}
                >
                  {t("common.pagination.next")}
                </Button>
              </div>
            </div>
          </>
        )}
      </div>

      {/* Detail sheet */}
      <Sheet
        open={detailOpen}
        onOpenChange={(open) => {
          setDetailOpen(open);
          if (!open) {
            setDetail(null);
            setDetailError(null);
          }
        }}
      >
        <SheetContent className="w-full overflow-y-auto sm:max-w-lg">
          {detailLoading && <PageLoading label={t("acceptance.detail.loading")} />}
          {!detailLoading && detailError && (
            <PageError message={detailError} onRetry={() => detailId && void openDetail(detailId)} />
          )}
          {detail && (
            <>
              <SheetHeader>
                <SheetTitle className="text-lg">
                  <span className="mr-2 font-mono text-xs text-muted-foreground">{detail.acceptanceCode}</span>
                  {detail.title}
                </SheetTitle>
                <SheetDescription>{detail.designProjectName}</SheetDescription>
              </SheetHeader>
              <div className="mt-4 space-y-4 text-sm">
                <div className="flex flex-wrap gap-2">
                  <Badge variant="outline" className={STATUS_BADGE[detail.status]}>
                    {t(`acceptance.status.${detail.status.toLowerCase()}`)}
                  </Badge>
                  {detail.isOverdue && (
                    <Badge variant="outline" className={OVERDUE_BADGE}>
                      {t("acceptance.stats.overdue")}
                    </Badge>
                  )}
                  {detail.revisionCount > 0 && (
                    <Badge variant="outline">
                      {t("acceptance.field.revisionCount")}: {detail.revisionCount}
                    </Badge>
                  )}
                </div>

                <dl className="grid grid-cols-2 gap-2 text-xs">
                  <dt className="text-muted-foreground">{t("acceptance.field.date")}</dt>
                  <dd>{formatDate(detail.acceptanceDate)}</dd>
                  <dt className="text-muted-foreground">{t("acceptance.field.task")}</dt>
                  <dd>{detail.constructionTaskName ?? "—"}</dd>
                  <dt className="text-muted-foreground">{t("acceptance.field.location")}</dt>
                  <dd>{detail.location ?? "—"}</dd>
                  <dt className="text-muted-foreground">{t("acceptance.field.participants")}</dt>
                  <dd className="whitespace-pre-wrap">{detail.participants ?? "—"}</dd>
                  <dt className="text-muted-foreground">{t("acceptance.field.findings")}</dt>
                  <dd className="whitespace-pre-wrap">{detail.findings ?? "—"}</dd>
                  {detail.description && (
                    <>
                      <dt className="text-muted-foreground">{t("acceptance.field.description")}</dt>
                      <dd className="whitespace-pre-wrap">{detail.description}</dd>
                    </>
                  )}
                  {detail.resolutionNote && (
                    <>
                      <dt className="text-muted-foreground">{t("acceptance.field.resolutionNote")}</dt>
                      <dd className="whitespace-pre-wrap">{detail.resolutionNote}</dd>
                    </>
                  )}
                  {detail.submittedAt && (
                    <>
                      <dt className="text-muted-foreground">{t("acceptance.field.submittedBy")}</dt>
                      <dd>
                        {detail.submittedByName ?? "—"} · {formatDateTime(detail.submittedAt)}
                      </dd>
                    </>
                  )}
                  {detail.approvedAt && (
                    <>
                      <dt className="text-muted-foreground">{t("acceptance.field.approvedBy")}</dt>
                      <dd>
                        {detail.approvedByName ?? "—"} · {formatDateTime(detail.approvedAt)}
                      </dd>
                    </>
                  )}
                  {detail.rejectedAt && (
                    <>
                      <dt className="text-muted-foreground">{t("acceptance.field.rejectedBy")}</dt>
                      <dd>
                        {detail.rejectedByName ?? "—"} · {formatDateTime(detail.rejectedAt)}
                      </dd>
                    </>
                  )}
                </dl>

                <div className="border-t pt-3">
                  <h3 className="mb-2 flex items-center gap-2 font-medium">
                    <FileText className="h-4 w-4" />
                    {t("acceptance.field.documents")}
                  </h3>
                  {parseDocuments(detail.documents).length === 0 ? (
                    <p className="text-xs text-muted-foreground">{t("acceptance.documents.empty")}</p>
                  ) : (
                    <ul className="space-y-2">
                      {parseDocuments(detail.documents).map((path, index) => {
                        const label = path.split("/").pop() || path;
                        return (
                          <li key={path} className="flex items-center gap-2 rounded-md border px-2 py-1.5">
                            <FileText className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                            <span className="min-w-0 flex-1 break-all text-sm">{label}</span>
                            <AdminFilePreview
                              url={path}
                              fileName={label}
                              testId={`acceptance-document-preview-${index}`}
                            />
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </div>

                {(canManage || canApprove) && detail.status !== "Cancelled" && (
                  <div className="flex flex-wrap gap-2 border-t pt-3">
                    {canManage && !OPEN_STATUSES.has(detail.status) ? null : canManage &&
                      detail.status !== "Approved" ? (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => openEdit(detail)}
                        data-testid="acceptance-edit"
                      >
                        {t("acceptance.action.edit")}
                      </Button>
                    ) : null}
                    {availableTransitions(detail).map((tr) => (
                      <Button
                        key={tr.next}
                        variant={tr.next === "Approved" ? "default" : "outline"}
                        size="sm"
                        onClick={() => confirmTransition(detail, tr.next)}
                        data-testid={tr.testId}
                      >
                        {tr.next === "Approved" && <CheckCircle2 className="mr-2 h-4 w-4" />}
                        {tr.label}
                      </Button>
                    ))}
                    {canManage && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-rose-600"
                        onClick={() => setPendingDelete(detail)}
                        data-testid="acceptance-delete"
                      >
                        <Trash2 className="mr-2 h-4 w-4" />
                        {t("acceptance.action.delete")}
                      </Button>
                    )}
                  </div>
                )}
              </div>
            </>
          )}
        </SheetContent>
      </Sheet>

      {/* Create / edit dialog */}
      <Dialog open={formOpen} onOpenChange={setFormOpen}>
        <DialogContent className="max-h-[90vh] max-w-lg overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {editingId ? t("acceptance.form.editTitle") : t("acceptance.form.newTitle")}
            </DialogTitle>
            <DialogDescription>
              {editingId
                ? projects.find((p) => p.id === rows.find((r) => r.id === editingId)?.designProjectId)?.name
                : projects.find((p) => p.id === projectId)?.name}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label>{t("acceptance.field.title")}</Label>
              <Input
                value={form.title}
                onChange={(e) => setForm({ ...form, title: e.target.value })}
                data-testid="acceptance-form-title"
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>{t("acceptance.field.date")}</Label>
                <Input
                  type="date"
                  value={form.acceptanceDate}
                  onChange={(e) => setForm({ ...form, acceptanceDate: e.target.value })}
                  data-testid="acceptance-form-date"
                />
              </div>
              <div>
                <Label>{t("acceptance.field.location")}</Label>
                <Input
                  value={form.location}
                  onChange={(e) => setForm({ ...form, location: e.target.value })}
                />
              </div>
            </div>
            <div data-testid="acceptance-form-task">
              <Label>{t("acceptance.field.task")}</Label>
              <SearchableSelect
                options={[
                  { value: "", label: t("acceptance.filter.task.none") },
                  ...tasks.map((task) => ({
                    value: String(task.id),
                    label: `${task.taskCode} — ${task.name}`,
                  })),
                ]}
                value={form.constructionTaskId}
                onChange={(value) => setForm({ ...form, constructionTaskId: value })}
                placeholder={t("acceptance.filter.task.none")}
              />
            </div>
            <div>
              <Label>{t("acceptance.field.participants")}</Label>
              <Input
                value={form.participants}
                onChange={(e) => setForm({ ...form, participants: e.target.value })}
              />
            </div>
            <div>
              <Label>{t("acceptance.field.findings")}</Label>
              <Textarea
                rows={3}
                value={form.findings}
                onChange={(e) => setForm({ ...form, findings: e.target.value })}
              />
            </div>
            <div>
              <Label>{t("acceptance.field.description")}</Label>
              <Textarea
                rows={2}
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="acceptance-form-documents">
                {t("acceptance.field.documents")}
              </Label>
              <Textarea
                id="acceptance-form-documents"
                rows={3}
                value={form.documents}
                placeholder={t("acceptance.form.documentsPlaceholder")}
                onChange={(event) => setForm({ ...form, documents: event.target.value })}
                aria-describedby="acceptance-form-documents-help"
                data-testid="acceptance-form-documents"
              />
              <p id="acceptance-form-documents-help" className="mt-1 text-xs text-muted-foreground">
                {t("acceptance.form.documentsHelp")}
              </p>
              {documentPaths(form.documents).length > 0 && (
                <div className="mt-2 flex flex-wrap gap-2">
                  {documentPaths(form.documents).map((path) => (
                    <AdminFilePreview
                      key={path}
                      url={path}
                      fileName={path.split("/").pop() || path}
                      showLabel
                      label={path.split("/").pop() || t("common.previewFile")}
                    />
                  ))}
                </div>
              )}
            </div>
            {formError && <div role="alert" className="text-sm text-rose-600">{formError}</div>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setFormOpen(false)}>
              {t("acceptance.action.close")}
            </Button>
            <Button onClick={handleSave} disabled={saving} data-testid="acceptance-form-save">
              {saving ? t("acceptance.form.saving") : t("acceptance.form.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Confirm transition */}
      <AlertDialog open={!!pendingTransition} onOpenChange={(o) => !o && setPendingTransition(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("acceptance.confirm.transition.title")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("acceptance.confirm.transition.body")
                .replace("{title}", pendingTransition?.title ?? "")
                .replace(
                  "{status}",
                  pendingTransition ? t(`acceptance.status.${pendingTransition.next.toLowerCase()}`) : "",
                )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div>
            <Textarea
              rows={2}
              placeholder={t("acceptance.confirm.reasonPlaceholder")}
              value={transitionNote}
              onChange={(e) => setTransitionNote(e.target.value)}
              data-testid="acceptance-transition-note"
            />
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("acceptance.action.close")}</AlertDialogCancel>
            <AlertDialogAction onClick={handleTransitionConfirm} data-testid="acceptance-action-confirm">
              {t("acceptance.form.save")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Confirm delete */}
      <AlertDialog open={!!pendingDelete} onOpenChange={(o) => !o && setPendingDelete(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("acceptance.confirm.delete.title")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("acceptance.confirm.delete.body").replace("{title}", pendingDelete?.title ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("acceptance.action.close")}</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete} data-testid="acceptance-delete-confirm">
              {t("acceptance.action.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Confirm bulk */}
      <AlertDialog open={pendingBulk} onOpenChange={setPendingBulk}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {t("acceptance.confirm.bulkDelete.title").replace("{count}", String(selected.size))}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {t("acceptance.confirm.bulkDelete.body").replace(
                "{titles}",
                rows
                  .filter((r) => selected.has(r.id))
                  .slice(0, 3)
                  .map((r) => `${r.acceptanceCode} — ${r.title}`)
                  .join(", ") + (selected.size > 3 ? ` … (+${selected.size - 3})` : ""),
              )}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>{t("acceptance.action.close")}</AlertDialogCancel>
            <AlertDialogAction onClick={handleBulkDelete} data-testid="acceptance-bulk-confirm">
              {t("acceptance.action.delete")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </AdminLayout>
  );
}
