import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { AlertTriangle, Cloud, CloudOff, Loader2, RefreshCcw, Search } from "lucide-react";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SearchableSelect } from "@/components/ui/searchable-select";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import {
  adminApi,
  SURVEY_DRIVE_STATUSES,
  type MasterDataOption,
  type SurveyDriveSyncStatus,
  type SurveyListItemResponse,
  type SurveyListParams,
} from "@/services/adminApi";
import { contentApi } from "@/services/contentApi";

// ------------------------------- helpers -------------------------------

const DRIVE_STATUS_BADGE: Record<SurveyDriveSyncStatus, string> = {
  NotSynced: "border-slate-200 bg-slate-50 text-slate-700",
  Syncing: "border-amber-200 bg-amber-50 text-amber-700",
  Synced: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Failed: "border-rose-200 bg-rose-50 text-rose-700",
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

interface UserOption {
  id: number;
  fullName: string;
}

interface ProjectOption {
  id: number;
  name: string;
}

/**
 * Compact badge for one Drive sync state. Renders the label + a hover
 * tooltip carrying the error reason when status is Failed.
 */
const DriveStatusBadge = ({ status, error }: { status: SurveyDriveSyncStatus; error?: string | null }) => {
  const { t } = useI18n();
  const Icon =
    status === "Synced"
      ? Cloud
      : status === "Syncing"
        ? Loader2
        : status === "Failed"
          ? AlertTriangle
          : CloudOff;

  const badge = (
    <Badge variant="outline" className={cn("gap-1", DRIVE_STATUS_BADGE[status])}>
      <Icon className={cn("h-3 w-3", status === "Syncing" && "animate-spin")} />
      {t(`surveys.driveStatus.${status}`)}
    </Badge>
  );

  if (status === "Failed" && error) {
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            <span>{badge}</span>
          </TooltipTrigger>
          <TooltipContent className="max-w-xs break-words text-xs">
            {error}
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    );
  }
  return badge;
};

// -------------------------------- page ---------------------------------

const AdminSurveys = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const navigate = useNavigate();
  const canPickSurveyor = has(ADMIN_PERMS.users);

  // list state
  const [rows, setRows] = useState<SurveyListItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const [search, setSearch] = useState("");
  const [constructionType, setConstructionType] = useState<string>("");
  const [surveyorUserId, setSurveyorUserId] = useState<number | null>(null);
  const [linkedProjectId, setLinkedProjectId] = useState<number | null>(null);
  const [driveStatus, setDriveStatus] = useState<SurveyDriveSyncStatus | "">("");
  const [dateFrom, setDateFrom] = useState<string>("");
  const [dateTo, setDateTo] = useState<string>("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // lookup options
  const [constructionTypes, setConstructionTypes] = useState<MasterDataOption[]>([]);
  const [users, setUsers] = useState<UserOption[]>([]);
  const [projects, setProjects] = useState<ProjectOption[]>([]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [ctResp, projResp] = await Promise.all([
          adminApi.getMasterDataOptions("construction_type"),
          contentApi.getProjects().catch(() => ({ data: [] })),
        ]);
        if (cancelled) return;
        setConstructionTypes((ctResp.data ?? []).filter((o) => o.isActive));
        // Projects endpoint is anonymous and returns full content rows;
        // we only need { id, name } for the filter dropdown.
        const projectRows = Array.isArray(projResp.data) ? projResp.data : [];
        setProjects(projectRows.map((p) => ({ id: p.id, name: p.name ?? `#${p.id}` })));

        if (canPickSurveyor) {
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
            // No permission or fetch failed — filter stays hidden.
          }
        }
      } catch {
        // Non-fatal: filters just fall back to empty lists.
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canPickSurveyor]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: SurveyListParams = {
        page,
        pageSize,
      };
      if (search.trim()) params.search = search.trim();
      if (constructionType) params.constructionTypeCode = constructionType;
      if (surveyorUserId != null) params.surveyorUserId = surveyorUserId;
      if (linkedProjectId != null) params.linkedProjectId = linkedProjectId;
      if (driveStatus) params.driveSyncStatus = driveStatus;
      if (dateFrom) params.dateFrom = dateFrom;
      if (dateTo) params.dateTo = dateTo;
      const { data } = await adminApi.listSurveys(params);
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
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
    constructionType,
    surveyorUserId,
    linkedProjectId,
    driveStatus,
    dateFrom,
    dateTo,
    t,
    toast,
  ]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const hasActiveFilter =
    search.trim().length > 0 ||
    constructionType !== "" ||
    surveyorUserId != null ||
    linkedProjectId != null ||
    driveStatus !== "" ||
    dateFrom !== "" ||
    dateTo !== "";

  const surveyorOptions = useMemo(
    () => users.map((u) => ({ value: String(u.id), label: u.fullName })),
    [users],
  );
  const projectOptions = useMemo(
    () => projects.map((p) => ({ value: String(p.id), label: p.name })),
    [projects],
  );

  const openDetail = (id: number) => navigate(`/admin/surveys/${id}`);

  const resetFilters = () => {
    setSearch("");
    setConstructionType("");
    setSurveyorUserId(null);
    setLinkedProjectId(null);
    setDriveStatus("");
    setDateFrom("");
    setDateTo("");
    setPage(1);
  };

  // ----------------------------- render -----------------------------

  return (
    <AdminLayout>
      <div className="space-y-4 p-3 md:p-4">
        <header className="flex flex-col gap-1">
          <h1 className="text-xl font-bold text-slate-900 md:text-2xl">
            {t("surveys.title")}
          </h1>
          <p className="text-sm text-slate-600">{t("surveys.subtitle")}</p>
        </header>

        <section className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
            <div className="lg:col-span-2">
              <Label className="text-xs">{t("surveys.filter.search")}</Label>
              <div className="relative mt-1">
                <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-slate-400" />
                <Input
                  className="pl-8"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder={t("surveys.filter.search")}
                />
              </div>
            </div>
            <div>
              <Label className="text-xs">{t("surveys.field.constructionType")}</Label>
              <Select
                value={constructionType}
                onValueChange={(v) => {
                  setConstructionType(v === "__all__" ? "" : v);
                  setPage(1);
                }}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("surveys.filter.allConstructionTypes")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("surveys.filter.allConstructionTypes")}</SelectItem>
                  {constructionTypes.map((c) => (
                    <SelectItem key={c.code} value={c.code}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label className="text-xs">{t("surveys.field.driveStatus")}</Label>
              <Select
                value={driveStatus}
                onValueChange={(v) => {
                  setDriveStatus(v === "__all__" ? "" : (v as SurveyDriveSyncStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("surveys.filter.allDriveStatuses")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("surveys.filter.allDriveStatuses")}</SelectItem>
                  {SURVEY_DRIVE_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`surveys.driveStatus.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {canPickSurveyor && surveyorOptions.length > 0 ? (
              <div>
                <Label className="text-xs">{t("surveys.field.surveyor")}</Label>
                <SearchableSelect
                  className="mt-1"
                  value={surveyorUserId != null ? String(surveyorUserId) : ""}
                  onChange={(v) => {
                    setSurveyorUserId(v ? Number(v) : null);
                    setPage(1);
                  }}
                  options={[{ value: "", label: t("surveys.filter.allSurveyors") }, ...surveyorOptions]}
                  placeholder={t("surveys.filter.allSurveyors")}
                />
              </div>
            ) : null}
            {projectOptions.length > 0 ? (
              <div>
                <Label className="text-xs">{t("surveys.field.linkedProject")}</Label>
                <SearchableSelect
                  className="mt-1"
                  value={linkedProjectId != null ? String(linkedProjectId) : ""}
                  onChange={(v) => {
                    setLinkedProjectId(v ? Number(v) : null);
                    setPage(1);
                  }}
                  options={[{ value: "", label: t("surveys.filter.allProjects") }, ...projectOptions]}
                  placeholder={t("surveys.filter.allProjects")}
                />
              </div>
            ) : null}
            <div>
              <Label className="text-xs">{t("surveys.filter.dateFrom")}</Label>
              <Input
                type="date"
                className="mt-1 h-9"
                value={toDateInputValue(dateFrom)}
                onChange={(e) => {
                  setDateFrom(e.target.value);
                  setPage(1);
                }}
              />
            </div>
            <div>
              <Label className="text-xs">{t("surveys.filter.dateTo")}</Label>
              <Input
                type="date"
                className="mt-1 h-9"
                value={toDateInputValue(dateTo)}
                onChange={(e) => {
                  setDateTo(e.target.value);
                  setPage(1);
                }}
              />
            </div>
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => void fetchList()} disabled={loading}>
              <RefreshCcw className={cn("mr-1 h-4 w-4", loading && "animate-spin")} />
              {t("common.refresh")}
            </Button>
            <Button variant="ghost" size="sm" onClick={resetFilters} disabled={loading}>
              {t("common.reset")}
            </Button>
            <span className="ml-auto text-xs text-slate-500">
              {t("surveys.total").replace("{count}", String(total))}
            </span>
          </div>
        </section>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : rows.length === 0 ? (
          <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
            <p>{hasActiveFilter ? t("surveys.emptyFiltered") : t("surveys.empty")}</p>
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
                <article
                  key={r.id}
                  className="cursor-pointer rounded-lg border bg-white p-3 shadow-sm hover:bg-slate-50/70"
                  onClick={() => openDetail(r.id)}
                >
                  <header className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <h3 className="break-words text-sm font-semibold leading-tight">{r.location}</h3>
                      <p className="mt-0.5 text-xs text-muted-foreground">
                        {r.code}
                        {r.constructionTypeLabel ? ` · ${r.constructionTypeLabel}` : ""}
                      </p>
                    </div>
                    <DriveStatusBadge status={r.driveSyncStatus} error={r.driveSyncError} />
                  </header>
                  <dl className="mt-2 grid grid-cols-2 gap-2 text-xs">
                    <div>
                      <dt className="text-muted-foreground">{t("surveys.field.surveyDate")}</dt>
                      <dd className="font-medium">{formatDate(r.surveyDate, lang)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("surveys.field.surveyor")}</dt>
                      <dd className="font-medium">{r.surveyorName ?? "—"}</dd>
                    </div>
                    <div className="col-span-2">
                      <dt className="text-muted-foreground">{t("surveys.field.projectOrOpportunity")}</dt>
                      <dd className="break-words font-medium">
                        {r.linkedProjectName ?? r.linkedOpportunityName ?? "—"}
                      </dd>
                    </div>
                  </dl>
                </article>
              ))}
            </div>

            {/* Desktop table */}
            <div className="hidden overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm lg:block">
              <table className="min-w-full text-sm">
                <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                  <tr>
                    <th className="px-3 py-2">{t("surveys.field.code")}</th>
                    <th className="px-3 py-2">{t("surveys.field.location")}</th>
                    <th className="px-3 py-2">{t("surveys.field.constructionType")}</th>
                    <th className="px-3 py-2">{t("surveys.field.surveyDate")}</th>
                    <th className="px-3 py-2">{t("surveys.field.surveyor")}</th>
                    <th className="px-3 py-2">{t("surveys.field.projectOrOpportunity")}</th>
                    <th className="px-3 py-2">{t("surveys.field.driveStatus")}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr
                      key={r.id}
                      className="cursor-pointer border-t align-top hover:bg-slate-50/60"
                      onClick={() => openDetail(r.id)}
                    >
                      <td className="whitespace-nowrap px-3 py-2 font-mono text-xs">{r.code}</td>
                      <td className="min-w-[220px] px-3 py-2 break-words font-medium">
                        {r.location}
                      </td>
                      <td className="px-3 py-2 text-xs">
                        {r.constructionTypeLabel ?? r.constructionTypeCode ?? "—"}
                      </td>
                      <td className="whitespace-nowrap px-3 py-2 text-xs">
                        {formatDate(r.surveyDate, lang)}
                      </td>
                      <td className="px-3 py-2 text-xs">{r.surveyorName ?? "—"}</td>
                      <td className="min-w-[180px] px-3 py-2 text-xs">
                        {r.linkedProjectName ?? r.linkedOpportunityName ?? "—"}
                      </td>
                      <td className="px-3 py-2">
                        <DriveStatusBadge status={r.driveSyncStatus} error={r.driveSyncError} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 ? (
              <div className="flex flex-wrap items-center justify-end gap-2 text-sm">
                <span className="text-xs text-slate-500">
                  {t("surveys.paginationLabel").replace("{page}", String(page)).replace("{totalPages}", String(totalPages))}
                </span>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={page <= 1}
                  onClick={() => setPage(page - 1)}
                >
                  {t("common.prev")}
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={page >= totalPages}
                  onClick={() => setPage(page + 1)}
                >
                  {t("common.next")}
                </Button>
              </div>
            ) : null}
          </>
        )}
      </div>
    </AdminLayout>
  );
};

export default AdminSurveys;
