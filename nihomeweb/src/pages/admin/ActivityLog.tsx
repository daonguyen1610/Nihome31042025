import { useCallback, useEffect, useMemo, useState } from "react";
import {
  ChevronLeft,
  ChevronRight,
  Eraser,
  Eye,
  Loader2,
  RefreshCw,
  Save,
  Trash2,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";
import { usePermissions } from "@/hooks/usePermissions";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { adminApi, type AuditLogItem, type AuditLogPage } from "@/services/adminApi";
import { cn } from "@/lib/utils";

type RetentionUnit = "minutes" | "hours" | "days";

const DEFAULT_PAGE_SIZE = 25;

const splitRetention = (totalMinutes: number): { value: number; unit: RetentionUnit } => {
  if (totalMinutes <= 0) return { value: 0, unit: "days" };
  if (totalMinutes % 1440 === 0) return { value: totalMinutes / 1440, unit: "days" };
  if (totalMinutes % 60 === 0) return { value: totalMinutes / 60, unit: "hours" };
  return { value: totalMinutes, unit: "minutes" };
};

const toMinutes = (value: number, unit: RetentionUnit): number => {
  const v = Math.max(0, Math.floor(value));
  if (unit === "days") return v * 1440;
  if (unit === "hours") return v * 60;
  return v;
};

const formatDate = (iso: string) => {
  try {
    return new Date(iso).toLocaleString();
  } catch {
    return iso;
  }
};

const statusStyles: Record<string, string> = {
  success: "border-emerald-200 bg-emerald-50 text-emerald-700",
  failure: "border-rose-200 bg-rose-50 text-rose-700",
  denied: "border-amber-200 bg-amber-50 text-amber-700",
};

const StatusBadge = ({ status }: { status: string }) => (
  <Badge
    variant="outline"
    className={cn(
      "text-[10px] font-medium uppercase tracking-wide",
      statusStyles[status] ?? "border-slate-200 bg-slate-50 text-slate-700",
    )}
  >
    {status}
  </Badge>
);

const prettyJson = (raw: string | null) => {
  if (!raw) return null;
  try {
    return JSON.stringify(JSON.parse(raw), null, 2);
  } catch {
    return raw;
  }
};

const Field = ({ label, value, mono }: { label: string; value: string; mono?: boolean }) => (
  <div>
    <div className="mb-0.5 text-[10px] font-medium uppercase tracking-wide text-muted-foreground">
      {label}
    </div>
    <div className={mono ? "break-all font-mono text-xs" : "text-xs"}>{value}</div>
  </div>
);

const FilterField = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <div className="space-y-1.5">
    <Label className="text-xs">{label}</Label>
    {children}
  </div>
);

const selectClasses =
  "flex h-9 w-full rounded-md border border-input bg-background px-3 py-1 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring";

const DetailDrawer = ({ item, onClose }: { item: AuditLogItem | null; onClose: () => void }) => {
  const { t } = useI18n();
  if (!item) return null;
  const sections: { label: string; value: string | null }[] = [
    { label: t("log.detail.oldValue"), value: prettyJson(item.oldValueJson) },
    { label: t("log.detail.newValue"), value: prettyJson(item.newValueJson) },
    { label: t("log.detail.metadata"), value: prettyJson(item.metadataJson) },
  ];
  return (
    <Sheet open={item !== null} onOpenChange={(o) => (!o ? onClose() : null)}>
      <SheetContent side="right" className="w-full overflow-y-auto sm:max-w-2xl">
        <SheetHeader>
          <SheetTitle>{t("log.detail.title")}</SheetTitle>
          <p className="mt-1 font-mono text-xs text-muted-foreground">{item.auditId}</p>
        </SheetHeader>

        <div className="mt-5 grid grid-cols-2 gap-3 text-xs">
          <Field label={t("log.createdOn")} value={formatDate(item.createdAt)} />
          <Field label={t("log.action")} value={item.action} mono />
          <Field label={t("log.detail.status")} value={item.status} />
          <Field label={t("log.detail.failureReason")} value={item.failureReason ?? "—"} />
          <Field label={t("log.detail.resourceType")} value={item.resourceType} />
          <Field label={t("log.detail.resourceId")} value={item.resourceId ?? "—"} mono />
          <Field label={t("log.actor")} value={item.actorPhone ?? "—"} />
          <Field label={t("log.detail.actorRole")} value={item.actorRole ?? "—"} />
          <Field label={t("log.detail.actorType")} value={item.actorType} />
          <Field label={t("log.detail.channel")} value={item.channel} />
          <Field label={t("log.detail.sourceSystem")} value={item.sourceSystem} />
          <Field label={t("log.detail.targetSystem")} value={item.targetSystem ?? "—"} />
          <Field label={t("log.ip")} value={item.ipAddress ?? "—"} mono />
          <Field label={t("log.detail.requestId")} value={item.requestId ?? "—"} mono />
          <Field label={t("log.detail.correlationId")} value={item.correlationId ?? "—"} mono />
          <Field label={t("log.detail.userAgent")} value={item.userAgent ?? "—"} />
        </div>

        <div className="mt-5">
          <div className="mb-1 text-xs font-medium text-muted-foreground">{t("log.message")}</div>
          <div className="rounded-lg border bg-card p-3 text-sm">{item.message}</div>
        </div>

        {sections.map((s) =>
          s.value ? (
            <div className="mt-4" key={s.label}>
              <div className="mb-1 text-xs font-medium text-muted-foreground">{s.label}</div>
              <pre className="overflow-x-auto rounded-lg border bg-card p-3 font-mono text-[11px]">{s.value}</pre>
            </div>
          ) : null,
        )}
      </SheetContent>
    </Sheet>
  );
};

const ActivityLog = () => {
  const { t } = useI18n();
  const { has: hasPermission } = usePermissions();
  const canManageAudit = hasPermission("system.audit.manage");

  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [ip, setIp] = useState("");
  const [actorPhone, setActorPhone] = useState("");
  const [actionFilter, setActionFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");
  const [resourceType, setResourceType] = useState("");
  const [correlationId, setCorrelationId] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  const [data, setData] = useState<AuditLogPage | null>(null);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<AuditLogItem | null>(null);

  const [retentionMinutes, setRetentionMinutes] = useState<number>(43200);
  const [retentionValue, setRetentionValue] = useState<number>(30);
  const [retentionUnit, setRetentionUnit] = useState<RetentionUnit>("days");
  const [savingConfig, setSavingConfig] = useState(false);

  const fetchLogs = useCallback(async () => {
    setLoading(true);
    try {
      const res = await adminApi.listAuditLogs({
        page,
        pageSize,
        from: from || undefined,
        to: to || undefined,
        action: actionFilter || undefined,
        actorPhone: actorPhone || undefined,
        ip: ip || undefined,
        status: statusFilter || undefined,
        resourceType: resourceType || undefined,
        correlationId: correlationId || undefined,
        search: search.trim() || undefined,
      });
      setData(res.data);
    } catch {
      toast.error(t("form.error"));
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, from, to, actionFilter, actorPhone, ip, statusFilter, resourceType, correlationId, search, t]);

  const fetchConfig = useCallback(async () => {
    try {
      const res = await adminApi.getAuditConfig();
      setRetentionMinutes(res.data.retentionMinutes);
      const { value, unit } = splitRetention(res.data.retentionMinutes);
      setRetentionValue(value);
      setRetentionUnit(unit);
    } catch {
      /* silent */
    }
  }, []);

  useEffect(() => { fetchLogs(); }, [fetchLogs]);
  useEffect(() => { fetchConfig(); }, [fetchConfig]);

  const items: AuditLogItem[] = data?.items ?? [];
  const total = data?.total ?? 0;
  const actions = data?.actions ?? [];
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const showingFrom = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const showingTo = Math.min(page * pageSize, total);

  const onRefresh = () => (page !== 1 ? setPage(1) : fetchLogs());
  const onResetFilters = () => {
    setFrom(""); setTo(""); setIp(""); setActorPhone(""); setActionFilter("");
    setStatusFilter(""); setResourceType(""); setCorrelationId(""); setSearch(""); setPage(1);
  };

  const onDeleteOne = async (id: number) => {
    if (!canManageAudit) return;
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteAuditLog(id);
      toast.success(t("form.deleted"));
      fetchLogs();
    } catch {
      toast.error(t("form.error"));
    }
  };

  const visibleIds = useMemo(() => (data?.items ?? []).map((l) => l.id), [data]);
  const {
    selectedIds,
    bulkDeleting,
    allVisibleSelected,
    someVisibleSelected,
    toggleAllVisible,
    toggleOne,
    clearSelection,
    handleBulkDelete,
  } = useBulkSelection<number>({
    visibleIds,
    deleteOne: (id) => adminApi.deleteAuditLog(id),
    onAfter: async () => {
      await fetchLogs();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [page, pageSize, from, to, actionFilter, actorPhone, ip, statusFilter, resourceType, correlationId, search, clearSelection]);

  const onClearBefore = async () => {
    if (!canManageAudit) return;
    if (!from) { toast.error(t("log.selectBefore")); return; }
    if (!window.confirm(t("log.confirmClearBefore"))) return;
    try {
      const res = await adminApi.deleteAuditLogRange({ before: from });
      toast.success(`${t("form.deleted")} (${res.data.deleted})`);
      fetchLogs();
    } catch {
      toast.error(t("form.error"));
    }
  };

  const onSaveConfig = async () => {
    if (!canManageAudit) return;
    const minutes = toMinutes(retentionValue, retentionUnit);
    setSavingConfig(true);
    try {
      const res = await adminApi.updateAuditConfig({ retentionMinutes: minutes });
      setRetentionMinutes(res.data.retentionMinutes);
      toast.success(t("form.saved"));
    } catch {
      toast.error(t("form.error"));
    } finally {
      setSavingConfig(false);
    }
  };

  const retentionLabel = useMemo(() => {
    if (retentionMinutes <= 0) return t("log.retentionForever");
    const { value, unit } = splitRetention(retentionMinutes);
    return `${value} ${t(`log.unit.${unit}`)}`;
  }, [retentionMinutes, t]);

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 data-testid="audit-log-title" className="text-2xl font-semibold">{t("log.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {total === 0 ? `0 ${t("log.entries")}` : `${showingFrom}–${showingTo} / ${total} ${t("log.entries")}`}
              {" · "}{t("log.currentRetention")}: <span className="font-medium text-foreground">{retentionLabel}</span>
            </p>
          </div>
          <div className="flex items-center gap-2">
            <Button variant="outline" onClick={onRefresh} disabled={loading}>
              {loading ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <RefreshCw className="mr-1.5 h-4 w-4" />}
              {t("common.refresh")}
            </Button>
            {canManageAudit && (
              <Button variant="destructive" onClick={onClearBefore}>
                <Eraser className="mr-1.5 h-4 w-4" /> {t("log.clearBefore")}
              </Button>
            )}
          </div>
        </header>

        {canManageAudit && (
          <section data-testid="audit-log-retention-card" className="rounded-lg border bg-card p-5">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-lg font-semibold">{t("log.retentionConfig")}</h2>
              <p className="text-xs text-muted-foreground">{t("log.retentionHint")}</p>
            </div>
            <div className="grid grid-cols-1 items-end gap-3 md:grid-cols-4">
              <FilterField label={t("log.retentionAmount")}>
                <Input type="number" min={0} value={retentionValue} onChange={(e) => setRetentionValue(Number(e.target.value))} className="h-9" />
              </FilterField>
              <FilterField label={t("log.retentionUnit")}>
                <select
                  value={retentionUnit}
                  onChange={(e) => setRetentionUnit(e.target.value as RetentionUnit)}
                  className={selectClasses}
                >
                  <option value="minutes">{t("log.unit.minutes")}</option>
                  <option value="hours">{t("log.unit.hours")}</option>
                  <option value="days">{t("log.unit.days")}</option>
                </select>
              </FilterField>
              <div className="flex items-center justify-end gap-3 md:col-span-2">
                <p className="text-xs text-muted-foreground">
                  {retentionValue <= 0 ? t("log.retentionForever") : `≈ ${toMinutes(retentionValue, retentionUnit)} ${t("log.unit.minutes")}`}
                </p>
                <Button onClick={onSaveConfig} disabled={savingConfig}>
                  {savingConfig ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : <Save className="mr-1.5 h-4 w-4" />}
                  {t("log.saveConfig")}
                </Button>
              </div>
            </div>
          </section>
        )}

        <section data-testid="audit-log-filter-card" className="rounded-lg border bg-card p-5">
          <FilterField label={t("log.search")}>
            <Input
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
              className="h-9"
              placeholder={t("log.searchPlaceholder")}
            />
          </FilterField>

          <div className="mt-3 grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-4">
            <FilterField label={t("log.from")}>
              <Input type="date" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1); }} className="h-9" />
            </FilterField>
            <FilterField label={t("log.to")}>
              <Input type="date" value={to} onChange={(e) => { setTo(e.target.value); setPage(1); }} className="h-9" />
            </FilterField>
            <FilterField label={t("log.action")}>
              <select
                value={actionFilter}
                onChange={(e) => { setActionFilter(e.target.value); setPage(1); }}
                className={selectClasses}
              >
                <option value="">{t("common.all")}</option>
                {actions.map((a) => (<option key={a} value={a}>{a}</option>))}
              </select>
            </FilterField>
            <FilterField label={t("log.detail.status")}>
              <select
                value={statusFilter}
                onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
                className={selectClasses}
              >
                <option value="">{t("common.all")}</option>
                <option value="success">success</option>
                <option value="failure">failure</option>
                <option value="denied">denied</option>
              </select>
            </FilterField>
            <FilterField label={t("log.actor")}>
              <Input value={actorPhone} onChange={(e) => { setActorPhone(e.target.value); setPage(1); }} className="h-9" placeholder={t("log.actorPlaceholder")} />
            </FilterField>
            <FilterField label={t("log.ip")}>
              <Input value={ip} onChange={(e) => { setIp(e.target.value); setPage(1); }} className="h-9" />
            </FilterField>
            <FilterField label={t("log.detail.resourceType")}>
              <Input value={resourceType} onChange={(e) => { setResourceType(e.target.value); setPage(1); }} className="h-9" />
            </FilterField>
            <FilterField label={t("log.detail.correlationId")}>
              <Input value={correlationId} onChange={(e) => { setCorrelationId(e.target.value); setPage(1); }} className="h-9" />
            </FilterField>
          </div>

          <div className="mt-3 flex justify-end">
            <Button size="sm" variant="ghost" onClick={onResetFilters}>
              {t("common.reset")}
            </Button>
          </div>
        </section>

        <section className="overflow-hidden rounded-lg border bg-card">
          {canManageAudit && (
            <BulkActionBar
              selectedCount={selectedIds.size}
              bulkDeleting={bulkDeleting}
              onClear={clearSelection}
              onBulkDelete={() => void handleBulkDelete()}
            />
          )}

          {/* Mobile / tablet card view (<lg). Audit rows are wide (8-9
              columns) so a horizontally-scrolling table on a phone is
              painful — stack into cards. */}
          <ul className="grid gap-3 p-3 lg:hidden">
            {loading && items.length === 0 && (
              <li className="rounded-lg border border-dashed p-10 text-center">
                <Loader2 className="inline-block h-5 w-5 animate-spin text-primary" />
              </li>
            )}
            {!loading && items.length === 0 && (
              <li className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
                {t("common.empty")}
              </li>
            )}
            {items.map((l) => (
              <li key={l.id} className="rounded-lg border bg-card p-3 shadow-sm">
                <div className="flex items-start justify-between gap-2">
                  <div className="flex min-w-0 items-start gap-2">
                    {canManageAudit && (
                      <span onClick={(e) => e.stopPropagation()} className="pt-0.5">
                        <Checkbox
                          checked={selectedIds.has(l.id)}
                          onCheckedChange={(v) => toggleOne(l.id, v === true)}
                          aria-label={`${t("common.selectAll")} · ${l.action}`}
                        />
                      </span>
                    )}
                    <div className="min-w-0">
                      <p className="break-all font-mono text-xs font-semibold">{l.action}</p>
                      <p className="mt-0.5 text-xs text-muted-foreground">{formatDate(l.createdAt)}</p>
                    </div>
                  </div>
                  <StatusBadge status={l.status} />
                </div>

                <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
                  <dt className="text-muted-foreground">{t("log.detail.resource")}</dt>
                  <dd className="min-w-0">
                    <span className="font-medium">{l.resourceType}</span>
                    {l.resourceId && (
                      <span className="ml-1 font-mono text-[10px] text-muted-foreground">#{l.resourceId}</span>
                    )}
                  </dd>

                  <dt className="text-muted-foreground">{t("log.actor")}</dt>
                  <dd className="min-w-0">
                    {l.actorPhone ? (
                      <>
                        <span className="font-medium">{l.actorPhone}</span>
                        {l.actorRole && (
                          <span className="ml-1 text-[10px] text-muted-foreground">{l.actorRole}</span>
                        )}
                      </>
                    ) : (
                      <span className="text-muted-foreground">{l.actorType}</span>
                    )}
                  </dd>

                  <dt className="text-muted-foreground">{t("log.ip")}</dt>
                  <dd className="font-mono">{l.ipAddress ?? "—"}</dd>
                </dl>

                {l.message && (
                  <p className="mt-2 line-clamp-2 break-words text-xs text-muted-foreground" title={l.message}>
                    {l.message}
                  </p>
                )}

                <div className="mt-3 flex items-center justify-end gap-1 border-t pt-2">
                  <Button size="sm" variant="ghost" onClick={() => setSelected(l)}>
                    <Eye className="mr-1 h-3.5 w-3.5" /> {t("common.view")}
                  </Button>
                  {canManageAudit && (
                    <Button
                      size="icon"
                      variant="ghost"
                      className="h-8 w-8 text-destructive hover:text-destructive"
                      onClick={() => onDeleteOne(l.id)}
                      aria-label={t("common.delete")}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  )}
                </div>
              </li>
            ))}
          </ul>

          {/* Desktop table (lg+) */}
          <div className="hidden overflow-x-auto lg:block">
            <table data-testid="audit-log-table" className="w-full min-w-[1100px] divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  {canManageAudit && (
                    <th className="w-10 px-3 py-3 text-left">
                      <Checkbox
                        checked={
                          allVisibleSelected
                            ? true
                            : someVisibleSelected
                              ? "indeterminate"
                              : false
                        }
                        onCheckedChange={(v) => toggleAllVisible(v === true)}
                        aria-label={t("common.selectAll")}
                      />
                    </th>
                  )}
                  <th className="px-4 py-3 text-left font-medium">{t("log.createdOn")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("log.detail.status")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("log.action")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("log.detail.resource")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("log.actor")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("log.ip")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("log.message")}</th>
                  <th className="px-4 py-3 text-right font-medium">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {loading && items.length === 0 && (
                  <tr>
                    <td colSpan={canManageAudit ? 9 : 8} className="px-5 py-10 text-center">
                      <Loader2 className="inline-block h-5 w-5 animate-spin text-primary" />
                    </td>
                  </tr>
                )}
                {!loading && items.length === 0 && (
                  <tr>
                    <td colSpan={canManageAudit ? 9 : 8} className="px-5 py-10 text-center text-sm text-muted-foreground">
                      {t("common.empty")}
                    </td>
                  </tr>
                )}
                {items.map((l) => (
                  <tr key={l.id} className="hover:bg-muted/40 transition">
                    {canManageAudit && (
                      <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                        <Checkbox
                          checked={selectedIds.has(l.id)}
                          onCheckedChange={(v) => toggleOne(l.id, v === true)}
                          aria-label={`${t("common.selectAll")} · ${l.action}`}
                        />
                      </td>
                    )}
                    <td className="whitespace-nowrap px-4 py-3 text-xs text-muted-foreground">{formatDate(l.createdAt)}</td>
                    <td className="px-4 py-3"><StatusBadge status={l.status} /></td>
                    <td className="px-4 py-3 font-mono text-xs">{l.action}</td>
                    <td className="px-4 py-3 text-xs">
                      <div className="font-medium">{l.resourceType}</div>
                      {l.resourceId && (
                        <div className="font-mono text-[10px] text-muted-foreground">#{l.resourceId}</div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-xs">
                      {l.actorPhone ? (
                        <>
                          <div className="font-medium">{l.actorPhone}</div>
                          {l.actorRole && (
                            <div className="text-[10px] text-muted-foreground">{l.actorRole}</div>
                          )}
                        </>
                      ) : (
                        <span className="text-muted-foreground">{l.actorType}</span>
                      )}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs">{l.ipAddress ?? "—"}</td>
                    <td className="max-w-xs truncate px-4 py-3 text-xs" title={l.message}>{l.message}</td>
                    <td className="whitespace-nowrap px-4 py-3 text-right">
                      <Button size="sm" variant="ghost" onClick={() => setSelected(l)}>
                        <Eye className="mr-1 h-3.5 w-3.5" /> {t("common.view")}
                      </Button>
                      {canManageAudit && (
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-8 w-8 text-destructive hover:text-destructive"
                          onClick={() => onDeleteOne(l.id)}
                          aria-label={t("common.delete")}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="flex items-center justify-between border-t px-5 py-3">
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <span>{t("common.rowsPerPage")}</span>
              <select
                value={pageSize}
                onChange={(e) => { setPageSize(Number(e.target.value)); setPage(1); }}
                className={cn(selectClasses, "h-8 w-auto")}
              >
                {[10, 25, 50, 100, 200].map((s) => (<option key={s} value={s}>{s}</option>))}
              </select>
            </div>
            <div className="flex items-center gap-3 text-xs">
              <Button
                size="icon"
                variant="ghost"
                className="h-8 w-8"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1 || loading}
                aria-label="Previous page"
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>
              <span>{page} / {totalPages}</span>
              <Button
                size="icon"
                variant="ghost"
                className="h-8 w-8"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages || loading}
                aria-label="Next page"
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </section>
      </div>

      <DetailDrawer item={selected} onClose={() => setSelected(null)} />
    </AdminLayout>
  );
};

export default ActivityLog;
