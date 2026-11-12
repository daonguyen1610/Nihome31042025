import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { useRequestGuard } from "@/hooks/useRequestGuard";
import StatTile from "./ConstructionStatTile";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { FieldScope } from "@/components/ui/field-scope";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  CircleDot,
  ClipboardList,
  CloudSun,
  Eye,
  Pencil,
  RefreshCcw,
  Search,
  Send,
  Trash2,
  Users,
} from "lucide-react";
import { type DesignProjectScopeProps } from "./constructionScope";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { ListPagination } from "@/components/admin/ListPagination";
import { PageEmpty, PageLoading, PageError } from "@/components/PageState";
import { ConstructionStatusBadge } from "./constructionUi";
import { DIARY_STATUS_TONE } from "./constructionStatus";
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
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import {
  adminApi,
  type CreateSiteDiaryRequest,
  type MasterDataOption,
  type SiteDiaryListParams,
  type SiteDiaryResponse,
  type SiteDiaryStatus,
  type UpdateSiteDiaryRequest,
} from "@/services/adminApi";
import {
  formatDate as formatDateInZone,
  formatDateTime as formatDateTimeInZone,
} from "@/lib/displaySettings";

const DIARY_STATUSES: SiteDiaryStatus[] = ["Draft", "Submitted", "Confirmed"];

type ConfirmKind = "submit" | "confirm" | "reopen" | "delete" | null;

const todayIso = () => new Date().toISOString().slice(0, 10);

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return formatDateInZone(iso, lang);
  } catch {
    return iso;
  }
};

const formatDateTime = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return formatDateTimeInZone(iso, lang);
  } catch {
    return iso;
  }
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

export const SiteDiaryPanel = ({ designProjectId, scope }: DesignProjectScopeProps) => {
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.constructionDiaryManage);
  const canConfirm = scope === "review" && has(ADMIN_PERMS.constructionDiaryConfirm);

  // list state
  const [rows, setRows] = useState<SiteDiaryResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<SiteDiaryStatus, number>>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const projectId = designProjectId;
  const [status, setStatus] = useState<SiteDiaryStatus | "">("");
  const [weatherCode, setWeatherCode] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [search, setSearch] = useState("");
  const settledSearch = useDebouncedValue(search);
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
  const [detailMode, setDetailMode] = useState<"view" | "edit">("view");
  const [detailForm, setDetailForm] = useState<DiaryForm | null>(null);
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const [confirmAction, setConfirmAction] = useState<{
    kind: ConfirmKind;
    row: SiteDiaryResponse | null;
  }>({
    kind: null,
    row: null,
  });
  const [actionBusy, setActionBusy] = useState(false);

  // lookups
  const [weatherOptions, setWeatherOptions] = useState<MasterDataOption[]>([]);

  useEffect(() => {
    let cancelled = false;
    void adminApi
      .getMasterDataOptions("diary_weather", lang)
      .then(({ data }) => {
        if (!cancelled) setWeatherOptions((data ?? []).filter((option) => option.isActive));
      })
      .catch(() => {
        // non-fatal
      });
    return () => {
      cancelled = true;
    };
  }, [lang]);

  const startListRequest = useRequestGuard();
  const fetchList = useCallback(async () => {
    const isCurrent = startListRequest();
    setLoading(true);
    setError(null);
    try {
      const params: SiteDiaryListParams = { page, pageSize };
      if (projectId != null) params.designProjectId = projectId;
      if (status) params.status = status;
      if (weatherCode) params.weatherCode = weatherCode;
      if (dateFrom) params.dateFrom = dateFrom;
      if (dateTo) params.dateTo = dateTo;
      if (settledSearch.trim()) params.search = settledSearch.trim();
      const { data } = await adminApi.listSiteDiaries(params);
      if (!isCurrent()) return;
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
      setStatusCounts(data.statusCounts ?? {});
      const visible = new Set((data.items ?? []).map((r) => r.id));
      setSelected((prev) => {
        const next = new Set<number>();
        prev.forEach((id) => {
          if (visible.has(id)) next.add(id);
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
    weatherCode,
    dateFrom,
    dateTo,
    settledSearch,
    uiError,
    toast,
    t,
  ]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const statusPill = (s: SiteDiaryStatus) => statusCounts[s] ?? 0;

  const weatherSelectOptions = useMemo(
    () => [
      { value: "__all__", label: t("siteDiary.filter.allWeather") },
      ...weatherOptions.map((o) => ({ value: o.code, label: o.name })),
    ],
    [weatherOptions, t],
  );

  const hasActiveFilter =
    status !== "" || weatherCode !== "" || dateFrom !== "" || dateTo !== "" || search.trim() !== "";

  const resetFilters = () => {
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
    if (createProjectId == null) {
      setCreateError(t("siteDiary.form.projectRequired"));
      return;
    }
    if (!createForm.weatherCode) {
      setCreateError(t("siteDiary.form.weatherRequired"));
      return;
    }
    if (!createForm.workPerformed.trim()) {
      setCreateError(t("siteDiary.form.workRequired"));
      return;
    }
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
      setCreateError(uiError(err));
    } finally {
      setSaving(false);
    }
  };

  // ---- detail ----
  const openDetail = (row: SiteDiaryResponse, mode: "view" | "edit" = "view") => {
    setDetail(row);
    setDetailMode(mode);
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

  const canEditDetail = canManage && detailMode === "edit" && detail?.status === "Draft";

  const submitDetail = async () => {
    if (!detail || !detailForm) return;
    setDetailError(null);
    if (!detailForm.weatherCode) {
      setDetailError(t("siteDiary.form.weatherRequired"));
      return;
    }
    if (!detailForm.workPerformed.trim()) {
      setDetailError(t("siteDiary.form.workRequired"));
      return;
    }
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
      setDetailError(uiError(err));
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
      toast({
        title: t("common.error"),
        description: uiError(err),
        variant: "destructive",
      });
    } finally {
      setActionBusy(false);
    }
  };

  // ---- bulk delete ----
  const toggleSelect = (id: number, checked: boolean) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  };
  const toggleSelectAll = (checked: boolean) => {
    if (checked) setSelected(new Set(rows.map((row) => row.id)));
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

  // ---- render helpers ----
  const renderStatusBadge = (row: SiteDiaryResponse) => (
    <ConstructionStatusBadge tone={DIARY_STATUS_TONE[row.status]}>
      {t(`siteDiary.status.${row.status}`)}
    </ConstructionStatusBadge>
  );

  const renderConfirmMessage = () => {
    const { kind, row } = confirmAction;
    if (!kind || !row) return "";
    const date = formatDate(row.diaryDate, lang);
    switch (kind) {
      case "submit":
        return t("siteDiary.confirmSubmit").replace("{date}", date);
      case "confirm":
        return t("siteDiary.confirmConfirm").replace("{date}", date);
      case "reopen":
        return t("siteDiary.confirmReopen").replace("{date}", date);
      case "delete":
        return t("siteDiary.confirmDelete").replace("{date}", date);
      default:
        return "";
    }
  };
  const confirmTitle = () => {
    switch (confirmAction.kind) {
      case "submit":
        return t("siteDiary.action.submit");
      case "confirm":
        return t("siteDiary.action.confirm");
      case "reopen":
        return t("siteDiary.action.reopen");
      case "delete":
        return t("siteDiary.action.delete");
      default:
        return "";
    }
  };

  return (
    <>
      <div className="space-y-6" data-testid="site-diary-page">
        <div className="flex justify-end">
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
          <StatTile
            icon={<ClipboardList className="h-6 w-6" />}
            label={t("siteDiary.stat.total")}
            value={total}
          />
          <StatTile
            icon={<CircleDot className="h-6 w-6" />}
            label={t("siteDiary.stat.draft")}
            value={statusPill("Draft")}
          />
          <StatTile
            icon={<Send className="h-6 w-6" />}
            label={t("siteDiary.stat.submitted")}
            value={statusPill("Submitted")}
          />
          <StatTile
            icon={<CheckCircle2 className="h-6 w-6" />}
            label={t("siteDiary.stat.confirmed")}
            value={statusPill("Confirmed")}
          />
        </div>

        {/* filters */}
        <div className="rounded-lg border border-border bg-card p-4 shadow-sm">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 [&>div]:min-w-0">
            <FieldScope>
              <Label className="text-xs text-muted-foreground">{t("siteDiary.field.status")}</Label>
              <Select
                value={status || "__all__"}
                onValueChange={(v) => {
                  setStatus(v === "__all__" ? "" : (v as SiteDiaryStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t("siteDiary.filter.allStatuses")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("siteDiary.filter.allStatuses")}</SelectItem>
                  {DIARY_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`siteDiary.status.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label className="text-xs text-muted-foreground">
                {t("siteDiary.field.weather")}
              </Label>
              <Select
                value={weatherCode || "__all__"}
                onValueChange={(v) => {
                  setWeatherCode(v === "__all__" ? "" : v);
                  setPage(1);
                }}
              >
                <SelectTrigger>
                  <SelectValue placeholder={t("siteDiary.filter.allWeather")} />
                </SelectTrigger>
                <SelectContent>
                  {weatherSelectOptions.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </FieldScope>
            <FieldScope>
              <Label className="text-xs text-muted-foreground">
                {t("siteDiary.filter.search")}
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
                  placeholder={t("siteDiary.filter.search")}
                  data-testid="diary-search"
                />
              </div>
            </FieldScope>
          </div>
          <div className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4 [&>div]:min-w-0">
            <FieldScope>
              <Label className="text-xs text-muted-foreground">
                {t("siteDiary.filter.dateFrom")}
              </Label>
              <Input
                type="date"
                value={dateFrom}
                onChange={(e) => {
                  setDateFrom(e.target.value);
                  setPage(1);
                }}
              />
            </FieldScope>
            <FieldScope>
              <Label className="text-xs text-muted-foreground">
                {t("siteDiary.filter.dateTo")}
              </Label>
              <Input
                type="date"
                value={dateTo}
                onChange={(e) => {
                  setDateTo(e.target.value);
                  setPage(1);
                }}
              />
            </FieldScope>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-3">
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
                className="ml-auto"
                data-testid="diary-bulk-delete"
              >
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
          <PageEmpty
            message={t("siteDiary.empty")}
            action={
              canManage && (
                <Button size="sm" onClick={openCreate} data-testid="diary-empty-new">
                  <ClipboardList className="mr-2 h-4 w-4" />
                  {t("siteDiary.new")}
                </Button>
              )
            }
          />
        ) : (
          <>
            {/* Mobile cards (< md) */}
            <div className="space-y-3 md:hidden">
              {rows.map((row) => (
                <article
                  key={`m-${row.id}`}
                  className="rounded-xl border border-border bg-card p-4 shadow-sm"
                >
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0 flex-1">
                      <div className="text-xs text-muted-foreground">{row.designProjectCode}</div>
                      <div className="text-base font-semibold text-foreground">
                        {formatDate(row.diaryDate, lang)}
                      </div>
                    </div>
                    {canManage ? (
                      <div>
                        <Checkbox
                          checked={selected.has(row.id)}
                          onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                          aria-label={`m-select-${row.id}`}
                          className="mt-1"
                        />
                      </div>
                    ) : null}
                  </div>
                  <div className="mt-2 flex flex-wrap items-center gap-2">
                    {renderStatusBadge(row)}
                    <span className="inline-flex items-center gap-1 rounded bg-info-soft px-1.5 py-0.5 text-xs text-info-strong">
                      <CloudSun className="h-3 w-3" />
                      {row.weatherLabel ?? row.weatherCode}
                    </span>
                    <span className="inline-flex items-center gap-1 rounded bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
                      <Users className="h-3 w-3" />
                      {row.headcountTotal}
                    </span>
                  </div>
                  <div className="mt-2 line-clamp-3 text-sm text-foreground">
                    {row.workPerformed}
                  </div>
                  {row.incidents && (
                    <div className="mt-1 flex items-start gap-1 text-xs text-warning-strong">
                      <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" />
                      <span className="line-clamp-2">{row.incidents}</span>
                    </div>
                  )}
                  <div className="mt-3 flex flex-wrap justify-end gap-1 border-t pt-2">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => openDetail(row, "view")}
                      data-testid={`diary-view-${row.id}`}
                    >
                      <Eye className="mr-1 h-4 w-4" />
                      {t("common.view")}
                    </Button>
                    {canManage ? (
                      <>
                        <span
                          title={
                            row.status !== "Draft" ? t("siteDiary.action.draftOnly") : undefined
                          }
                        >
                          <Button
                            variant="ghost"
                            size="sm"
                            disabled={row.status !== "Draft"}
                            onClick={() => openDetail(row, "edit")}
                          >
                            <Pencil className="mr-1 h-4 w-4" />
                            {t("common.edit")}
                          </Button>
                        </span>
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-destructive hover:text-destructive"
                          onClick={() => setConfirmAction({ kind: "delete", row })}
                        >
                          <Trash2 className="mr-1 h-4 w-4" />
                          {t("common.delete")}
                        </Button>
                      </>
                    ) : null}
                  </div>
                </article>
              ))}
            </div>

            {/* Desktop table (md+) */}
            <div className="hidden overflow-hidden rounded-lg border border-border bg-card shadow-sm md:block">
              <div className="max-h-[70vh] overflow-auto">
                <table className="min-w-full divide-y divide-border text-sm">
                  <thead className="sticky top-0 z-10 bg-muted/30 text-left text-xs uppercase tracking-wide text-muted-foreground">
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
                      <th className="px-3 py-2 text-right">{t("common.actions")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border">
                    {rows.map((row) => (
                      <tr
                        key={row.id}
                        className="hover:bg-muted/30"
                        data-testid={`diary-row-${row.id}`}
                      >
                        {canManage && (
                          <td className="px-3 py-2">
                            <Checkbox
                              checked={selected.has(row.id)}
                              onCheckedChange={(v) => toggleSelect(row.id, !!v)}
                              aria-label={`select-${row.id}`}
                            />
                          </td>
                        )}
                        <td className="px-3 py-2 whitespace-nowrap font-medium text-foreground">
                          {formatDate(row.diaryDate, lang)}
                        </td>
                        <td className="px-3 py-2 text-xs text-muted-foreground">
                          <div className="font-mono">{row.designProjectCode}</div>
                          <div className="truncate max-w-[220px] text-muted-foreground">
                            {row.designProjectName}
                          </div>
                        </td>
                        <td className="px-3 py-2 text-xs text-foreground">
                          <div className="inline-flex items-center gap-1">
                            <CloudSun className="h-3.5 w-3.5 text-info-strong" />
                            {row.weatherLabel ?? row.weatherCode}
                          </div>
                          {row.weatherNote && (
                            <div className="text-[11px] text-muted-foreground">
                              {row.weatherNote}
                            </div>
                          )}
                        </td>
                        <td className="px-3 py-2 text-sm tabular-nums text-foreground">
                          {row.headcountTotal}
                        </td>
                        <td className="px-3 py-2">
                          <div className="line-clamp-2 max-w-[420px] text-sm text-foreground">
                            {row.workPerformed}
                          </div>
                          {row.incidents && (
                            <div className="mt-0.5 flex items-start gap-1 text-[11px] text-warning-strong">
                              <AlertTriangle className="mt-0.5 h-3 w-3 shrink-0" />
                              <span className="line-clamp-1">{row.incidents}</span>
                            </div>
                          )}
                        </td>
                        <td className="px-3 py-2">{renderStatusBadge(row)}</td>
                        <td className="px-3 py-2">
                          <div className="flex justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              title={t("common.view")}
                              aria-label={t("common.view")}
                              onClick={() => openDetail(row, "view")}
                              data-testid={`diary-view-${row.id}`}
                            >
                              <Eye className="h-4 w-4" />
                            </Button>
                            {canManage ? (
                              <>
                                <span
                                  title={
                                    row.status !== "Draft"
                                      ? t("siteDiary.action.draftOnly")
                                      : undefined
                                  }
                                >
                                  <Button
                                    variant="ghost"
                                    size="icon"
                                    disabled={row.status !== "Draft"}
                                    aria-label={
                                      row.status === "Draft"
                                        ? t("common.edit")
                                        : t("siteDiary.action.draftOnly")
                                    }
                                    onClick={() => openDetail(row, "edit")}
                                  >
                                    <Pencil className="h-4 w-4" />
                                  </Button>
                                </span>
                                <Button
                                  variant="ghost"
                                  size="icon"
                                  title={t("common.delete")}
                                  aria-label={t("common.delete")}
                                  className="text-destructive hover:text-destructive"
                                  onClick={() => setConfirmAction({ kind: "delete", row })}
                                >
                                  <Trash2 className="h-4 w-4" />
                                </Button>
                              </>
                            ) : null}
                          </div>
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
                <FieldScope>
                  <Label>{t("siteDiary.field.date")}</Label>
                  <Input
                    type="date"
                    value={createForm.diaryDate}
                    onChange={(e) => setCreateForm((f) => ({ ...f, diaryDate: e.target.value }))}
                  />
                </FieldScope>
              </div>
              <div className="grid grid-cols-2 gap-3">
                <FieldScope>
                  <Label>{t("siteDiary.field.weather")}</Label>
                  <Select
                    value={createForm.weatherCode}
                    onValueChange={(v) => setCreateForm((f) => ({ ...f, weatherCode: v }))}
                  >
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {weatherOptions.map((o) => (
                        <SelectItem key={o.code} value={o.code}>
                          {o.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </FieldScope>
                <FieldScope>
                  <Label>{t("siteDiary.field.weatherNote")}</Label>
                  <Input
                    value={createForm.weatherNote}
                    onChange={(e) => setCreateForm((f) => ({ ...f, weatherNote: e.target.value }))}
                  />
                </FieldScope>
              </div>
              <div className="grid grid-cols-4 gap-2">
                <HeadcountField
                  label={t("siteDiary.field.headcountLabor")}
                  value={createForm.headcountLabor}
                  onChange={(n) => setCreateForm((f) => ({ ...f, headcountLabor: n }))}
                />
                <HeadcountField
                  label={t("siteDiary.field.headcountEngineers")}
                  value={createForm.headcountEngineers}
                  onChange={(n) => setCreateForm((f) => ({ ...f, headcountEngineers: n }))}
                />
                <HeadcountField
                  label={t("siteDiary.field.headcountSupervisors")}
                  value={createForm.headcountSupervisors}
                  onChange={(n) => setCreateForm((f) => ({ ...f, headcountSupervisors: n }))}
                />
                <HeadcountField
                  label={t("siteDiary.field.headcountSubcontractors")}
                  value={createForm.headcountSubcontractors}
                  onChange={(n) => setCreateForm((f) => ({ ...f, headcountSubcontractors: n }))}
                />
              </div>
              <FieldScope>
                <Label>{t("siteDiary.field.workPerformed")}</Label>
                <Textarea
                  rows={3}
                  value={createForm.workPerformed}
                  onChange={(e) => setCreateForm((f) => ({ ...f, workPerformed: e.target.value }))}
                  data-testid="diary-new-work"
                />
              </FieldScope>
              <div className="grid grid-cols-2 gap-3">
                <FieldScope>
                  <Label>{t("siteDiary.field.machines")}</Label>
                  <Textarea
                    rows={2}
                    value={createForm.machinesSummary}
                    onChange={(e) =>
                      setCreateForm((f) => ({ ...f, machinesSummary: e.target.value }))
                    }
                  />
                </FieldScope>
                <FieldScope>
                  <Label>{t("siteDiary.field.materials")}</Label>
                  <Textarea
                    rows={2}
                    value={createForm.materialsReceived}
                    onChange={(e) =>
                      setCreateForm((f) => ({ ...f, materialsReceived: e.target.value }))
                    }
                  />
                </FieldScope>
              </div>
              <FieldScope>
                <Label>{t("siteDiary.field.incidents")}</Label>
                <Textarea
                  rows={2}
                  value={createForm.incidents}
                  onChange={(e) => setCreateForm((f) => ({ ...f, incidents: e.target.value }))}
                />
              </FieldScope>
              <FieldScope>
                <Label>{t("siteDiary.field.note")}</Label>
                <Textarea
                  rows={2}
                  value={createForm.note}
                  onChange={(e) => setCreateForm((f) => ({ ...f, note: e.target.value }))}
                />
              </FieldScope>
              {createError && <div className="text-sm text-destructive">{createError}</div>}
            </div>
            <DialogFooter>
              <Button variant="ghost" onClick={() => setCreating(false)} disabled={saving}>
                {t("common.cancel")}
              </Button>
              <Button onClick={submitCreate} disabled={saving} data-testid="diary-new-save">
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
          <SheetContent side="right" className="w-full max-w-2xl overflow-y-auto sm:max-w-2xl">
            {detail && detailForm && (
              <>
                <SheetHeader>
                  <SheetTitle>
                    {t(detailMode === "edit" ? "siteDiary.edit" : "siteDiary.view")} ·{" "}
                    {formatDate(detail.diaryDate, lang)} — {detail.designProjectCode}
                  </SheetTitle>
                  <SheetDescription className="text-xs text-muted-foreground">
                    {detail.designProjectName}
                  </SheetDescription>
                </SheetHeader>
                <div className="mt-4 space-y-4">
                  <div className="flex flex-wrap items-center gap-2">
                    {renderStatusBadge(detail)}
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <FieldScope>
                      <Label>{t("siteDiary.field.date")}</Label>
                      <Input
                        type="date"
                        value={detailForm.diaryDate}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, diaryDate: e.target.value })
                        }
                        disabled={!canEditDetail}
                      />
                    </FieldScope>
                    <FieldScope>
                      <Label>{t("siteDiary.field.weather")}</Label>
                      <Select
                        value={detailForm.weatherCode}
                        onValueChange={(v) => setDetailForm((f) => f && { ...f, weatherCode: v })}
                        disabled={!canEditDetail}
                      >
                        <SelectTrigger>
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {weatherOptions.map((o) => (
                            <SelectItem key={o.code} value={o.code}>
                              {o.name}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </FieldScope>
                  </div>
                  <FieldScope>
                    <Label>{t("siteDiary.field.weatherNote")}</Label>
                    <Input
                      value={detailForm.weatherNote}
                      onChange={(e) =>
                        setDetailForm((f) => f && { ...f, weatherNote: e.target.value })
                      }
                      disabled={!canEditDetail}
                    />
                  </FieldScope>
                  <div className="grid grid-cols-4 gap-2">
                    <HeadcountField
                      label={t("siteDiary.field.headcountLabor")}
                      value={detailForm.headcountLabor}
                      onChange={(n) => setDetailForm((f) => f && { ...f, headcountLabor: n })}
                      disabled={!canEditDetail}
                    />
                    <HeadcountField
                      label={t("siteDiary.field.headcountEngineers")}
                      value={detailForm.headcountEngineers}
                      onChange={(n) => setDetailForm((f) => f && { ...f, headcountEngineers: n })}
                      disabled={!canEditDetail}
                    />
                    <HeadcountField
                      label={t("siteDiary.field.headcountSupervisors")}
                      value={detailForm.headcountSupervisors}
                      onChange={(n) => setDetailForm((f) => f && { ...f, headcountSupervisors: n })}
                      disabled={!canEditDetail}
                    />
                    <HeadcountField
                      label={t("siteDiary.field.headcountSubcontractors")}
                      value={detailForm.headcountSubcontractors}
                      onChange={(n) =>
                        setDetailForm((f) => f && { ...f, headcountSubcontractors: n })
                      }
                      disabled={!canEditDetail}
                    />
                  </div>
                  <FieldScope>
                    <Label>{t("siteDiary.field.workPerformed")}</Label>
                    <Textarea
                      rows={4}
                      value={detailForm.workPerformed}
                      onChange={(e) =>
                        setDetailForm((f) => f && { ...f, workPerformed: e.target.value })
                      }
                      disabled={!canEditDetail}
                    />
                  </FieldScope>
                  <div className="grid grid-cols-2 gap-3">
                    <FieldScope>
                      <Label>{t("siteDiary.field.machines")}</Label>
                      <Textarea
                        rows={2}
                        value={detailForm.machinesSummary}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, machinesSummary: e.target.value })
                        }
                        disabled={!canEditDetail}
                      />
                    </FieldScope>
                    <FieldScope>
                      <Label>{t("siteDiary.field.materials")}</Label>
                      <Textarea
                        rows={2}
                        value={detailForm.materialsReceived}
                        onChange={(e) =>
                          setDetailForm((f) => f && { ...f, materialsReceived: e.target.value })
                        }
                        disabled={!canEditDetail}
                      />
                    </FieldScope>
                  </div>
                  <FieldScope>
                    <Label>{t("siteDiary.field.incidents")}</Label>
                    <Textarea
                      rows={2}
                      value={detailForm.incidents}
                      onChange={(e) =>
                        setDetailForm((f) => f && { ...f, incidents: e.target.value })
                      }
                      disabled={!canEditDetail}
                    />
                  </FieldScope>
                  <FieldScope>
                    <Label>{t("siteDiary.field.note")}</Label>
                    <Textarea
                      rows={2}
                      value={detailForm.note}
                      onChange={(e) => setDetailForm((f) => f && { ...f, note: e.target.value })}
                      disabled={!canEditDetail}
                    />
                  </FieldScope>

                  {detail.status !== "Draft" && (
                    <div className="rounded-md border border-border bg-muted/30 p-3 text-xs text-muted-foreground">
                      <p className="mb-2 font-medium text-foreground">
                        {t("siteDiary.action.draftOnly")}
                      </p>
                      {detail.submittedByName && (
                        <div>
                          <span className="font-semibold">{t("siteDiary.field.submittedBy")}:</span>{" "}
                          {detail.submittedByName} · {formatDateTime(detail.submittedAt, lang)}
                        </div>
                      )}
                      {detail.confirmedByName && (
                        <div className="mt-1">
                          <span className="font-semibold">{t("siteDiary.field.confirmedBy")}:</span>{" "}
                          {detail.confirmedByName} · {formatDateTime(detail.confirmedAt, lang)}
                        </div>
                      )}
                    </div>
                  )}

                  {detailError && <div className="text-sm text-destructive">{detailError}</div>}

                  <div className="flex flex-wrap items-center justify-between gap-2 pt-2">
                    <div className="flex flex-wrap gap-2">
                      {canManage ? (
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => setConfirmAction({ kind: "delete", row: detail })}
                          data-testid="diary-detail-delete"
                        >
                          <Trash2 className="mr-2 h-4 w-4" />
                          {t("siteDiary.action.delete")}
                        </Button>
                      ) : null}
                      {canManage && detail.status === "Draft" && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setConfirmAction({ kind: "submit", row: detail })}
                          data-testid="diary-submit"
                        >
                          <Send className="mr-2 h-4 w-4" />
                          {t("siteDiary.action.submit")}
                        </Button>
                      )}
                      {canConfirm && detail.status === "Submitted" && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setConfirmAction({ kind: "confirm", row: detail })}
                          data-testid="diary-confirm"
                        >
                          <CheckCircle2 className="mr-2 h-4 w-4" />
                          {t("siteDiary.action.confirm")}
                        </Button>
                      )}
                      {canConfirm && detail.status !== "Draft" && (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setConfirmAction({ kind: "reopen", row: detail })}
                          data-testid="diary-reopen"
                        >
                          {t("siteDiary.action.reopen")}
                        </Button>
                      )}
                    </div>
                    <div className="flex gap-2">
                      <Button variant="ghost" onClick={closeDetail} disabled={detailSaving}>
                        {t("common.close")}
                      </Button>
                      {canManage && detailMode === "view" ? (
                        <span
                          title={
                            detail.status !== "Draft" ? t("siteDiary.action.draftOnly") : undefined
                          }
                        >
                          <Button
                            variant="outline"
                            disabled={detail.status !== "Draft"}
                            onClick={() => setDetailMode("edit")}
                          >
                            <Pencil className="mr-1 h-4 w-4" />
                            {t("common.edit")}
                          </Button>
                        </span>
                      ) : null}
                      {canEditDetail && (
                        <Button
                          onClick={submitDetail}
                          disabled={detailSaving}
                          data-testid="diary-detail-save"
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

        {/* single-action confirm */}
        <AlertDialog
          open={confirmAction.kind !== null}
          onOpenChange={(v) => !v && setConfirmAction({ kind: null, row: null })}
        >
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
                className={
                  confirmAction.kind === "delete"
                    ? "bg-destructive text-destructive-foreground hover:bg-destructive/90"
                    : undefined
                }
              >
                {actionBusy
                  ? t("common.saving")
                  : t(confirmAction.kind === "delete" ? "common.delete" : "common.confirm")}
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
              <AlertDialogAction
                onClick={submitBulkDelete}
                disabled={bulkDeleting}
                data-testid="diary-bulk-delete-confirm"
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              >
                {bulkDeleting ? t("common.saving") : t("common.delete")}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </>
  );
};

interface HeadcountFieldProps {
  label: string;
  value: number;
  onChange: (n: number) => void;
  disabled?: boolean;
}

const HeadcountField = ({ label, value, onChange, disabled }: HeadcountFieldProps) => (
  <FieldScope>
    <Label className="text-xs text-muted-foreground">{label}</Label>
    <Input
      type="number"
      min={0}
      value={value}
      onChange={(e) => onChange(Math.max(0, Number(e.target.value) || 0))}
      disabled={disabled}
      className="tabular-nums"
    />
  </FieldScope>
);
