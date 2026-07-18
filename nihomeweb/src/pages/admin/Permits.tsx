import { useCallback, useEffect, useMemo, useState } from "react";
import { AlertTriangle, Clock, FileCheck2, Filter, RefreshCcw, Search, ShieldAlert } from "lucide-react";
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
  adminApi,
  PERMIT_STATUSES,
  type DesignProjectListItemResponse,
  type MasterDataOption,
  type PermitChecklistItemResponse,
  type PermitChecklistListParams,
  type PermitChecklistRiskSummary,
  type PermitStatus,
  type UpdatePermitChecklistItemRequest,
} from "@/services/adminApi";

// ------------------------------- helpers -------------------------------

const STATUS_BADGE: Record<PermitStatus, string> = {
  NotStarted: "border-slate-200 bg-slate-50 text-slate-700",
  Preparing: "border-sky-200 bg-sky-50 text-sky-700",
  Submitted: "border-indigo-200 bg-indigo-50 text-indigo-700",
  UnderReview: "border-amber-200 bg-amber-50 text-amber-700",
  NeedMoreDocs: "border-orange-200 bg-orange-50 text-orange-700",
  Issued: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  Expired: "border-rose-200 bg-rose-50 text-rose-700",
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

/** Local YYYY-MM-DD → `${YYYY-MM-DD}T00:00:00.000Z`. Keeps the picked
 * calendar date intact across the UTC+7 boundary. */
const toUtcMidnight = (yyyyMmDd: string): string =>
  yyyyMmDd ? `${yyyyMmDd}T00:00:00.000Z` : "";

interface UserOption {
  id: number;
  fullName: string;
}

interface EditForm {
  status: PermitStatus;
  issuingAgency: string;
  ownerUserId: number | null;
  targetDeadline: string;
  submittedAt: string;
  issuedAt: string;
  expiresAt: string;
  note: string;
}

const formFrom = (row: PermitChecklistItemResponse): EditForm => ({
  status: row.status,
  issuingAgency: row.issuingAgency ?? "",
  ownerUserId: row.ownerUserId ?? null,
  targetDeadline: toDateInputValue(row.targetDeadline),
  submittedAt: toDateInputValue(row.submittedAt),
  issuedAt: toDateInputValue(row.issuedAt),
  expiresAt: toDateInputValue(row.expiresAt),
  note: row.note ?? "",
});

// -------------------------------- page ---------------------------------

const AdminPermits = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.permitsManage);
  const canPickUser = has(ADMIN_PERMS.users);

  // list state
  const [rows, setRows] = useState<PermitChecklistItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [risk, setRisk] = useState<PermitChecklistRiskSummary>({
    overdue: 0,
    dueSoon: 0,
    expiringSoon: 0,
    totalOpen: 0,
  });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const [search, setSearch] = useState("");
  const [projectId, setProjectId] = useState<number | null>(null);
  const [permitTypeCode, setPermitTypeCode] = useState<string>("");
  const [status, setStatus] = useState<PermitStatus | "">("");
  const [ownerUserId, setOwnerUserId] = useState<number | null>(null);
  const [flag, setFlag] = useState<"" | "overdue" | "dueSoon" | "expiringSoon">("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // lookups
  const [projects, setProjects] = useState<DesignProjectListItemResponse[]>([]);
  const [permitTypes, setPermitTypes] = useState<MasterDataOption[]>([]);
  const [users, setUsers] = useState<UserOption[]>([]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [projResp, typesResp] = await Promise.all([
          adminApi.listDesignProjects({ pageSize: 200 }).catch(() => ({
            data: { total: 0, page: 1, pageSize: 200, items: [] },
          })),
          adminApi.getMasterDataOptions("permit_type").catch(() => ({ data: [] })),
        ]);
        if (cancelled) return;
        setProjects(projResp.data.items ?? []);
        setPermitTypes((typesResp.data ?? []).filter((o) => o.isActive));

        if (canPickUser) {
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
            // no permission → filter stays hidden
          }
        }
      } catch {
        // non-fatal
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canPickUser]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: PermitChecklistListParams = { page, pageSize };
      if (search.trim()) params.search = search.trim();
      if (projectId != null) params.designProjectId = projectId;
      if (permitTypeCode) params.permitTypeCode = permitTypeCode;
      if (status) params.status = status;
      if (ownerUserId != null) params.ownerUserId = ownerUserId;
      if (flag === "overdue") params.overdue = true;
      if (flag === "dueSoon") params.dueSoon = true;
      if (flag === "expiringSoon") params.expiringSoon = true;
      const { data } = await adminApi.listPermits(params);
      setRows(data.items ?? []);
      setTotal(data.total ?? 0);
      setRisk(data.risk);
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
  }, [page, pageSize, search, projectId, permitTypeCode, status, ownerUserId, flag, t, toast]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const hasActiveFilter =
    search.trim().length > 0 ||
    projectId != null ||
    permitTypeCode !== "" ||
    status !== "" ||
    ownerUserId != null ||
    flag !== "";

  const projectOptions = useMemo(
    () =>
      projects.map((p) => ({
        value: String(p.id),
        label: `${p.projectCode} — ${p.name}`,
      })),
    [projects],
  );
  const userOptions = useMemo(
    () => users.map((u) => ({ value: String(u.id), label: u.fullName })),
    [users],
  );

  const resetFilters = () => {
    setSearch("");
    setProjectId(null);
    setPermitTypeCode("");
    setStatus("");
    setOwnerUserId(null);
    setFlag("");
    setPage(1);
  };

  // -------- edit dialog --------
  const [editing, setEditing] = useState<PermitChecklistItemResponse | null>(null);
  const [form, setForm] = useState<EditForm | null>(null);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openEdit = (row: PermitChecklistItemResponse) => {
    setEditing(row);
    setForm(formFrom(row));
    setFormError(null);
  };

  const closeEdit = () => {
    setEditing(null);
    setForm(null);
    setFormError(null);
  };

  const submitForm = async () => {
    if (!editing || !form) return;
    setFormError(null);
    setSaving(true);
    try {
      // Compute an explicit patch payload — Clear* flags are used only when
      // the field was previously set and the operator wiped it clean.
      const payload: UpdatePermitChecklistItemRequest = { status: form.status };
      const nextAgency = form.issuingAgency.trim();
      if (nextAgency !== (editing.issuingAgency ?? "")) {
        if (nextAgency.length === 0) payload.clearIssuingAgency = true;
        else payload.issuingAgency = nextAgency;
      }
      if ((form.ownerUserId ?? null) !== (editing.ownerUserId ?? null)) {
        if (form.ownerUserId == null) payload.clearOwner = true;
        else payload.ownerUserId = form.ownerUserId;
      }
      applyDatePatch("targetDeadline", form.targetDeadline, editing.targetDeadline, payload);
      applyDatePatch("submittedAt", form.submittedAt, editing.submittedAt, payload);
      applyDatePatch("issuedAt", form.issuedAt, editing.issuedAt, payload);
      applyDatePatch("expiresAt", form.expiresAt, editing.expiresAt, payload);
      const nextNote = form.note.trim();
      if (nextNote !== (editing.note ?? "")) {
        if (nextNote.length === 0) payload.clearNote = true;
        else payload.note = nextNote;
      }

      await adminApi.updatePermit(editing.id, payload);
      toast({ title: t("permits.updated") });
      closeEdit();
      await fetchList();
    } catch (err) {
      setFormError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  const applyDatePatch = (
    key: "targetDeadline" | "submittedAt" | "issuedAt" | "expiresAt",
    nextLocal: string,
    prev: string | null | undefined,
    payload: UpdatePermitChecklistItemRequest,
  ) => {
    const prevLocal = toDateInputValue(prev);
    if (nextLocal === prevLocal) return;
    const clearKey =
      key === "targetDeadline"
        ? "clearTargetDeadline"
        : key === "submittedAt"
          ? "clearSubmittedAt"
          : key === "issuedAt"
            ? "clearIssuedAt"
            : "clearExpiresAt";
    if (nextLocal.length === 0) {
      (payload as Record<string, unknown>)[clearKey] = true;
    } else {
      (payload as Record<string, unknown>)[key] = toUtcMidnight(nextLocal);
    }
  };

  // ----------------------------- render -----------------------------

  return (
    <AdminLayout>
      <div className="space-y-4 p-3 md:p-4">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex flex-col gap-1">
            <h1 className="text-xl font-bold text-slate-900 md:text-2xl">
              {t("permits.title")}
            </h1>
            <p className="text-sm text-slate-600">{t("permits.subtitle")}</p>
          </div>
        </header>

        {/* Risk register */}
        <section className="grid gap-2 md:grid-cols-4">
          <RiskCard
            title={t("permits.risk.overdue")}
            value={risk.overdue}
            tone="rose"
            icon={<ShieldAlert className="h-4 w-4" />}
            onClick={() => {
              setFlag("overdue");
              setPage(1);
            }}
            active={flag === "overdue"}
          />
          <RiskCard
            title={t("permits.risk.dueSoon")}
            value={risk.dueSoon}
            tone="amber"
            icon={<Clock className="h-4 w-4" />}
            onClick={() => {
              setFlag("dueSoon");
              setPage(1);
            }}
            active={flag === "dueSoon"}
          />
          <RiskCard
            title={t("permits.risk.expiringSoon")}
            value={risk.expiringSoon}
            tone="orange"
            icon={<AlertTriangle className="h-4 w-4" />}
            onClick={() => {
              setFlag("expiringSoon");
              setPage(1);
            }}
            active={flag === "expiringSoon"}
          />
          <RiskCard
            title={t("permits.risk.open")}
            value={risk.totalOpen}
            tone="slate"
            icon={<FileCheck2 className="h-4 w-4" />}
          />
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
            <div className="lg:col-span-2">
              <Label className="text-xs">{t("permits.filter.search")}</Label>
              <div className="relative mt-1">
                <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-slate-400" />
                <Input
                  className="pl-8"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder={t("permits.filter.search")}
                />
              </div>
            </div>
            <div>
              <Label className="text-xs">{t("permits.field.status")}</Label>
              <Select
                value={status}
                onValueChange={(v) => {
                  setStatus(v === "__all__" ? "" : (v as PermitStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("permits.filter.allStatuses")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("permits.filter.allStatuses")}</SelectItem>
                  {PERMIT_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`permits.status.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label className="text-xs">{t("permits.field.permitType")}</Label>
              <Select
                value={permitTypeCode}
                onValueChange={(v) => {
                  setPermitTypeCode(v === "__all__" ? "" : v);
                  setPage(1);
                }}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("permits.filter.allTypes")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("permits.filter.allTypes")}</SelectItem>
                  {permitTypes.map((c) => (
                    <SelectItem key={c.code} value={c.code}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {projectOptions.length > 0 ? (
              <div>
                <Label className="text-xs">{t("permits.field.project")}</Label>
                <SearchableSelect
                  className="mt-1"
                  value={projectId != null ? String(projectId) : ""}
                  onChange={(v) => {
                    setProjectId(v ? Number(v) : null);
                    setPage(1);
                  }}
                  options={[{ value: "", label: t("permits.filter.allProjects") }, ...projectOptions]}
                  placeholder={t("permits.filter.allProjects")}
                />
              </div>
            ) : null}
            {canPickUser && userOptions.length > 0 ? (
              <div>
                <Label className="text-xs">{t("permits.field.owner")}</Label>
                <SearchableSelect
                  className="mt-1"
                  value={ownerUserId != null ? String(ownerUserId) : ""}
                  onChange={(v) => {
                    setOwnerUserId(v ? Number(v) : null);
                    setPage(1);
                  }}
                  options={[{ value: "", label: t("permits.filter.allOwners") }, ...userOptions]}
                  placeholder={t("permits.filter.allOwners")}
                />
              </div>
            ) : null}
          </div>
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => void fetchList()} disabled={loading}>
              <RefreshCcw className={cn("mr-1 h-4 w-4", loading && "animate-spin")} />
              {t("common.refresh")}
            </Button>
            <Button variant="ghost" size="sm" onClick={resetFilters} disabled={loading}>
              <Filter className="mr-1 h-4 w-4" />
              {t("common.reset")}
            </Button>
            <span className="ml-auto text-xs text-slate-500">
              {t("permits.total").replace("{count}", String(total))}
            </span>
          </div>
        </section>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : rows.length === 0 ? (
          <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
            <p>{hasActiveFilter ? t("permits.emptyFiltered") : t("permits.empty")}</p>
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
                  className={cn(
                    "rounded-lg border bg-white p-3 shadow-sm hover:bg-slate-50/70",
                    canManage && "cursor-pointer",
                  )}
                  onClick={() => (canManage ? openEdit(r) : undefined)}
                >
                  <header className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <h3 className="break-words text-sm font-semibold leading-tight">
                        {r.permitTypeLabel ?? r.permitTypeCode}
                      </h3>
                      <p className="mt-0.5 break-words text-xs text-muted-foreground">
                        {r.designProjectCode ? `${r.designProjectCode} — ` : ""}
                        {r.designProjectName ?? "—"}
                      </p>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <Badge variant="outline" className={cn("whitespace-nowrap", STATUS_BADGE[r.status])}>
                        {t(`permits.status.${r.status}`)}
                      </Badge>
                      <RiskPills row={r} t={t} />
                    </div>
                  </header>
                  <dl className="mt-2 grid grid-cols-2 gap-2 text-xs">
                    <div>
                      <dt className="text-muted-foreground">{t("permits.field.owner")}</dt>
                      <dd className="font-medium">{r.ownerName ?? "—"}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("permits.field.agency")}</dt>
                      <dd className="font-medium">{r.issuingAgency ?? "—"}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("permits.field.targetDeadline")}</dt>
                      <dd className="font-medium">{formatDate(r.targetDeadline, lang)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("permits.field.expiresAt")}</dt>
                      <dd className="font-medium">{formatDate(r.expiresAt, lang)}</dd>
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
                    <th className="px-3 py-2">{t("permits.field.project")}</th>
                    <th className="px-3 py-2">{t("permits.field.permitType")}</th>
                    <th className="px-3 py-2">{t("permits.field.agency")}</th>
                    <th className="px-3 py-2">{t("permits.field.owner")}</th>
                    <th className="px-3 py-2">{t("permits.field.status")}</th>
                    <th className="px-3 py-2">{t("permits.field.targetDeadline")}</th>
                    <th className="px-3 py-2">{t("permits.field.expiresAt")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {rows.map((r) => (
                    <tr
                      key={r.id}
                      className={cn(
                        "transition-colors hover:bg-slate-50/70",
                        canManage && "cursor-pointer",
                      )}
                      onClick={() => (canManage ? openEdit(r) : undefined)}
                    >
                      <td className="px-3 py-2 text-slate-700">
                        <div className="font-mono text-xs text-slate-500">{r.designProjectCode ?? "—"}</div>
                        <div className="text-slate-800">{r.designProjectName ?? "—"}</div>
                      </td>
                      <td className="px-3 py-2 font-medium text-slate-900">
                        {r.permitTypeLabel ?? r.permitTypeCode}
                      </td>
                      <td className="px-3 py-2 text-slate-700">{r.issuingAgency ?? "—"}</td>
                      <td className="px-3 py-2 text-slate-700">{r.ownerName ?? "—"}</td>
                      <td className="px-3 py-2">
                        <div className="flex flex-col items-start gap-1">
                          <Badge variant="outline" className={cn("whitespace-nowrap", STATUS_BADGE[r.status])}>
                            {t(`permits.status.${r.status}`)}
                          </Badge>
                          <RiskPills row={r} t={t} />
                        </div>
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-slate-700">
                        {formatDate(r.targetDeadline, lang)}
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-slate-700">
                        {formatDate(r.expiresAt, lang)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {totalPages > 1 ? (
              <div className="flex items-center justify-between text-sm text-slate-600">
                <span>
                  {t("designProjects.paginationLabel")
                    .replace("{page}", String(page))
                    .replace("{totalPages}", String(totalPages))}
                </span>
                <div className="flex gap-1">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={page <= 1}
                  >
                    {t("common.previous")}
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                    disabled={page >= totalPages}
                  >
                    {t("common.next")}
                  </Button>
                </div>
              </div>
            ) : null}
          </>
        )}
      </div>

      {/* Edit dialog */}
      <Dialog open={!!editing} onOpenChange={(o) => (!o ? closeEdit() : undefined)}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{t("permits.form.title")}</DialogTitle>
            <DialogDescription>
              {editing ? (
                <span className="text-xs text-slate-600">
                  {(editing.designProjectCode ?? "") + " — " + (editing.permitTypeLabel ?? editing.permitTypeCode)}
                </span>
              ) : null}
              <span className="mt-1 block">{t("permits.form.hint")}</span>
            </DialogDescription>
          </DialogHeader>
          {form && editing ? (
            <div className="grid gap-3 md:grid-cols-2">
              <div>
                <Label>{t("permits.field.status")}</Label>
                <Select
                  value={form.status}
                  onValueChange={(v) => setForm((f) => (f ? { ...f, status: v as PermitStatus } : f))}
                >
                  <SelectTrigger className="mt-1 h-9">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {PERMIT_STATUSES.map((s) => (
                      <SelectItem key={s} value={s}>
                        {t(`permits.status.${s}`)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>{t("permits.field.agency")}</Label>
                <Input
                  value={form.issuingAgency}
                  onChange={(e) => setForm((f) => (f ? { ...f, issuingAgency: e.target.value } : f))}
                />
              </div>
              {canPickUser ? (
                <div className="md:col-span-2">
                  <Label>{t("permits.field.owner")}</Label>
                  <SearchableSelect
                    className="mt-1"
                    value={form.ownerUserId != null ? String(form.ownerUserId) : ""}
                    onChange={(v) => setForm((f) => (f ? { ...f, ownerUserId: v ? Number(v) : null } : f))}
                    options={[{ value: "", label: t("permits.form.ownerNone") }, ...userOptions]}
                    placeholder={t("permits.form.ownerNone")}
                  />
                </div>
              ) : null}
              <div>
                <Label>{t("permits.field.targetDeadline")}</Label>
                <Input
                  type="date"
                  value={form.targetDeadline}
                  onChange={(e) => setForm((f) => (f ? { ...f, targetDeadline: e.target.value } : f))}
                />
              </div>
              <div>
                <Label>{t("permits.field.submittedAt")}</Label>
                <Input
                  type="date"
                  value={form.submittedAt}
                  onChange={(e) => setForm((f) => (f ? { ...f, submittedAt: e.target.value } : f))}
                />
              </div>
              <div>
                <Label>{t("permits.field.issuedAt")}</Label>
                <Input
                  type="date"
                  value={form.issuedAt}
                  onChange={(e) => setForm((f) => (f ? { ...f, issuedAt: e.target.value } : f))}
                />
              </div>
              <div>
                <Label>{t("permits.field.expiresAt")}</Label>
                <Input
                  type="date"
                  value={form.expiresAt}
                  onChange={(e) => setForm((f) => (f ? { ...f, expiresAt: e.target.value } : f))}
                />
              </div>
              <div className="md:col-span-2">
                <Label>{t("permits.field.note")}</Label>
                <Textarea
                  rows={3}
                  value={form.note}
                  onChange={(e) => setForm((f) => (f ? { ...f, note: e.target.value } : f))}
                />
              </div>
            </div>
          ) : null}
          {formError ? <p className="text-sm text-rose-600">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={closeEdit} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submitForm()} disabled={saving || !canManage}>
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

const RiskCard = ({
  title,
  value,
  tone,
  icon,
  onClick,
  active,
}: {
  title: string;
  value: number;
  tone: "rose" | "amber" | "orange" | "slate";
  icon: React.ReactNode;
  onClick?: () => void;
  active?: boolean;
}) => {
  const tones: Record<typeof tone, string> = {
    rose: "border-rose-200 bg-rose-50/60 text-rose-800",
    amber: "border-amber-200 bg-amber-50/60 text-amber-800",
    orange: "border-orange-200 bg-orange-50/60 text-orange-800",
    slate: "border-slate-200 bg-slate-50 text-slate-800",
  };
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={!onClick}
      className={cn(
        "flex items-center justify-between rounded-lg border px-3 py-2.5 text-left shadow-sm transition-colors",
        tones[tone],
        onClick ? "cursor-pointer hover:brightness-95" : "cursor-default",
        active && "ring-2 ring-offset-1 ring-slate-800/40",
      )}
    >
      <div className="flex items-center gap-2">
        <span className="rounded-md bg-white/70 p-1">{icon}</span>
        <span className="text-xs font-medium">{title}</span>
      </div>
      <span className="text-lg font-bold">{value}</span>
    </button>
  );
};

const RiskPills = ({
  row,
  t,
}: {
  row: PermitChecklistItemResponse;
  t: (key: string) => string;
}) => {
  if (!row.isOverdue && !row.isDueSoon && !row.isExpiringSoon) return null;
  return (
    <div className="flex flex-wrap items-center gap-1">
      {row.isOverdue ? (
        <Badge variant="outline" className="border-rose-200 bg-rose-50 text-rose-700 whitespace-nowrap">
          {t("permits.risk.overduePill")}
        </Badge>
      ) : null}
      {row.isDueSoon ? (
        <Badge variant="outline" className="border-amber-200 bg-amber-50 text-amber-700 whitespace-nowrap">
          {t("permits.risk.dueSoonPill")}
        </Badge>
      ) : null}
      {row.isExpiringSoon ? (
        <Badge variant="outline" className="border-orange-200 bg-orange-50 text-orange-700 whitespace-nowrap">
          {t("permits.risk.expiringSoonPill")}
        </Badge>
      ) : null}
    </div>
  );
};

export default AdminPermits;
