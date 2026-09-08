import { useCallback, useEffect, useState } from "react";
import { ArrowDown, ArrowUp, Download, Eye, RefreshCw, Search, TriangleAlert } from "lucide-react";
import { Link } from "react-router-dom";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { extractApiError } from "@/lib/apiError";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import {
  adminApi,
  type MaterialAlertListParams,
  type MaterialAlertResponse,
  type MaterialAlertSeverity,
  type MaterialAlertStatus,
  type MaterialAlertType,
} from "@/services/adminApi";

type UserOption = { userId: number; userName: string };
type Translate = (key: string, params?: Record<string, string | number>) => string;

interface MaterialAlertsWorkspaceProps {
  projectId: number;
  users: UserOption[];
  t: Translate;
  formatDate: (value?: string | null) => string;
  formatNumber: (value: number) => string;
  refreshToken: number;
  canManage: boolean;
  onTotalChange: (total: number) => void;
}

const statusClass: Record<MaterialAlertStatus, string> = {
  Open: "border-rose-200 bg-rose-50 text-rose-800",
  Acknowledged: "border-amber-200 bg-amber-50 text-amber-800",
  Resolved: "border-emerald-200 bg-emerald-50 text-emerald-800",
};

const severityClass: Record<MaterialAlertSeverity, string> = {
  Critical: "border-rose-300 bg-rose-100 text-rose-900",
  Warning: "border-amber-300 bg-amber-100 text-amber-900",
};

const MaterialAlertsWorkspace = ({
  projectId,
  users,
  t,
  formatDate,
  formatNumber,
  refreshToken,
  canManage,
  onTotalChange,
}: MaterialAlertsWorkspaceProps) => {
  const [rows, setRows] = useState<MaterialAlertResponse[]>([]);
  const [counts, setCounts] = useState<Partial<Record<MaterialAlertStatus, number>>>({});
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [type, setType] = useState("all");
  const [status, setStatus] = useState("Open");
  const [severity, setSeverity] = useState("all");
  const [assignee, setAssignee] = useState("all");
  const [sortBy, setSortBy] = useState<MaterialAlertListParams["sortBy"]>("severity");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("desc");
  const [loading, setLoading] = useState(false);
  const [exporting, setExporting] = useState(false);
  const [evaluating, setEvaluating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const pageSize = 20;

  useEffect(() => {
    const timer = window.setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => window.clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    setRows([]);
    setCounts({});
    setTotal(0);
    setPage(1);
  }, [projectId]);

  const parameters = useCallback((requestedPage = page, requestedPageSize = pageSize): MaterialAlertListParams => ({
    page: requestedPage,
    pageSize: requestedPageSize,
    sortBy,
    sortDirection,
    ...(search ? { search } : {}),
    ...(type !== "all" ? { type: type as MaterialAlertType } : {}),
    ...(status !== "all" ? { status: status as MaterialAlertStatus } : {}),
    ...(severity !== "all" ? { severity: severity as MaterialAlertSeverity } : {}),
    ...(assignee !== "all" ? { assignedToUserId: Number(assignee) } : {}),
  }), [assignee, page, search, severity, sortBy, sortDirection, status, type]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.listMaterialAlerts(projectId, parameters());
      setRows(data.items);
      setCounts(data.statusCounts);
      setTotal(data.total);
      onTotalChange((data.statusCounts.Open ?? 0) + (data.statusCounts.Acknowledged ?? 0));
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setLoading(false);
    }
  }, [onTotalChange, parameters, projectId]);

  useEffect(() => { void load(); }, [load, refreshToken]);

  const exportRows = async () => {
    setExporting(true);
    try {
      const first = (await adminApi.listMaterialAlerts(projectId, parameters(1, 100))).data;
      const allRows = [...first.items];
      for (let nextPage = 2; nextPage <= Math.ceil(first.total / first.pageSize); nextPage += 1) {
        allRows.push(...(await adminApi.listMaterialAlerts(projectId, parameters(nextPage, 100))).data.items);
      }
      downloadCsv({
        filename: createCsvFilename(`material-alerts-project-${projectId}`),
        rows: allRows,
        columns: [
          { header: t("procurement.field.code"), value: "code" },
          { header: t("procurement.alerts.type"), value: (row) => t(`procurement.alerts.type.${row.type}`) },
          { header: t("procurement.field.status"), value: (row) => t(`procurement.alerts.status.${row.status}`) },
          { header: t("procurement.alerts.severity"), value: (row) => t(`procurement.alerts.severity.${row.severity}`) },
          { header: t("procurement.field.itemCode"), value: "itemCode" },
          { header: t("procurement.alerts.variance"), value: "varianceQuantity" },
          { header: t("procurement.alerts.assignee"), value: (row) => row.assignedToUserName ?? `#${row.assignedToUserId}` },
          { header: t("procurement.alerts.detectedAt"), value: (row) => formatDate(row.detectedAt) },
        ],
      });
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setExporting(false);
    }
  };

  const evaluate = async () => {
    setEvaluating(true);
    setError(null);
    try {
      await adminApi.evaluateMaterialAlerts(projectId);
      await load();
    } catch (reason) {
      setError(extractApiError(reason));
    } finally {
      setEvaluating(false);
    }
  };

  const pages = Math.max(1, Math.ceil(total / pageSize));
  const resetPage = (operation: () => void) => { operation(); setPage(1); };

  return (
    <section className="space-y-5" aria-labelledby="material-alerts-heading">
      <div className="flex flex-col gap-3 border-b pb-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <h2 id="material-alerts-heading" className="flex items-center gap-2 text-lg font-semibold"><TriangleAlert className="h-5 w-5 text-amber-600" />{t("procurement.alerts.title")}</h2>
          <p className="mt-1 text-sm text-muted-foreground">{t("procurement.alerts.description")}</p>
        </div>
        <div className="flex flex-col gap-2 sm:flex-row">
          {canManage && <Button variant="outline" onClick={() => void evaluate()} disabled={evaluating}><RefreshCw className={evaluating ? "mr-2 h-4 w-4 animate-spin" : "mr-2 h-4 w-4"} />{t("procurement.alerts.evaluate")}</Button>}
          <Button variant="outline" onClick={() => void exportRows()} disabled={exporting || rows.length === 0}><Download className="mr-2 h-4 w-4" />{t("procurement.alerts.export")}</Button>
        </div>
      </div>

      <div className="grid grid-cols-3 border-y" aria-label={t("procurement.alerts.summary")}>
        {(["Open", "Acknowledged", "Resolved"] as const).map((value) => <button key={value} type="button" className="min-w-0 border-r px-3 py-3 text-left last:border-r-0 hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => resetPage(() => setStatus(value))}>
          <span className="block text-xl font-semibold tabular-nums">{counts[value] ?? 0}</span>
          <span className="block truncate text-xs text-muted-foreground">{t(`procurement.alerts.status.${value}`)}</span>
        </button>)}
      </div>

      <div className="grid gap-3 border-y py-4 sm:grid-cols-2 xl:grid-cols-[minmax(220px,1fr)_160px_160px_180px_180px_auto_auto]">
        <div className="relative sm:col-span-2 xl:col-span-1"><Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" /><Input className="h-11 pl-9 sm:h-10" aria-label={t("procurement.alerts.search")} placeholder={t("procurement.alerts.searchPlaceholder")} value={searchInput} onChange={(event) => setSearchInput(event.target.value)} /></div>
        <Select value={type} onValueChange={(value) => resetPage(() => setType(value))}><SelectTrigger aria-label={t("procurement.alerts.type")}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("procurement.alerts.allTypes")}</SelectItem>{(["OverBoq", "Shortage"] as const).map((value) => <SelectItem key={value} value={value}>{t(`procurement.alerts.type.${value}`)}</SelectItem>)}</SelectContent></Select>
        <Select value={status} onValueChange={(value) => resetPage(() => setStatus(value))}><SelectTrigger aria-label={t("procurement.field.status")}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("procurement.alerts.allStatuses")}</SelectItem>{(["Open", "Acknowledged", "Resolved"] as const).map((value) => <SelectItem key={value} value={value}>{t(`procurement.alerts.status.${value}`)}</SelectItem>)}</SelectContent></Select>
        <Select value={severity} onValueChange={(value) => resetPage(() => setSeverity(value))}><SelectTrigger aria-label={t("procurement.alerts.severity")}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("procurement.alerts.allSeverities")}</SelectItem>{(["Critical", "Warning"] as const).map((value) => <SelectItem key={value} value={value}>{t(`procurement.alerts.severity.${value}`)}</SelectItem>)}</SelectContent></Select>
        <Select value={assignee} onValueChange={(value) => resetPage(() => setAssignee(value))}><SelectTrigger aria-label={t("procurement.alerts.assignee")}><SelectValue /></SelectTrigger><SelectContent><SelectItem value="all">{t("procurement.alerts.allAssignees")}</SelectItem>{users.map((user) => <SelectItem key={user.userId} value={String(user.userId)}>{user.userName}</SelectItem>)}</SelectContent></Select>
        <Select value={sortBy} onValueChange={(value) => resetPage(() => setSortBy(value as MaterialAlertListParams["sortBy"]))}><SelectTrigger aria-label={t("procurement.alerts.sortBy")}><SelectValue /></SelectTrigger><SelectContent>{(["severity", "variance", "detectedAt", "updatedAt", "itemCode"] as const).map((value) => <SelectItem key={value} value={value}>{t(`procurement.alerts.sort.${value}`)}</SelectItem>)}</SelectContent></Select>
        <Button variant="outline" onClick={() => resetPage(() => setSortDirection((value) => value === "asc" ? "desc" : "asc"))}>{sortDirection === "asc" ? <ArrowUp className="mr-2 h-4 w-4" /> : <ArrowDown className="mr-2 h-4 w-4" />}{t(`procurement.alerts.sort.${sortDirection}`)}</Button>
      </div>

      {loading && rows.length === 0 ? <p className="py-8 text-center text-sm text-muted-foreground">{t("common.loading")}</p> : error ? <div className="border-y py-6"><p className="text-sm text-destructive">{error}</p><Button className="mt-3" variant="outline" onClick={() => void load()}>{t("common.retry")}</Button></div> : rows.length === 0 ? <div className="border-y py-8 text-center"><p className="font-medium">{t("procurement.alerts.empty")}</p><p className="mt-1 text-sm text-muted-foreground">{t("procurement.alerts.emptyHint")}</p></div> : <>
        <div className="hidden overflow-x-auto border-y xl:block"><table className="w-full min-w-[980px] text-sm"><thead><tr className="border-b text-left text-xs uppercase text-muted-foreground"><th className="px-3 py-3 font-medium">{t("procurement.field.code")}</th><th className="px-3 py-3 font-medium">{t("procurement.alerts.risk")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.itemCode")}</th><th className="px-3 py-3 text-right font-medium">{t("procurement.alerts.variance")}</th><th className="px-3 py-3 font-medium">{t("procurement.alerts.assignee")}</th><th className="px-3 py-3 font-medium">{t("procurement.alerts.detectedAt")}</th><th className="px-3 py-3 font-medium">{t("procurement.field.actions")}</th></tr></thead><tbody>{rows.map((row) => <tr className="border-b" key={row.id}><td className="px-3 py-3 font-medium">{row.code}</td><td className="px-3 py-3"><div className="flex flex-wrap gap-1"><Badge variant="outline" className={severityClass[row.severity]}>{t(`procurement.alerts.severity.${row.severity}`)}</Badge><Badge variant="outline" className={statusClass[row.status]}>{t(`procurement.alerts.status.${row.status}`)}</Badge></div><p className="mt-1 text-xs text-muted-foreground">{t(`procurement.alerts.type.${row.type}`)}</p></td><td className="px-3 py-3"><span className="font-medium">{row.itemCode}</span><span className="ml-2 text-xs text-muted-foreground">{row.unit}</span><p className="mt-1 max-w-[260px] truncate text-xs text-muted-foreground">{row.description}</p></td><td className="px-3 py-3 text-right font-semibold tabular-nums">{formatNumber(row.varianceQuantity)} {row.unit}</td><td className="max-w-[180px] break-words px-3 py-3">{row.assignedToUserName ?? `#${row.assignedToUserId}`}</td><td className="px-3 py-3">{formatDate(row.detectedAt)}</td><td className="px-3 py-3"><Button asChild variant="outline" size="sm"><Link to={`/admin/procurement-control/projects/${projectId}/material-alerts/${row.id}`}><Eye className="mr-2 h-4 w-4" />{t("procurement.action.view")}</Link></Button></td></tr>)}</tbody></table></div>
        <div className="grid gap-3 xl:hidden">{rows.map((row) => <article className="rounded-md border p-4" key={row.id}><div className="flex items-start justify-between gap-3"><div className="min-w-0"><p className="text-xs font-medium uppercase text-muted-foreground">{row.code}</p><h3 className="mt-1 break-words font-semibold">{row.itemCode} · {row.description}</h3></div><Badge variant="outline" className={severityClass[row.severity]}>{t(`procurement.alerts.severity.${row.severity}`)}</Badge></div><div className="mt-3 flex flex-wrap gap-2"><Badge variant="outline" className={statusClass[row.status]}>{t(`procurement.alerts.status.${row.status}`)}</Badge><Badge variant="outline">{t(`procurement.alerts.type.${row.type}`)}</Badge></div><dl className="mt-4 grid grid-cols-2 gap-3"><Datum label={t("procurement.alerts.variance")} value={`${formatNumber(row.varianceQuantity)} ${row.unit}`} /><Datum label={t("procurement.alerts.assignee")} value={row.assignedToUserName ?? `#${row.assignedToUserId}`} /><Datum label={t("procurement.alerts.detectedAt")} value={formatDate(row.detectedAt)} /><Datum label={t("procurement.alerts.lastEvaluated")} value={formatDate(row.lastEvaluatedAt)} /></dl><Button asChild className="mt-4 w-full sm:w-auto" variant="outline"><Link to={`/admin/procurement-control/projects/${projectId}/material-alerts/${row.id}`}><Eye className="mr-2 h-4 w-4" />{t("procurement.action.view")}</Link></Button></article>)}</div>
      </>}

      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between"><p className="text-sm text-muted-foreground">{t("procurement.alerts.total", { count: total })}</p><div className="flex items-center justify-between gap-2 sm:justify-end"><Button variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>{t("common.prev")}</Button><span className="text-sm tabular-nums">{page} / {pages}</span><Button variant="outline" size="sm" disabled={page >= pages} onClick={() => setPage((value) => value + 1)}>{t("common.next")}</Button></div></div>
    </section>
  );
};

const Datum = ({ label, value }: { label: string; value: string }) => <div className="min-w-0"><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 break-words text-sm font-medium">{value}</dd></div>;

export default MaterialAlertsWorkspace;