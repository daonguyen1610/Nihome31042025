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
  X,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";
import { useAppSelector } from "@/store";
import { adminApi, type AuditLogItem, type AuditLogPage } from "@/services/adminApi";

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

const StatusBadge = ({ status }: { status: string }) => {
  const colors: Record<string, { bg: string; fg: string }> = {
    success: { bg: "rgba(16,185,129,0.15)", fg: "rgb(5,150,105)" },
    failure: { bg: "rgba(239,68,68,0.15)", fg: "rgb(220,38,38)" },
    denied: { bg: "rgba(249,115,22,0.15)", fg: "rgb(234,88,12)" },
  };
  const c = colors[status] ?? { bg: "rgba(148,163,184,0.18)", fg: "rgb(71,85,105)" };
  return (
    <span
      className="inline-flex items-center px-2 py-0.5 rounded-md text-[10px] font-bold uppercase tracking-wide"
      style={{ background: c.bg, color: c.fg }}
    >
      {status}
    </span>
  );
};

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
    <div className="text-[10px] font-bold uppercase tracking-wide mb-0.5" style={{ color: "hsl(var(--admin-muted))" }}>
      {label}
    </div>
    <div className={mono ? "font-mono text-xs break-all" : "text-xs"}>{value}</div>
  </div>
);

const Filter = ({ label, children }: { label: string; children: React.ReactNode }) => (
  <label className="block">
    <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>
      {label}
    </span>
    {children}
  </label>
);

const DetailDrawer = ({ item, onClose }: { item: AuditLogItem; onClose: () => void }) => {
  const { t } = useI18n();
  const sections: { label: string; value: string | null }[] = [
    { label: t("log.detail.oldValue"), value: prettyJson(item.oldValueJson) },
    { label: t("log.detail.newValue"), value: prettyJson(item.newValueJson) },
    { label: t("log.detail.metadata"), value: prettyJson(item.metadataJson) },
  ];
  return (
    <div
      className="fixed inset-0 z-50 flex justify-end"
      style={{ background: "rgba(15,23,42,0.45)" }}
      onClick={onClose}
    >
      <div
        className="h-full w-full max-w-2xl overflow-y-auto p-6 shadow-2xl"
        style={{ background: "hsl(var(--admin-surface))" }}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between mb-4">
          <div>
            <h2 className="font-display text-xl font-extrabold">{t("log.detail.title")}</h2>
            <p className="text-xs font-mono mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
              {item.auditId}
            </p>
          </div>
          <button onClick={onClose} className="p-2 rounded-lg hover:bg-muted">
            <X className="w-4 h-4" />
          </button>
        </div>

        <div className="grid grid-cols-2 gap-3 text-xs mb-5">
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

        <div className="mb-5">
          <div className="text-xs font-bold mb-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("log.message")}
          </div>
          <div className="admin-card p-3 text-sm">{item.message}</div>
        </div>

        {sections.map((s) =>
          s.value ? (
            <div className="mb-4" key={s.label}>
              <div className="text-xs font-bold mb-1" style={{ color: "hsl(var(--admin-muted))" }}>
                {s.label}
              </div>
              <pre className="admin-card p-3 text-[11px] font-mono overflow-x-auto">{s.value}</pre>
            </div>
          ) : null,
        )}
      </div>
    </div>
  );
};

const ActivityLog = () => {
  const { t } = useI18n();
  const user = useAppSelector((state) => state.auth.user);
  const isSuperAdmin = user?.role?.toUpperCase() === "SUPER_ADMIN";

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
    if (!isSuperAdmin) return;
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteAuditLog(id);
      toast.success(t("form.deleted"));
      fetchLogs();
    } catch {
      toast.error(t("form.error"));
    }
  };

  const onClearBefore = async () => {
    if (!isSuperAdmin) return;
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
    if (!isSuperAdmin) return;
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
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 data-testid="audit-log-title" className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("log.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {total === 0 ? `0 ${t("log.entries")}` : `${showingFrom}–${showingTo} / ${total} ${t("log.entries")}`}
            {" · "}{t("log.currentRetention")}: <span className="font-semibold">{retentionLabel}</span>
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button onClick={onRefresh} disabled={loading} className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2.5 rounded-xl border" style={{ borderColor: "hsl(var(--admin-border))" }}>
            {loading ? <Loader2 className="w-4 h-4 animate-spin" /> : <RefreshCw className="w-4 h-4" />}
            {t("common.refresh")}
          </button>
          {isSuperAdmin && (
            <button onClick={onClearBefore} className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2.5 rounded-xl" style={{ background: "hsl(var(--admin-danger))", color: "white" }}>
              <Eraser className="w-4 h-4" /> {t("log.clearBefore")}
            </button>
          )}
        </div>
      </div>

      {isSuperAdmin && (
        <div data-testid="audit-log-retention-card" className="admin-card p-5 mb-5">
          <div className="flex items-center justify-between mb-4">
            <h2 className="font-bold text-lg">{t("log.retentionConfig")}</h2>
            <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>{t("log.retentionHint")}</p>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-4 gap-3 items-end">
            <Filter label={t("log.retentionAmount")}>
              <input type="number" min={0} value={retentionValue} onChange={(e) => setRetentionValue(Number(e.target.value))} className="admin-input w-full" />
            </Filter>
            <Filter label={t("log.retentionUnit")}>
              <select value={retentionUnit} onChange={(e) => setRetentionUnit(e.target.value as RetentionUnit)} className="admin-input w-full">
                <option value="minutes">{t("log.unit.minutes")}</option>
                <option value="hours">{t("log.unit.hours")}</option>
                <option value="days">{t("log.unit.days")}</option>
              </select>
            </Filter>
            <div className="md:col-span-2 flex items-center justify-end gap-3">
              <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
                {retentionValue <= 0 ? t("log.retentionForever") : `≈ ${toMinutes(retentionValue, retentionUnit)} ${t("log.unit.minutes")}`}
              </p>
              <button onClick={onSaveConfig} disabled={savingConfig} className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2.5 rounded-xl" style={{ background: "hsl(var(--admin-primary))", color: "white" }}>
                {savingConfig ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                {t("log.saveConfig")}
              </button>
            </div>
          </div>
        </div>
      )}

      <div data-testid="audit-log-filter-card" className="admin-card p-5 mb-5">
        <div className="mb-3">
          <Filter label={t("log.search")}>
            <input
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
              className="admin-input w-full"
              placeholder={t("log.searchPlaceholder")}
            />
          </Filter>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-3">
          <Filter label={t("log.from")}>
            <input type="date" value={from} onChange={(e) => { setFrom(e.target.value); setPage(1); }} className="admin-input w-full" />
          </Filter>
          <Filter label={t("log.to")}>
            <input type="date" value={to} onChange={(e) => { setTo(e.target.value); setPage(1); }} className="admin-input w-full" />
          </Filter>
          <Filter label={t("log.action")}>
            <select value={actionFilter} onChange={(e) => { setActionFilter(e.target.value); setPage(1); }} className="admin-input w-full">
              <option value="">{t("common.all")}</option>
              {actions.map((a) => (<option key={a} value={a}>{a}</option>))}
            </select>
          </Filter>
          <Filter label={t("log.detail.status")}>
            <select value={statusFilter} onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }} className="admin-input w-full">
              <option value="">{t("common.all")}</option>
              <option value="success">success</option>
              <option value="failure">failure</option>
              <option value="denied">denied</option>
            </select>
          </Filter>
          <Filter label={t("log.actor")}>
            <input value={actorPhone} onChange={(e) => { setActorPhone(e.target.value); setPage(1); }} className="admin-input w-full" placeholder={t("log.actorPlaceholder")} />
          </Filter>
          <Filter label={t("log.ip")}>
            <input value={ip} onChange={(e) => { setIp(e.target.value); setPage(1); }} className="admin-input w-full" />
          </Filter>
          <Filter label={t("log.detail.resourceType")}>
            <input value={resourceType} onChange={(e) => { setResourceType(e.target.value); setPage(1); }} className="admin-input w-full" />
          </Filter>
          <Filter label={t("log.detail.correlationId")}>
            <input value={correlationId} onChange={(e) => { setCorrelationId(e.target.value); setPage(1); }} className="admin-input w-full" />
          </Filter>
        </div>
        <div className="flex justify-end mt-3">
          <button onClick={onResetFilters} className="text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted" style={{ color: "hsl(var(--admin-muted))" }}>
            {t("common.reset")}
          </button>
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table data-testid="audit-log-table" className="w-full text-sm min-w-[1100px]">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-4 py-3 font-semibold">{t("log.createdOn")}</th>
                <th className="px-4 py-3 font-semibold">{t("log.detail.status")}</th>
                <th className="px-4 py-3 font-semibold">{t("log.action")}</th>
                <th className="px-4 py-3 font-semibold">{t("log.detail.resource")}</th>
                <th className="px-4 py-3 font-semibold">{t("log.actor")}</th>
                <th className="px-4 py-3 font-semibold">{t("log.ip")}</th>
                <th className="px-4 py-3 font-semibold">{t("log.message")}</th>
                <th className="px-4 py-3 font-semibold text-right">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {loading && items.length === 0 && (
                <tr><td colSpan={8} className="px-5 py-10 text-center"><Loader2 className="w-5 h-5 animate-spin inline-block" /></td></tr>
              )}
              {!loading && items.length === 0 && (
                <tr><td colSpan={8} className="px-5 py-10 text-center text-sm" style={{ color: "hsl(var(--admin-muted))" }}>{t("common.empty")}</td></tr>
              )}
              {items.map((l) => (
                <tr key={l.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-4 py-3 text-xs whitespace-nowrap" style={{ color: "hsl(var(--admin-muted))" }}>{formatDate(l.createdAt)}</td>
                  <td className="px-4 py-3"><StatusBadge status={l.status} /></td>
                  <td className="px-4 py-3 font-mono text-xs">{l.action}</td>
                  <td className="px-4 py-3 text-xs">
                    <div className="font-semibold">{l.resourceType}</div>
                    {l.resourceId && (
                      <div className="font-mono text-[10px]" style={{ color: "hsl(var(--admin-muted))" }}>#{l.resourceId}</div>
                    )}
                  </td>
                  <td className="px-4 py-3 text-xs">
                    {l.actorPhone ? (
                      <>
                        <div className="font-semibold">{l.actorPhone}</div>
                        {l.actorRole && (
                          <div className="text-[10px]" style={{ color: "hsl(var(--admin-muted))" }}>{l.actorRole}</div>
                        )}
                      </>
                    ) : (
                      <span style={{ color: "hsl(var(--admin-muted))" }}>{l.actorType}</span>
                    )}
                  </td>
                  <td className="px-4 py-3 font-mono text-xs">{l.ipAddress ?? "—"}</td>
                  <td className="px-4 py-3 text-xs max-w-xs truncate" title={l.message}>{l.message}</td>
                  <td className="px-4 py-3 text-right whitespace-nowrap">
                    <button onClick={() => setSelected(l)} className="inline-flex items-center gap-1 text-xs font-bold px-2.5 py-1.5 rounded-lg hover:bg-muted mr-1">
                      <Eye className="w-3.5 h-3.5" /> {t("common.view")}
                    </button>
                    {isSuperAdmin && (
                      <button onClick={() => onDeleteOne(l.id)} className="inline-flex items-center gap-1 text-xs font-bold px-2.5 py-1.5 rounded-lg hover:bg-muted" style={{ color: "hsl(var(--admin-danger))" }}>
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex items-center justify-between px-5 py-3 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
          <div className="flex items-center gap-2 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
            <span>{t("common.rowsPerPage")}</span>
            <select value={pageSize} onChange={(e) => { setPageSize(Number(e.target.value)); setPage(1); }} className="admin-input">
              {[10, 25, 50, 100, 200].map((s) => (<option key={s} value={s}>{s}</option>))}
            </select>
          </div>
          <div className="flex items-center gap-3 text-xs">
            <button onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page <= 1 || loading} className="p-1.5 rounded-lg hover:bg-muted disabled:opacity-40">
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span>{page} / {totalPages}</span>
            <button onClick={() => setPage((p) => Math.min(totalPages, p + 1))} disabled={page >= totalPages || loading} className="p-1.5 rounded-lg hover:bg-muted disabled:opacity-40">
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>

      {selected && <DetailDrawer item={selected} onClose={() => setSelected(null)} />}
    </AdminLayout>
  );
};

export default ActivityLog;
