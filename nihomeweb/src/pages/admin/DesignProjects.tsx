import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Pencil, PenTool, Plus, RefreshCcw, Search, Trash2 } from "lucide-react";
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
  adminApi,
  DESIGN_PROJECT_STAGES,
  DESIGN_PROJECT_STATUSES,
  type CreateDesignProjectRequest,
  type CustomerResponse,
  type DesignProjectListItemResponse,
  type DesignProjectListParams,
  type DesignProjectResponse,
  type DesignProjectStage,
  type DesignProjectStatus,
  type ContractResponse,
} from "@/services/adminApi";

// ------------------------------- helpers -------------------------------

const STAGE_BADGE: Record<DesignProjectStage, string> = {
  Concept: "border-sky-200 bg-sky-50 text-sky-700",
  BasicDesign: "border-indigo-200 bg-indigo-50 text-indigo-700",
  ShopDrawing: "border-violet-200 bg-violet-50 text-violet-700",
  Completed: "border-emerald-200 bg-emerald-50 text-emerald-700",
};

const STATUS_BADGE: Record<DesignProjectStatus, string> = {
  Active: "border-emerald-200 bg-emerald-50 text-emerald-700",
  OnHold: "border-amber-200 bg-amber-50 text-amber-700",
  Completed: "border-slate-200 bg-slate-50 text-slate-700",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
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

/** Turn a YYYY-MM-DD input into `${YYYY-MM-DD}T00:00:00.000Z`. Keeps the
 * chosen local calendar date intact across the UTC+7 boundary. */
const toUtcMidnight = (yyyyMmDd: string): string =>
  yyyyMmDd ? `${yyyyMmDd}T00:00:00.000Z` : "";

const emptyForm = (): CreateDesignProjectRequest => ({
  name: "",
  customerId: 0,
  contractId: null,
  projectManagerUserId: null,
  designLeadUserId: null,
  startDate: null,
  deadline: null,
  note: "",
});

interface UserOption {
  id: number;
  fullName: string;
}

// -------------------------------- page ---------------------------------

const AdminDesignProjects = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const navigate = useNavigate();
  const canManage = has(ADMIN_PERMS.designProjectsManage);
  const canPickUser = has(ADMIN_PERMS.users);

  // list state
  const [rows, setRows] = useState<DesignProjectListItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // filters
  const [search, setSearch] = useState("");
  const [customerId, setCustomerId] = useState<number | null>(null);
  const [pmId, setPmId] = useState<number | null>(null);
  const [leadId, setLeadId] = useState<number | null>(null);
  const [stage, setStage] = useState<DesignProjectStage | "">("");
  const [status, setStatus] = useState<DesignProjectStatus | "">("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // lookups
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [contracts, setContracts] = useState<ContractResponse[]>([]);
  const [users, setUsers] = useState<UserOption[]>([]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [custResp, ctrResp] = await Promise.all([
          adminApi.listCustomers({ pageSize: 200 }).catch(() => ({
            data: { total: 0, page: 1, pageSize: 200, items: [] },
          })),
          adminApi.listContracts({ pageSize: 200 }).catch(() => ({
            data: { total: 0, page: 1, pageSize: 200, items: [] },
          })),
        ]);
        if (cancelled) return;
        setCustomers(custResp.data.items ?? []);
        setContracts(ctrResp.data.items ?? []);
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
            // no permission — dropdowns stay hidden
          }
        }
      } catch {
        // non-fatal; filters fall back to empty lists
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
      const params: DesignProjectListParams = { page, pageSize };
      if (search.trim()) params.search = search.trim();
      if (customerId != null) params.customerId = customerId;
      if (pmId != null) params.projectManagerUserId = pmId;
      if (leadId != null) params.designLeadUserId = leadId;
      if (stage) params.stage = stage;
      if (status) params.status = status;
      const { data } = await adminApi.listDesignProjects(params);
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
  }, [page, pageSize, search, customerId, pmId, leadId, stage, status, t, toast]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const hasActiveFilter =
    search.trim().length > 0 ||
    customerId != null ||
    pmId != null ||
    leadId != null ||
    stage !== "" ||
    status !== "";

  const customerOptions = useMemo(
    () => customers.map((c) => ({ value: String(c.id), label: c.name })),
    [customers],
  );
  const contractOptions = useMemo(
    () =>
      contracts.map((c) => ({
        value: String(c.id),
        label: `${c.contractNumber}${c.customerName ? ` — ${c.customerName}` : ""}`,
      })),
    [contracts],
  );
  const userOptions = useMemo(
    () => users.map((u) => ({ value: String(u.id), label: u.fullName })),
    [users],
  );

  const openDetail = (id: number) => navigate(`/admin/design-projects/${id}`);

  const resetFilters = () => {
    setSearch("");
    setCustomerId(null);
    setPmId(null);
    setLeadId(null);
    setStage("");
    setStatus("");
    setPage(1);
  };

  // -------- create / edit dialog --------
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingDetail, setEditingDetail] = useState<DesignProjectResponse | null>(null);
  const [form, setForm] = useState<CreateDesignProjectRequest>(emptyForm());
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const isEdit = !!editingDetail;

  const openCreate = () => {
    setEditingDetail(null);
    setForm(emptyForm());
    setFormError(null);
    setDialogOpen(true);
  };

  const openEdit = async (id: number) => {
    setSaving(true);
    try {
      const { data } = await adminApi.getDesignProject(id);
      setEditingDetail(data);
      setForm({
        name: data.name,
        customerId: data.customerId,
        contractId: data.contractId ?? null,
        projectManagerUserId: data.projectManagerUserId ?? null,
        designLeadUserId: data.designLeadUserId ?? null,
        startDate: data.startDate ?? null,
        deadline: data.deadline ?? null,
        note: data.note ?? "",
      });
      setFormError(null);
      setDialogOpen(true);
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  const submitForm = async () => {
    setFormError(null);
    if (!form.name.trim()) {
      setFormError(t("designProjects.form.nameRequired"));
      return;
    }
    if (!form.customerId) {
      setFormError(t("designProjects.form.customerRequired"));
      return;
    }
    if (form.startDate && form.deadline && form.deadline < form.startDate) {
      setFormError(t("designProjects.form.deadlineBeforeStart"));
      return;
    }
    setSaving(true);
    try {
      const payload: CreateDesignProjectRequest = {
        name: form.name.trim(),
        customerId: form.customerId,
        contractId: form.contractId ?? null,
        projectManagerUserId: form.projectManagerUserId ?? null,
        designLeadUserId: form.designLeadUserId ?? null,
        startDate: form.startDate || null,
        deadline: form.deadline || null,
        note: form.note?.trim() || null,
      };
      if (isEdit) {
        await adminApi.updateDesignProject(editingDetail!.id, payload);
        toast({ title: t("designProjects.updated") });
      } else {
        await adminApi.createDesignProject(payload);
        toast({ title: t("designProjects.created") });
      }
      setDialogOpen(false);
      setEditingDetail(null);
      await fetchList();
    } catch (err) {
      setFormError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // -------- delete confirmation --------
  const [deleting, setDeleting] = useState<DesignProjectListItemResponse | null>(null);
  const [busyDelete, setBusyDelete] = useState(false);

  const confirmDelete = async () => {
    if (!deleting) return;
    setBusyDelete(true);
    try {
      await adminApi.deleteDesignProject(deleting.id);
      toast({ title: t("designProjects.deleted") });
      setDeleting(null);
      await fetchList();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setBusyDelete(false);
    }
  };

  // ----------------------------- render -----------------------------

  return (
    <AdminLayout>
      <div className="space-y-4 p-3 md:p-4">
        <header className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex flex-col gap-1">
            <h1 className="text-xl font-bold text-slate-900 md:text-2xl">
              {t("designProjects.title")}
            </h1>
            <p className="text-sm text-slate-600">{t("designProjects.subtitle")}</p>
          </div>
          {canManage ? (
            <Button size="sm" onClick={openCreate}>
              <Plus className="mr-1 h-4 w-4" />
              {t("designProjects.new")}
            </Button>
          ) : null}
        </header>

        <section className="rounded-lg border border-slate-200 bg-white p-3 shadow-sm">
          <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
            <div className="lg:col-span-2">
              <Label className="text-xs">{t("designProjects.filter.search")}</Label>
              <div className="relative mt-1">
                <Search className="pointer-events-none absolute left-2.5 top-2.5 h-4 w-4 text-slate-400" />
                <Input
                  className="pl-8"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setPage(1);
                  }}
                  placeholder={t("designProjects.filter.search")}
                />
              </div>
            </div>
            <div>
              <Label className="text-xs">{t("designProjects.field.stage")}</Label>
              <Select
                value={stage}
                onValueChange={(v) => {
                  setStage(v === "__all__" ? "" : (v as DesignProjectStage));
                  setPage(1);
                }}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("designProjects.filter.allStages")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("designProjects.filter.allStages")}</SelectItem>
                  {DESIGN_PROJECT_STAGES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`designProjects.stage.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label className="text-xs">{t("designProjects.field.status")}</Label>
              <Select
                value={status}
                onValueChange={(v) => {
                  setStatus(v === "__all__" ? "" : (v as DesignProjectStatus));
                  setPage(1);
                }}
              >
                <SelectTrigger className="mt-1 h-9">
                  <SelectValue placeholder={t("designProjects.filter.allStatuses")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("designProjects.filter.allStatuses")}</SelectItem>
                  {DESIGN_PROJECT_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {t(`designProjects.status.${s}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {customerOptions.length > 0 ? (
              <div>
                <Label className="text-xs">{t("designProjects.field.customer")}</Label>
                <SearchableSelect
                  className="mt-1"
                  value={customerId != null ? String(customerId) : ""}
                  onChange={(v) => {
                    setCustomerId(v ? Number(v) : null);
                    setPage(1);
                  }}
                  options={[{ value: "", label: t("designProjects.filter.allCustomers") }, ...customerOptions]}
                  placeholder={t("designProjects.filter.allCustomers")}
                />
              </div>
            ) : null}
            {canPickUser && userOptions.length > 0 ? (
              <>
                <div>
                  <Label className="text-xs">{t("designProjects.field.pm")}</Label>
                  <SearchableSelect
                    className="mt-1"
                    value={pmId != null ? String(pmId) : ""}
                    onChange={(v) => {
                      setPmId(v ? Number(v) : null);
                      setPage(1);
                    }}
                    options={[{ value: "", label: t("designProjects.filter.allPms") }, ...userOptions]}
                    placeholder={t("designProjects.filter.allPms")}
                  />
                </div>
                <div>
                  <Label className="text-xs">{t("designProjects.field.designLead")}</Label>
                  <SearchableSelect
                    className="mt-1"
                    value={leadId != null ? String(leadId) : ""}
                    onChange={(v) => {
                      setLeadId(v ? Number(v) : null);
                      setPage(1);
                    }}
                    options={[{ value: "", label: t("designProjects.filter.allLeads") }, ...userOptions]}
                    placeholder={t("designProjects.filter.allLeads")}
                  />
                </div>
              </>
            ) : null}
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
              {t("designProjects.total").replace("{count}", String(total))}
            </span>
          </div>
        </section>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : rows.length === 0 ? (
          <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
            <p>{hasActiveFilter ? t("designProjects.emptyFiltered") : t("designProjects.empty")}</p>
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
                      <h3 className="break-words text-sm font-semibold leading-tight">{r.name}</h3>
                      <p className="mt-0.5 text-xs text-muted-foreground">
                        {r.projectCode}
                        {r.customerName ? ` · ${r.customerName}` : ""}
                      </p>
                    </div>
                    <div className="flex flex-col items-end gap-1">
                      <Badge variant="outline" className={cn("whitespace-nowrap", STAGE_BADGE[r.currentStage])}>
                        {t(`designProjects.stage.${r.currentStage}`)}
                      </Badge>
                      <Badge variant="outline" className={cn("whitespace-nowrap", STATUS_BADGE[r.status])}>
                        {t(`designProjects.status.${r.status}`)}
                      </Badge>
                    </div>
                  </header>
                  <dl className="mt-2 grid grid-cols-2 gap-2 text-xs">
                    <div>
                      <dt className="text-muted-foreground">{t("designProjects.field.pm")}</dt>
                      <dd className="font-medium">{r.projectManagerName ?? "—"}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("designProjects.field.designLead")}</dt>
                      <dd className="font-medium">{r.designLeadName ?? "—"}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("designProjects.field.startDate")}</dt>
                      <dd className="font-medium">{formatDate(r.startDate, lang)}</dd>
                    </div>
                    <div>
                      <dt className="text-muted-foreground">{t("designProjects.field.deadline")}</dt>
                      <dd className="font-medium">{formatDate(r.deadline, lang)}</dd>
                    </div>
                  </dl>
                  {canManage ? (
                    <div className="mt-2 flex justify-end gap-1.5" onClick={(e) => e.stopPropagation()}>
                      <Button variant="ghost" size="sm" onClick={() => void openEdit(r.id)}>
                        <Pencil className="mr-1 h-3.5 w-3.5" />
                        {t("common.edit")}
                      </Button>
                      {r.currentStage === "Concept" ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="text-rose-700 hover:bg-rose-50 hover:text-rose-800"
                          onClick={() => setDeleting(r)}
                        >
                          <Trash2 className="mr-1 h-3.5 w-3.5" />
                          {t("common.delete")}
                        </Button>
                      ) : null}
                    </div>
                  ) : null}
                </article>
              ))}
            </div>

            {/* Desktop table */}
            <div className="hidden overflow-x-auto rounded-lg border border-slate-200 bg-white shadow-sm lg:block">
              <table className="min-w-full text-sm">
                <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                  <tr>
                    <th className="px-3 py-2">{t("designProjects.field.code")}</th>
                    <th className="px-3 py-2">{t("designProjects.field.name")}</th>
                    <th className="px-3 py-2">{t("designProjects.field.customer")}</th>
                    <th className="px-3 py-2">{t("designProjects.field.pm")}</th>
                    <th className="px-3 py-2">{t("designProjects.field.stage")}</th>
                    <th className="px-3 py-2">{t("designProjects.field.status")}</th>
                    <th className="px-3 py-2">{t("designProjects.field.deadline")}</th>
                    <th className="px-3 py-2 text-right">{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {rows.map((r) => (
                    <tr
                      key={r.id}
                      className="cursor-pointer transition-colors hover:bg-slate-50/70"
                      onClick={() => openDetail(r.id)}
                    >
                      <td className="px-3 py-2 font-mono text-xs text-slate-600">{r.projectCode}</td>
                      <td className="px-3 py-2 font-medium text-slate-900">
                        <div className="flex items-center gap-2">
                          <PenTool className="h-3.5 w-3.5 text-slate-400" />
                          <span className="break-words">{r.name}</span>
                        </div>
                      </td>
                      <td className="px-3 py-2 text-slate-700">{r.customerName ?? "—"}</td>
                      <td className="px-3 py-2 text-slate-700">{r.projectManagerName ?? "—"}</td>
                      <td className="px-3 py-2">
                        <Badge variant="outline" className={cn("whitespace-nowrap", STAGE_BADGE[r.currentStage])}>
                          {t(`designProjects.stage.${r.currentStage}`)}
                        </Badge>
                      </td>
                      <td className="px-3 py-2">
                        <Badge variant="outline" className={cn("whitespace-nowrap", STATUS_BADGE[r.status])}>
                          {t(`designProjects.status.${r.status}`)}
                        </Badge>
                      </td>
                      <td className="px-3 py-2 whitespace-nowrap text-slate-700">
                        {formatDate(r.deadline, lang)}
                      </td>
                      <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                        {canManage ? (
                          <div className="flex justify-end gap-1">
                            <Button variant="ghost" size="sm" onClick={() => void openEdit(r.id)}>
                              <Pencil className="h-4 w-4" />
                            </Button>
                            {r.currentStage === "Concept" ? (
                              <Button
                                variant="ghost"
                                size="sm"
                                className="text-rose-700 hover:bg-rose-50 hover:text-rose-800"
                                onClick={() => setDeleting(r)}
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            ) : null}
                          </div>
                        ) : null}
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

      {/* Create / edit dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{isEdit ? t("designProjects.edit") : t("designProjects.new")}</DialogTitle>
            <DialogDescription>{t("designProjects.form.createHint")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-3 md:grid-cols-2">
            <div className="md:col-span-2">
              <Label>{t("designProjects.field.name")}</Label>
              <Input
                value={form.name}
                onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
                autoFocus
              />
            </div>
            <div>
              <Label>{t("designProjects.field.customer")}</Label>
              <SearchableSelect
                className="mt-1"
                value={form.customerId ? String(form.customerId) : ""}
                onChange={(v) => setForm((f) => ({ ...f, customerId: v ? Number(v) : 0 }))}
                options={customerOptions}
                placeholder={t("designProjects.field.customer")}
              />
            </div>
            <div>
              <Label>{t("designProjects.field.contract")}</Label>
              <SearchableSelect
                className="mt-1"
                value={form.contractId != null ? String(form.contractId) : ""}
                onChange={(v) => setForm((f) => ({ ...f, contractId: v ? Number(v) : null }))}
                options={[{ value: "", label: t("designProjects.form.contractNone") }, ...contractOptions]}
                placeholder={t("designProjects.form.contractNone")}
              />
            </div>
            {canPickUser ? (
              <>
                <div>
                  <Label>{t("designProjects.field.pm")}</Label>
                  <SearchableSelect
                    className="mt-1"
                    value={form.projectManagerUserId != null ? String(form.projectManagerUserId) : ""}
                    onChange={(v) =>
                      setForm((f) => ({ ...f, projectManagerUserId: v ? Number(v) : null }))
                    }
                    options={[{ value: "", label: t("designProjects.form.pmNone") }, ...userOptions]}
                    placeholder={t("designProjects.form.pmNone")}
                  />
                </div>
                <div>
                  <Label>{t("designProjects.field.designLead")}</Label>
                  <SearchableSelect
                    className="mt-1"
                    value={form.designLeadUserId != null ? String(form.designLeadUserId) : ""}
                    onChange={(v) =>
                      setForm((f) => ({ ...f, designLeadUserId: v ? Number(v) : null }))
                    }
                    options={[{ value: "", label: t("designProjects.form.leadNone") }, ...userOptions]}
                    placeholder={t("designProjects.form.leadNone")}
                  />
                </div>
              </>
            ) : null}
            <div>
              <Label>{t("designProjects.field.startDate")}</Label>
              <Input
                type="date"
                value={toDateInputValue(form.startDate)}
                onChange={(e) =>
                  setForm((f) => ({ ...f, startDate: toUtcMidnight(e.target.value) || null }))
                }
              />
            </div>
            <div>
              <Label>{t("designProjects.field.deadline")}</Label>
              <Input
                type="date"
                value={toDateInputValue(form.deadline)}
                onChange={(e) =>
                  setForm((f) => ({ ...f, deadline: toUtcMidnight(e.target.value) || null }))
                }
              />
            </div>
            <div className="md:col-span-2">
              <Label>{t("designProjects.field.note")}</Label>
              <Textarea
                rows={3}
                value={form.note ?? ""}
                onChange={(e) => setForm((f) => ({ ...f, note: e.target.value }))}
              />
            </div>
          </div>
          {formError ? (
            <p className="text-sm text-rose-600">{formError}</p>
          ) : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submitForm()} disabled={saving}>
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete confirmation */}
      <AlertDialog open={!!deleting} onOpenChange={(o) => (!o ? setDeleting(null) : undefined)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("designProjects.delete.confirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("designProjects.delete.confirmBody").replace("{name}", deleting?.name ?? "")}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyDelete}>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              className="bg-rose-600 hover:bg-rose-700"
              onClick={(e) => {
                e.preventDefault();
                void confirmDelete();
              }}
              disabled={busyDelete}
            >
              {t("designProjects.delete.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </AdminLayout>
  );
};

export default AdminDesignProjects;
