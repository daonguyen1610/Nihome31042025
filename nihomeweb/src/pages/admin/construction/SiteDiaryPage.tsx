import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  CircleDot,
  ClipboardList,
  CloudSun,
  RefreshCcw,
  Search,
  Send,
  Trash2,
  Users,
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
  type CreateSiteDiaryRequest,
  type DesignProjectListItemResponse,
  type MasterDataOption,
  type SiteDiaryListParams,
  type SiteDiaryResponse,
  type SiteDiaryStatus,
  type UpdateSiteDiaryRequest,
} from "@/services/adminApi";

const DIARY_STATUSES: SiteDiaryStatus[] = ["Draft", "Submitted", "Confirmed"];

const STATUS_BADGE: Record<SiteDiaryStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Confirmed: "border-emerald-200 bg-emerald-50 text-emerald-800",
};

type ConfirmKind = "submit" | "confirm" | "reopen" | "delete" | null;

const todayIso = () => new Date().toISOString().slice(0, 10);

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try { return new Date(iso).toLocaleDateString(lang); } catch { return iso; }
};

const formatDateTime = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try { return new Date(iso).toLocaleString(lang); } catch { return iso; }
};

interface DiaryForm {
  diaryDate: string;
  weatherCode: string;
  weatherNote: string;
  headcountLabor: number;
  headcountEngineers: number;
  headcountSupervisors: number;
  headcountSubcontractors: number;
  machinesSummary: string;
  materialsReceived: string;
  workPerformed: string;
  incidents: string;
  note: string;
}

const emptyForm = (): DiaryForm => ({
  diaryDate: todayIso(),
  weatherCode: "sunny",
  weatherNote: "",
  headcountLabor: 0,
  headcountEngineers: 0,
  headcountSupervisors: 0,
  headcountSubcontractors: 0,
  machinesSummary: "",
  materialsReceived: "",
  workPerformed: "",
  incidents: "",
  note: "",
});

const formFromRow = (row: SiteDiaryResponse): DiaryForm => ({
  diaryDate: row.diaryDate,
  weatherCode: row.weatherCode,
  weatherNote: row.weatherNote ?? "",
  headcountLabor: row.headcountLabor,
  headcountEngineers: row.headcountEngineers,
  headcountSupervisors: row.headcountSupervisors,
  headcountSubcontractors: row.headcountSubcontractors,
  machinesSummary: row.machinesSummary ?? "",
  materialsReceived: row.materialsReceived ?? "",
  workPerformed: row.workPerformed,
  incidents: row.incidents ?? "",
  note: row.note ?? "",
});

const AdminSiteDiary = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionDiaryManage);
  const canConfirm = has(ADMIN_PERMS.constructionDiaryConfirm);

  // list state
  const [rows, setRows] = useState<SiteDiaryResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<SiteDiaryStatus, number>>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const [projectId, setProjectId] = useState<number | null>(null);
  const [status, setStatus] = useState<SiteDiaryStatus | "">("");
  const [weatherCode, setWeatherCode] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 50;

  // selection
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [confirmBulkDelete, setConfirmBulkDelete] = useState(false);
  const [bulkDeleting, setBulkDeleting] = useState(false);

  // dialogs
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState<DiaryForm>(() => emptyForm());
  const [createProjectId, setCreateProjectId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const [detail, setDetail] = useState<SiteDiaryResponse | null>(null);
  const [detailForm, setDetailForm] = useState<DiaryForm | null>(null);
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [confirmAction, setConfirmAction] = useState<{ kind: ConfirmKind; row: SiteDiaryResponse | null }>({
    kind: null,
    row: null,
  });
  const [actionBusy, setActionBusy] = useState(false);

  // lookups
  const [projects, setProjects] = useState<DesignProjectListItemResponse[]>([]);
  const [weatherOptions, setWeatherOptions] = useState<MasterDataOption[]>([]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [projResp, wxResp] = await Promise.all([
          adminApi.listDesignProjects({ pageSize: 200 }).catch(() => ({
            data: { total: 0, page: 1, pageSize: 200, items: [] },
          })),
          adminApi.getMasterDataOptions("diary_weather").catch(() => ({ data: [] })),
        ]);
        if (cancelled) return;
        setProjects(projResp.data.items ?? []);
        setWeatherOptions((wxResp.data ?? []).filter((o) => o.isActive));
      } catch {
        // non-fatal
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: SiteDiaryListParams = { page, pageSize };
      if (projectId != null) params.designProjectId = projectId;
      if (status) params.status = status;
      if (weatherCode) params.weatherCode = weatherCode;
      if (dateFrom) params.dateFrom = dateFrom;
      if (dateTo) params.dateTo = dateTo;
      if (search.trim()) params.search = search.trim();
      const { data } = await adminApi.listSiteDiaries(params);
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
      setStatusCounts(data.statusCounts ?? {});
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
  }, [page, pageSize, projectId, status, weatherCode, dateFrom, dateTo, search, t, toast]);

  useEffect(() => { void fetchList(); }, [fetchList]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const statusPill = (s: SiteDiaryStatus) => statusCounts[s] ?? 0;

  const projectOptions = useMemo(
    () => [
      { value: "", label: t("siteDiary.filter.allProjects") },
      ...projects.map((p) => ({ value: String(p.id), label: `${p.projectCode} — ${p.name}` })),
    ],
    [projects, t],
  );
  const projectOptionsForCreate = useMemo(
    () => projects.map((p) => ({ value: String(p.id), label: `${p.projectCode} — ${p.name}` })),
    [projects],
  );
  const weatherSelectOptions = useMemo(
    () => [
      { value: "__all__", label: t("siteDiary.filter.allWeather") },
      ...weatherOptions.map((o) => ({ value: o.code, label: o.name })),
    ],
    [weatherOptions, t],
  );

  const hasActiveFilter =
    projectId != null || status !== "" || weatherCode !== "" || dateFrom !== "" || dateTo !== "" || search.trim() !== "";

  const resetFilters = () => {
    setProjectId(null);
    setStatus("");
    setWeatherCode("");
    setDateFrom("");
    setDateTo("");
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
    if (createProjectId == null) { setCreateError(t("siteDiary.form.projectRequired")); return; }
    if (!createForm.weatherCode) { setCreateError(t("siteDiary.form.weatherRequired")); return; }
    if (!createForm.workPerformed.trim()) { setCreateError(t("siteDiary.form.workRequired")); return; }
    setSaving(true);
    try {
      const body: CreateSiteDiaryRequest = {
        designProjectId: createProjectId,
        diaryDate: createForm.diaryDate,
        weatherCode: createForm.weatherCode,
        weatherNote: createForm.weatherNote.trim() || undefined,
        headcountLabor: createForm.headcountLabor,
        headcountEngineers: createForm.headcountEngineers,
        headcountSupervisors: createForm.headcountSupervisors,
        headcountSubcontractors: createForm.headcountSubcontractors,
        machinesSummary: createForm.machinesSummary.trim() || undefined,
        materialsReceived: createForm.materialsReceived.trim() || undefined,
        workPerformed: createForm.workPerformed.trim(),
        incidents: createForm.incidents.trim() || undefined,
        note: createForm.note.trim() || undefined,
      };
      await adminApi.createSiteDiary(body);
      toast({ title: t("siteDiary.created") });
      setCreating(false);
      await fetchList();
    } catch (err) {
      setCreateError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // ---- detail ----
  const openDetail = (row: SiteDiaryResponse) => {
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

  const canEditDetail = canManage && detail?.status === "Draft";

  const submitDetail = async () => {
    if (!detail || !detailForm) return;
    setDetailError(null);
    if (!detailForm.weatherCode) { setDetailError(t("siteDiary.form.weatherRequired")); return; }
    if (!detailForm.workPerformed.trim()) { setDetailError(t("siteDiary.form.workRequired")); return; }
    setDetailSaving(true);
    try {
      const body: UpdateSiteDiaryRequest = {
        diaryDate: detailForm.diaryDate,
        weatherCode: detailForm.weatherCode,
        weatherNote: detailForm.weatherNote.trim() || undefined,
        headcountLabor: detailForm.headcountLabor,
        headcountEngineers: detailForm.headcountEngineers,
        headcountSupervisors: detailForm.headcountSupervisors,
        headcountSubcontractors: detailForm.headcountSubcontractors,
        machinesSummary: detailForm.machinesSummary.trim() || undefined,
        materialsReceived: detailForm.materialsReceived.trim() || undefined,
        workPerformed: detailForm.workPerformed.trim(),
        incidents: detailForm.incidents.trim() || undefined,
        note: detailForm.note.trim() || undefined,
      };
      const { data: updated } = await adminApi.updateSiteDiary(detail.id, body);
      toast({ title: t("siteDiary.updated") });
      setDetail(updated);
      await fetchList();
    } catch (err) {
      setDetailError(extractApiError(err));
    } finally {
      setDetailSaving(false);
    }
  };

  // ---- workflow actions (submit / confirm / reopen / delete single) ----
  const runAction = async () => {
    const { kind, row } = confirmAction;
    if (!kind || !row) return;
    setActionBusy(true);
    try {
      let updated: SiteDiaryResponse | null = null;
      if (kind === "submit") {
        const { data } = await adminApi.submitSiteDiary(row.id);
        updated = data;
        toast({ title: t("siteDiary.submitted") });
      } else if (kind === "confirm") {
        const { data } = await adminApi.confirmSiteDiary(row.id);
        updated = data;
        toast({ title: t("siteDiary.confirmed") });
      } else if (kind === "reopen") {
        const { data } = await adminApi.reopenSiteDiary(row.id);
        updated = data;
        toast({ title: t("siteDiary.reopened") });
      } else if (kind === "delete") {
        await adminApi.deleteSiteDiary(row.id);
        toast({ title: t("siteDiary.deleted") });
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
      const { data } = await adminApi.bulkDeleteSiteDiaries({ ids: Array.from(selected) });
      toast({
        title: t("siteDiary.bulkDeleted")
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
  const renderStatusBadge = (row: SiteDiaryResponse) => (
    <Badge variant="outline" className={cn(STATUS_BADGE[row.status])}>
      {t(`siteDiary.status.${row.status}`)}
    </Badge>
  );

  const renderConfirmMessage = () => {
    const { kind, row } = confirmAction;
    if (!kind || !row) return "";
    const date = formatDate(row.diaryDate, lang);
    switch (kind) {
      case "submit": return t("siteDiary.confirmSubmit").replace("{date}", date);
      case "confirm": return t("siteDiary.confirmConfirm").replace("{date}", date);
      case "reopen": return t("siteDiary.confirmReopen").replace("{date}", date);
      case "delete": return t("siteDiary.confirmDelete").replace("{date}", date);
      default: return "";
    }
  };
  const confirmTitle = () => {
    switch (confirmAction.kind) {
      case "submit": return t("siteDiary.action.submit");
      case "confirm": return t("siteDiary.action.confirm");
      case "reopen": return t("siteDiary.action.reopen");
      case "delete": return t("siteDiary.action.delete");
      default: return "";
    }
  };

  return (
    <AdminLayout>
      <div className="space-y-6" data-testid="site-diary-page">
        {/* header */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900">{t("siteDiary.title")}</h1>
            <p className="text-sm text-slate-500">{t("siteDiary.subtitle")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => void fetchList()}>
              <RefreshCcw className="mr-2 h-4 w-4" />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button size="sm" onClick={openCreate} data-testid="diary-new">
                <ClipboardList className="mr-2 h-4 w-4" />
                {t("siteDiary.new")}
              </Button>
            )}
          </div>
        </div>

        {/* stats */}
        <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
          <StatTile icon={<ClipboardList className="h-6 w-6" />} label={t("siteDiary.stat.total")} value={total} gradient="from-indigo-500 to-violet-600" />
          <StatTile icon={<CircleDot className="h-6 w-6" />} label={t("siteDiary.stat.draft")} value={statusPill("Draft")} gradient="from-slate-500 to-slate-700" />
          <StatTile icon={<Send className="h-6 w-6" />} label={t("siteDiary.stat.submitted")} value={statusPill("Submitted")} gradient="from-sky-500 to-blue-600" />
          <StatTile icon={<CheckCircle2 className="h-6 w-6" />} label={t("siteDiary.stat.confirmed")} value={statusPill("Confirmed")} gradient="from-emerald-500 to-teal-600" />
        </div>

        {/* filters */}
        <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 [&>div]:min-w-0">
            <div>
              <Label className="text-xs text-slate-500">{t("siteDiary.field.project")}</Label>
              <SearchableSelect
                value={projectId == null ? "" : String(projectId)}
                onChange={(v) => { setProjectId(v ? Number(v) : null); setPage(1); }}
                options={projectOptions}
                placeholder={t("siteDiary.filter.allProjects")}
              />
            </div>
            <div>
              <Label className="text-xs text-slate-500">{t("siteDiary.field.status")}</Label>
              <Select value={status || "__all__"} onValueChange={(v) => { setStatus(v === "__all__" ? "" : (v as SiteDiaryStatus)); setPage(1); }}>
                <SelectTrigger><SelectValue placeholder={t("siteDiary.filter.allStatuses")} /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("siteDiary.filter.allStatuses")}</SelectItem>
                  {DIARY_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>{t(`siteDiary.status.${s}`)}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label className="text-xs text-slate-500">{t("siteDiary.field.weather")}</Label>
              <Select value={weatherCode || "__all__"} onValueChange={(v) => { setWeatherCode(v === "__all__" ? "" : v); setPage(1); }}>
                <SelectTrigger><SelectValue placeholder={t("siteDiary.filter.allWeather")} /></SelectTrigger>
                <SelectContent>
                  {weatherSelectOptions.map((o) => (
                    <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label className="text-xs text-slate-500">{t("siteDiary.filter.search")}</Label>
              <div className="relative">
                <Search className="pointer-events-none absolute left-2 top-2.5 h-4 w-4 text-slate-400" />
                <Input
                  className="pl-8"
                  value={search}
                  onChange={(e) => { setSearch(e.target.value); setPage(1); }}
                  placeholder={t("siteDiary.filter.search")}
                  data-testid="diary-search"
                />
              </div>
            </div>
          </div>
          <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4 [&>div]:min-w-0">
            <div>
              <Label className="text-xs text-slate-500">{t("siteDiary.filter.dateFrom")}</Label>
              <Input type="date" value={dateFrom} onChange={(e) => { setDateFrom(e.target.value); setPage(1); }} />
            </div>
            <div>
              <Label className="text-xs text-slate-500">{t("siteDiary.filter.dateTo")}</Label>
              <Input type="date" value={dateTo} onChange={(e) => { setDateTo(e.target.value); setPage(1); }} />
            </div>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-3">
            {hasActiveFilter && (
              <Button variant="ghost" size="sm" onClick={resetFilters}>{t("common.reset")}</Button>
            )}
            {canManage && selected.size > 0 && (
              <Button variant="destructive" size="sm" onClick={() => setConfirmBulkDelete(true)} className="ml-auto" data-testid="diary-bulk-delete">
                <Trash2 className="mr-2 h-4 w-4" />
                {t("siteDiary.action.bulkDelete")} ({selected.size})
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
            {t("siteDiary.empty")}
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
                  className="relative rounded-xl border border-slate-200 bg-white p-4 text-left shadow-sm transition hover:border-slate-300 hover:shadow-md focus:outline-none focus:ring-2 focus:ring-slate-300"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="text-xs text-slate-500">{row.designProjectCode}</div>
                      <div className="text-base font-semibold text-slate-900">
                        {formatDate(row.diaryDate, lang)}
                      </div>
                    </div>
                    {canManage && (
                      <div onClick={(e) => e.stopPropagation()} role="presentation">
                        <Checkbox
                          checked={selected.has(row.id)}
                          onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                          aria-label={`m-select-${row.id}`}
                          className="mt-1"
                        />
                      </div>
                    )}
                  </div>
                  <div className="mt-2 flex flex-wrap items-center gap-2">
                    {renderStatusBadge(row)}
                    <span className="inline-flex items-center gap-1 rounded bg-sky-50 px-1.5 py-0.5 text-xs text-sky-700">
                      <CloudSun className="h-3 w-3" />
                      {row.weatherLabel ?? row.weatherCode}
                    </span>
                    <span className="inline-flex items-center gap-1 rounded bg-indigo-50 px-1.5 py-0.5 text-xs text-indigo-700">
                      <Users className="h-3 w-3" />
                      {row.headcountTotal}
                    </span>
                  </div>
                  <div className="mt-2 line-clamp-3 text-sm text-slate-700">{row.workPerformed}</div>
                  {row.incidents && (
                    <div className="mt-1 flex items-start gap-1 text-xs text-amber-700">
                      <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" />
                      <span className="line-clamp-2">{row.incidents}</span>
                    </div>
                  )}
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
                            aria-label={t("siteDiary.action.selectAll")}
                            checked={rows.length > 0 && selected.size === rows.length}
                            onCheckedChange={(v) => toggleSelectAll(!!v)}
                            data-testid="diary-select-all"
                          />
                        </th>
                      )}
                      <th className="px-3 py-2">{t("siteDiary.field.date")}</th>
                      <th className="px-3 py-2">{t("siteDiary.field.project")}</th>
                      <th className="px-3 py-2">{t("siteDiary.field.weather")}</th>
                      <th className="px-3 py-2">{t("siteDiary.field.headcountTotal")}</th>
                      <th className="px-3 py-2">{t("siteDiary.field.workPerformed")}</th>
                      <th className="px-3 py-2">{t("siteDiary.field.status")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {rows.map((row) => (
                      <tr
                        key={row.id}
                        className="cursor-pointer hover:bg-slate-50"
                        onClick={() => openDetail(row)}
                        data-testid={`diary-row-${row.id}`}
                      >
                        {canManage && (
                          <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                            <Checkbox
                              checked={selected.has(row.id)}
                              onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                              aria-label={`select-${row.id}`}
                            />
                          </td>
                        )}
                        <td className="px-3 py-2 whitespace-nowrap font-medium text-slate-900">
                          {formatDate(row.diaryDate, lang)}
                        </td>
                        <td className="px-3 py-2 text-xs text-slate-600">
                          <div className="font-mono">{row.designProjectCode}</div>
                          <div className="truncate max-w-[220px] text-slate-500">{row.designProjectName}</div>
                        </td>
                        <td className="px-3 py-2 text-xs text-slate-700">
                          <div className="inline-flex items-center gap-1">
                            <CloudSun className="h-3.5 w-3.5 text-sky-600" />
                            {row.weatherLabel ?? row.weatherCode}
                          </div>
                          {row.weatherNote && (
                            <div className="text-[11px] text-slate-500">{row.weatherNote}</div>
                          )}
                        </td>
                        <td className="px-3 py-2 text-sm tabular-nums text-slate-800">{row.headcountTotal}</td>
                        <td className="px-3 py-2">
                          <div className="line-clamp-2 max-w-[420px] text-sm text-slate-700">{row.workPerformed}</div>
                          {row.incidents && (
                            <div className="mt-0.5 flex items-start gap-1 text-[11px] text-amber-700">
                              <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" />
                              <span className="line-clamp-1">{row.incidents}</span>
                            </div>
                          )}
                        </td>
                        <td className="px-3 py-2">{renderStatusBadge(row)}</td>
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
          <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
            <DialogHeader>
              <DialogTitle>{t("siteDiary.new")}</DialogTitle>
              <DialogDescription>{t("siteDiary.form.hint")}</DialogDescription>
            </DialogHeader>
            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>{t("siteDiary.field.project")}</Label>
                  <SearchableSelect
                    value={createProjectId == null ? "" : String(createProjectId)}
                    onChange={(v) => setCreateProjectId(v ? Number(v) : null)}
                    options={projectOptionsForCreate}
                    placeholder="—"
                  />
                </div>
                <div>
                  <Label>{t("siteDiary.field.date")}</Label>
                  <Input type="date" value={createForm.diaryDate} onChange={(e) => setCreateForm((f) => ({ ...f, diaryDate: e.target.value }))} />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>{t("siteDiary.field.weather")}</Label>
                  <Select value={createForm.weatherCode} onValueChange={(v) => setCreateForm((f) => ({ ...f, weatherCode: v }))}>
                    <SelectTrigger><SelectValue /></SelectTrigger>
                    <SelectContent>
                      {weatherOptions.map((o) => (
                        <SelectItem key={o.code} value={o.code}>{o.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label>{t("siteDiary.field.weatherNote")}</Label>
                  <Input value={createForm.weatherNote} onChange={(e) => setCreateForm((f) => ({ ...f, weatherNote: e.target.value }))} />
                </div>
              </div>
              <div className="grid grid-cols-4 gap-2">
                <HeadcountField label={t("siteDiary.field.headcountLabor")} value={createForm.headcountLabor} onChange={(n) => setCreateForm((f) => ({ ...f, headcountLabor: n }))} />
                <HeadcountField label={t("siteDiary.field.headcountEngineers")} value={createForm.headcountEngineers} onChange={(n) => setCreateForm((f) => ({ ...f, headcountEngineers: n }))} />
                <HeadcountField label={t("siteDiary.field.headcountSupervisors")} value={createForm.headcountSupervisors} onChange={(n) => setCreateForm((f) => ({ ...f, headcountSupervisors: n }))} />
                <HeadcountField label={t("siteDiary.field.headcountSubcontractors")} value={createForm.headcountSubcontractors} onChange={(n) => setCreateForm((f) => ({ ...f, headcountSubcontractors: n }))} />
              </div>
              <div>
                <Label>{t("siteDiary.field.workPerformed")}</Label>
                <Textarea rows={3} value={createForm.workPerformed} onChange={(e) => setCreateForm((f) => ({ ...f, workPerformed: e.target.value }))} data-testid="diary-new-work" />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <Label>{t("siteDiary.field.machines")}</Label>
                  <Textarea rows={2} value={createForm.machinesSummary} onChange={(e) => setCreateForm((f) => ({ ...f, machinesSummary: e.target.value }))} />
                </div>
                <div>
                  <Label>{t("siteDiary.field.materials")}</Label>
                  <Textarea rows={2} value={createForm.materialsReceived} onChange={(e) => setCreateForm((f) => ({ ...f, materialsReceived: e.target.value }))} />
                </div>
              </div>
              <div>
                <Label>{t("siteDiary.field.incidents")}</Label>
                <Textarea rows={2} value={createForm.incidents} onChange={(e) => setCreateForm((f) => ({ ...f, incidents: e.target.value }))} />
              </div>
              <div>
                <Label>{t("siteDiary.field.note")}</Label>
                <Textarea rows={2} value={createForm.note} onChange={(e) => setCreateForm((f) => ({ ...f, note: e.target.value }))} />
              </div>
              {createError && <div className="text-sm text-rose-600">{createError}</div>}
            </div>
            <DialogFooter>
              <Button variant="ghost" onClick={() => setCreating(false)} disabled={saving}>{t("common.cancel")}</Button>
              <Button onClick={submitCreate} disabled={saving} data-testid="diary-new-save">{saving ? t("common.saving") : t("common.save")}</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* detail sheet */}
        <Sheet open={detail !== null} onOpenChange={(open) => { if (!open) closeDetail(); }}>
          <SheetContent side="right" className="w-full max-w-2xl overflow-y-auto sm:max-w-2xl">
            {detail && detailForm && (
              <>
                <SheetHeader>
                  <SheetTitle>{formatDate(detail.diaryDate, lang)} — {detail.designProjectCode}</SheetTitle>
                  <SheetDescription className="text-xs text-slate-500">{detail.designProjectName}</SheetDescription>
                </SheetHeader>
                <div className="mt-4 space-y-4">
                  <div className="flex flex-wrap items-center gap-2">{renderStatusBadge(detail)}</div>

                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label>{t("siteDiary.field.date")}</Label>
                      <Input type="date" value={detailForm.diaryDate} onChange={(e) => setDetailForm((f) => f && { ...f, diaryDate: e.target.value })} disabled={!canEditDetail} />
                    </div>
                    <div>
                      <Label>{t("siteDiary.field.weather")}</Label>
                      <Select value={detailForm.weatherCode} onValueChange={(v) => setDetailForm((f) => f && { ...f, weatherCode: v })} disabled={!canEditDetail}>
                        <SelectTrigger><SelectValue /></SelectTrigger>
                        <SelectContent>
                          {weatherOptions.map((o) => (<SelectItem key={o.code} value={o.code}>{o.name}</SelectItem>))}
                        </SelectContent>
                      </Select>
                    </div>
                  </div>
                  <div>
                    <Label>{t("siteDiary.field.weatherNote")}</Label>
                    <Input value={detailForm.weatherNote} onChange={(e) => setDetailForm((f) => f && { ...f, weatherNote: e.target.value })} disabled={!canEditDetail} />
                  </div>
                  <div className="grid grid-cols-4 gap-2">
                    <HeadcountField label={t("siteDiary.field.headcountLabor")} value={detailForm.headcountLabor} onChange={(n) => setDetailForm((f) => f && { ...f, headcountLabor: n })} disabled={!canEditDetail} />
                    <HeadcountField label={t("siteDiary.field.headcountEngineers")} value={detailForm.headcountEngineers} onChange={(n) => setDetailForm((f) => f && { ...f, headcountEngineers: n })} disabled={!canEditDetail} />
                    <HeadcountField label={t("siteDiary.field.headcountSupervisors")} value={detailForm.headcountSupervisors} onChange={(n) => setDetailForm((f) => f && { ...f, headcountSupervisors: n })} disabled={!canEditDetail} />
                    <HeadcountField label={t("siteDiary.field.headcountSubcontractors")} value={detailForm.headcountSubcontractors} onChange={(n) => setDetailForm((f) => f && { ...f, headcountSubcontractors: n })} disabled={!canEditDetail} />
                  </div>
                  <div>
                    <Label>{t("siteDiary.field.workPerformed")}</Label>
                    <Textarea rows={4} value={detailForm.workPerformed} onChange={(e) => setDetailForm((f) => f && { ...f, workPerformed: e.target.value })} disabled={!canEditDetail} />
                  </div>
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <Label>{t("siteDiary.field.machines")}</Label>
                      <Textarea rows={2} value={detailForm.machinesSummary} onChange={(e) => setDetailForm((f) => f && { ...f, machinesSummary: e.target.value })} disabled={!canEditDetail} />
                    </div>
                    <div>
                      <Label>{t("siteDiary.field.materials")}</Label>
                      <Textarea rows={2} value={detailForm.materialsReceived} onChange={(e) => setDetailForm((f) => f && { ...f, materialsReceived: e.target.value })} disabled={!canEditDetail} />
                    </div>
                  </div>
                  <div>
                    <Label>{t("siteDiary.field.incidents")}</Label>
                    <Textarea rows={2} value={detailForm.incidents} onChange={(e) => setDetailForm((f) => f && { ...f, incidents: e.target.value })} disabled={!canEditDetail} />
                  </div>
                  <div>
                    <Label>{t("siteDiary.field.note")}</Label>
                    <Textarea rows={2} value={detailForm.note} onChange={(e) => setDetailForm((f) => f && { ...f, note: e.target.value })} disabled={!canEditDetail} />
                  </div>

                  {detail.status !== "Draft" && (
                    <div className="rounded-md border border-slate-200 bg-slate-50 p-3 text-xs text-slate-600">
                      {detail.submittedByName && (
                        <div>
                          <span className="font-semibold">{t("siteDiary.field.submittedBy")}:</span> {detail.submittedByName} · {formatDateTime(detail.submittedAt, lang)}
                        </div>
                      )}
                      {detail.confirmedByName && (
                        <div className="mt-1">
                          <span className="font-semibold">{t("siteDiary.field.confirmedBy")}:</span> {detail.confirmedByName} · {formatDateTime(detail.confirmedAt, lang)}
                        </div>
                      )}
                    </div>
                  )}

                  {detailError && <div className="text-sm text-rose-600">{detailError}</div>}

                  <div className="flex flex-wrap items-center justify-between gap-2 pt-2">
                    <div className="flex flex-wrap gap-2">
                      {canManage && detail.status === "Draft" && (
                        <Button variant="destructive" size="sm" onClick={() => setConfirmAction({ kind: "delete", row: detail })} data-testid="diary-detail-delete">
                          <Trash2 className="mr-2 h-4 w-4" />
                          {t("siteDiary.action.delete")}
                        </Button>
                      )}
                      {canManage && detail.status === "Draft" && (
                        <Button variant="outline" size="sm" onClick={() => setConfirmAction({ kind: "submit", row: detail })} data-testid="diary-submit">
                          <Send className="mr-2 h-4 w-4" />
                          {t("siteDiary.action.submit")}
                        </Button>
                      )}
                      {canConfirm && detail.status === "Submitted" && (
                        <Button variant="outline" size="sm" onClick={() => setConfirmAction({ kind: "confirm", row: detail })} data-testid="diary-confirm">
                          <CheckCircle2 className="mr-2 h-4 w-4" />
                          {t("siteDiary.action.confirm")}
                        </Button>
                      )}
                      {canConfirm && detail.status !== "Draft" && (
                        <Button variant="outline" size="sm" onClick={() => setConfirmAction({ kind: "reopen", row: detail })} data-testid="diary-reopen">
                          {t("siteDiary.action.reopen")}
                        </Button>
                      )}
                    </div>
                    <div className="flex gap-2">
                      <Button variant="ghost" onClick={closeDetail} disabled={detailSaving}>{t("common.close")}</Button>
                      {canEditDetail && (
                        <Button onClick={submitDetail} disabled={detailSaving} data-testid="diary-detail-save">
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
              <AlertDialogDescription>{renderConfirmMessage()}</AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={actionBusy}>{t("common.cancel")}</AlertDialogCancel>
              <AlertDialogAction
                onClick={runAction}
                disabled={actionBusy}
                data-testid="diary-action-confirm"
              >
                {actionBusy ? t("common.saving") : t("common.confirm")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* bulk delete confirm */}
        <AlertDialog open={confirmBulkDelete} onOpenChange={setConfirmBulkDelete}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>{t("siteDiary.action.bulkDelete")}</AlertDialogTitle>
              <AlertDialogDescription>
                {t("siteDiary.confirmBulkDelete").replace("{count}", String(selected.size))}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={bulkDeleting}>{t("common.cancel")}</AlertDialogCancel>
              <AlertDialogAction onClick={submitBulkDelete} disabled={bulkDeleting} data-testid="diary-bulk-delete-confirm">
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

interface HeadcountFieldProps {
  label: string;
  value: number;
  onChange: (n: number) => void;
  disabled?: boolean;
}

const HeadcountField = ({ label, value, onChange, disabled }: HeadcountFieldProps) => (
  <div>
    <Label className="text-xs text-slate-500">{label}</Label>
    <Input
      type="number"
      min={0}
      value={value}
      onChange={(e) => onChange(Math.max(0, Number(e.target.value) || 0))}
      disabled={disabled}
      className="tabular-nums"
    />
  </div>
);

export default AdminSiteDiary;
