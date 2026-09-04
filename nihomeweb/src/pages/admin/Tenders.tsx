import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { AlertTriangle, Loader2, Pencil, Plus, RefreshCcw, Search, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { PageLoading, PageError } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Textarea } from "@/components/ui/textarea";
import { Progress } from "@/components/ui/progress";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { SearchableSelect } from "@/components/ui/searchable-select";
import {
  adminApi,
  TENDER_STATUSES,
  type CreateTenderRequest,
  type CustomerResponse,
  type DeletionImpactResponse,
  type TenderListItemResponse,
  type TenderListParams,
  type TenderResponse,
  type TenderStatus,
  type UserListItemResponse,
} from "@/services/adminApi";

const ALL_VALUE = "__all__";

const STATUS_BADGE_STYLES: Record<TenderStatus, string> = {
  Preparing: "border-slate-200 bg-slate-50 text-slate-700",
  Submitted: "border-sky-200 bg-sky-50 text-sky-700",
  Won: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Lost: "border-rose-200 bg-rose-50 text-rose-700",
  Cancelled: "border-zinc-200 bg-zinc-100 text-zinc-600",
};

const emptyCreate = (): CreateTenderRequest => ({
  name: "",
  customerId: 0,
  openingDate: null,
  submissionDeadline: "",
  preparerUserId: null,
  infoSource: "",
  note: "",
});

const formatDate = (iso?: string | null, lang: string = "vi") => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

const toDateInputValue = (iso?: string | null) => (iso ? iso.slice(0, 10) : "");
const toDateTimeInputValue = (iso?: string | null) => (iso ? iso.slice(0, 16) : "");

const AdminTenders = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const navigate = useNavigate();
  const canManage = has(ADMIN_PERMS.tendersManage);
  const canPickPreparer = has(ADMIN_PERMS.users);

  // Row-click / card-click on the list opens the NIH-97 detail page. The
  // previous quick-view dialog has been removed since the detail page now
  // covers the same read-only summary + the checklist + result workflow.
  const openDetail = useCallback((id: number) => navigate(`/admin/tenders/${id}`), [navigate]);

  // -------- list state --------
  const [rows, setRows] = useState<TenderListItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const [statusFilter, setStatusFilter] = useState<TenderStatus | "">("");
  const [customerFilter, setCustomerFilter] = useState<number | null>(null);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");

  useEffect(() => {
    const h = window.setTimeout(() => {
      setSearch(searchInput);
      setPage(1);
    }, 350);
    return () => window.clearTimeout(h);
  }, [searchInput]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: TenderListParams = { page, pageSize };
      if (statusFilter) params.status = statusFilter;
      if (customerFilter) params.customerId = customerFilter;
      if (search.trim()) params.search = search.trim();
      const { data } = await adminApi.listTenders(params);
      setRows(data.items);
      setTotal(data.total);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter, customerFilter, search]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  // -------- customers for filter + form --------
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [preparers, setPreparers] = useState<UserListItemResponse[]>([]);
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.listCustomers({ pageSize: 200 });
        if (!cancelled) setCustomers(data.items);
      } catch {
        /* non-fatal — filter/form dropdowns just stay empty */
      }
      if (canPickPreparer) {
        try {
          const { data } = await adminApi.getUsers({ take: 200 });
          if (!cancelled) setPreparers(data.items.filter((u) => u.isActive));
        } catch {
          /* non-fatal — preparer field will be hidden */
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canPickPreparer]);
  const customerOptions = useMemo(
    () => customers.map((c) => ({ value: String(c.id), label: c.name })),
    [customers],
  );

  // -------- create / edit dialog --------
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingDetail, setEditingDetail] = useState<TenderResponse | null>(null);
  const [form, setForm] = useState<CreateTenderRequest>(emptyCreate());
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const isEdit = !!editingDetail;
  const editLocked = isEdit && editingDetail!.status !== "Preparing";

  const openCreate = () => {
    setEditingDetail(null);
    setForm(emptyCreate());
    setFormError(null);
    setDialogOpen(true);
  };

  const openEdit = async (id: number) => {
    setSaving(true);
    try {
      const { data } = await adminApi.getTender(id);
      setEditingDetail(data);
      setForm({
        name: data.name,
        customerId: data.customerId,
        openingDate: data.openingDate ?? null,
        submissionDeadline: data.submissionDeadline,
        preparerUserId: data.preparerUserId ?? null,
        infoSource: data.infoSource ?? "",
        note: data.note ?? "",
      });
      setFormError(null);
      setDialogOpen(true);
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setSaving(false);
    }
  };

  const submitForm = async () => {
    setFormError(null);
    if (!form.customerId) {
      setFormError(t("tenders.form.customerRequired"));
      return;
    }
    if (!form.submissionDeadline) {
      setFormError(t("tenders.form.deadlineRequired"));
      return;
    }
    // For create, deadline must be > now on the client too (server enforces).
    if (!isEdit && new Date(form.submissionDeadline) <= new Date()) {
      setFormError(t("tenders.form.deadlineRequired"));
      return;
    }
    setSaving(true);
    try {
      if (isEdit) {
        await adminApi.updateTender(editingDetail!.id, {
          name: form.name.trim(),
          openingDate: form.openingDate || null,
          submissionDeadline: form.submissionDeadline,
          preparerUserId: form.preparerUserId ?? null,
          infoSource: form.infoSource?.trim() || null,
          note: form.note?.trim() || null,
        });
        toast({ title: t("tenders.updated") });
      } else {
        await adminApi.createTender({
          name: form.name.trim(),
          customerId: form.customerId,
          openingDate: form.openingDate || null,
          submissionDeadline: form.submissionDeadline,
          preparerUserId: form.preparerUserId ?? null,
          infoSource: form.infoSource?.trim() || null,
          note: form.note?.trim() || null,
        });
        toast({ title: t("tenders.created") });
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

  // -------- delete --------
  const [deleting, setDeleting] = useState<TenderListItemResponse | null>(null);
  const [deleteImpact, setDeleteImpact] = useState<DeletionImpactResponse | null>(null);
  const [deleteImpactLoading, setDeleteImpactLoading] = useState(false);
  const [busyDelete, setBusyDelete] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const openDelete = async (tender: TenderListItemResponse) => {
    setDeleting(tender);
    setDeleteImpact(null);
    setDeleteError(null);
    setDeleteImpactLoading(true);
    try {
      const response = await adminApi.getTenderDeletionImpact(tender.id);
      setDeleteImpact(response.data);
    } catch (err) {
      setDeleteError(extractApiError(err));
    } finally {
      setDeleteImpactLoading(false);
    }
  };

  const confirmDelete = async (confirmation: string) => {
    if (!deleting || !deleteImpact) return null;
    setBusyDelete(true);
    setDeleteError(null);
    try {
      const response = await adminApi.deleteTender(deleting.id, {
        planToken: deleteImpact.planToken,
        confirmation,
      });
      return response.status === 204 ? null : response.data;
    } catch (err) {
      setDeleteError(extractApiError(err));
      throw err;
    } finally {
      setBusyDelete(false);
    }
  };

  const completeDelete = async () => {
    setDeleting(null);
    setDeleteImpact(null);
    setDeleteError(null);
    toast({ title: t("tenders.deleted") });
    await fetchList();
  };

  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const statusLabel = (s: TenderStatus) => t(`tenders.status.${s}`);

  const renderRowActions = (r: TenderListItemResponse) => (
    <>
      {canManage && (
        <>
          <Button
            size="icon"
            variant="ghost"
            onClick={() => void openEdit(r.id)}
            title={t("common.edit")}
            aria-label={t("common.edit")}
          >
            <Pencil className="h-4 w-4" />
          </Button>
          <Button
            size="icon"
            variant="ghost"
            className="text-rose-600 hover:bg-rose-50 hover:text-rose-700"
            onClick={() => void openDelete(r)}
            title={t("common.delete")}
            aria-label={t("common.delete")}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </>
      )}
    </>
  );

  return (
    <AdminLayout>
      <div className="space-y-4 md:space-y-6">
        {/* Header */}
        <header className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div className="min-w-0">
            <h1 className="text-xl font-semibold tracking-tight md:text-2xl">{t("tenders.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t("tenders.subtitle")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <Button variant="outline" size="sm" onClick={() => void fetchList()} disabled={loading} className="flex-1 md:flex-none">
              <RefreshCcw className={cn("mr-2 h-4 w-4", loading && "animate-spin")} />
              {t("common.refresh")}
            </Button>
            {canManage && (
              <Button size="sm" onClick={openCreate} className="flex-1 md:flex-none">
                <Plus className="mr-2 h-4 w-4" />
                {t("tenders.new")}
              </Button>
            )}
          </div>
        </header>

        {/* Filter bar */}
        <div className="grid gap-2 md:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_minmax(0,1fr)]">
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              className="pl-9"
              placeholder={t("tenders.filter.search")}
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
            />
          </div>
          <Select
            value={statusFilter || ALL_VALUE}
            onValueChange={(v) => {
              setStatusFilter(v === ALL_VALUE ? "" : (v as TenderStatus));
              setPage(1);
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder={t("tenders.filter.allStatus")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ALL_VALUE}>{t("tenders.filter.allStatus")}</SelectItem>
              {TENDER_STATUSES.map((s) => (
                <SelectItem key={s} value={s}>{statusLabel(s)}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select
            value={customerFilter ? String(customerFilter) : ALL_VALUE}
            onValueChange={(v) => {
              setCustomerFilter(v === ALL_VALUE ? null : Number(v));
              setPage(1);
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder={t("tenders.filter.allCustomers")} />
            </SelectTrigger>
            <SelectContent className="max-h-64">
              <SelectItem value={ALL_VALUE}>{t("tenders.filter.allCustomers")}</SelectItem>
              {customers.map((c) => (
                <SelectItem key={c.id} value={String(c.id)}>{c.name}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* List */}
        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : rows.length === 0 ? (
          <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground">
            <p>{t("tenders.empty")}</p>
            {canManage && (
              <Button size="sm" onClick={openCreate} className="mt-3">
                <Plus className="mr-2 h-4 w-4" />
                {t("tenders.emptyCta")}
              </Button>
            )}
          </div>
        ) : (
          <>
            {/* Mobile / tablet card view */}
            <div className="grid gap-3 lg:hidden">
              {rows.map((r) => {
                return (
                  <article
                    key={r.id}
                    className="cursor-pointer rounded-lg border bg-white p-3 shadow-sm hover:bg-slate-50/70"
                    onClick={() => openDetail(r.id)}
                  >
                    <header className="flex items-start justify-between gap-2">
                      <div className="flex min-w-0 items-start gap-2">
                        <div className="min-w-0">
                          <h3 className="break-words text-sm font-semibold leading-tight">{r.name}</h3>
                          <p className="mt-0.5 text-xs text-muted-foreground">{r.code} · {r.customerName}</p>
                        </div>
                      </div>
                      <Badge className={cn("shrink-0 whitespace-nowrap", STATUS_BADGE_STYLES[r.status])} variant="outline">
                        {statusLabel(r.status)}
                      </Badge>
                    </header>

                    {r.isDeadlineImminent && (
                      <div className="mt-2 flex items-center gap-1 text-xs text-rose-700">
                        <AlertTriangle className="h-3.5 w-3.5" />
                        <span>{t("tenders.badge.deadlineImminent")}</span>
                      </div>
                    )}

                    <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-2 text-xs">
                      <div>
                        <dt className="text-muted-foreground">{t("tenders.field.deadline")}</dt>
                        <dd className={cn("font-medium", r.isDeadlineImminent && "text-rose-700")}>
                          {formatDate(r.submissionDeadline, lang)}
                        </dd>
                      </div>
                      <div>
                        <dt className="text-muted-foreground">{t("tenders.field.openingDate")}</dt>
                        <dd className="font-medium">{formatDate(r.openingDate, lang)}</dd>
                      </div>
                      <div>
                        <dt className="text-muted-foreground">{t("tenders.field.preparer")}</dt>
                        <dd className="font-medium">{r.preparerName ?? "—"}</dd>
                      </div>
                      <div>
                        <dt className="text-muted-foreground">{t("tenders.field.checklist")}</dt>
                        <dd>
                          <div className="flex items-center gap-2">
                            <Progress value={r.checklistCompletionPercent} className="h-1.5" />
                            <span className="text-xs tabular-nums text-muted-foreground">{r.checklistCompletionPercent}%</span>
                          </div>
                        </dd>
                      </div>
                    </dl>

                    <footer
                      className="mt-3 flex items-center justify-between gap-1 border-t pt-2"
                      onClick={(e) => e.stopPropagation()}
                    >
                      <span className="text-xs text-muted-foreground">{formatDate(r.updatedAt, lang)}</span>
                      <div className="flex gap-1">{renderRowActions(r)}</div>
                    </footer>
                  </article>
                );
              })}
            </div>

            {/* Desktop table */}
            <div className="hidden overflow-x-auto rounded-md border lg:block">
              <table className="w-full min-w-[960px] text-sm">
                <thead className="bg-slate-50 text-left text-xs uppercase text-muted-foreground">
                  <tr>
                    <th className="min-w-[220px] px-3 py-2">{t("tenders.field.name")}</th>
                    <th className="min-w-[160px] px-3 py-2">{t("tenders.field.customer")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("tenders.field.deadline")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("tenders.field.status")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("tenders.field.checklist")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("tenders.field.preparer")}</th>
                    <th className="whitespace-nowrap px-3 py-2">{t("tenders.field.updatedAt")}</th>
                    <th className="w-24 px-3 py-2 text-right"> </th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => {
                    return (
                      <tr
                        key={r.id}
                        className="cursor-pointer border-t align-top hover:bg-slate-50/50"
                        onClick={() => openDetail(r.id)}
                      >
                        <td className="min-w-[220px] px-3 py-2">
                          <div className="font-medium">{r.name}</div>
                          <div className="whitespace-nowrap text-xs text-muted-foreground">{r.code}</div>
                        </td>
                        <td className="min-w-[160px] px-3 py-2">{r.customerName}</td>
                        <td className="whitespace-nowrap px-3 py-2">
                          <div className={cn(r.isDeadlineImminent && "font-semibold text-rose-700")}>
                            {formatDate(r.submissionDeadline, lang)}
                          </div>
                          {r.isDeadlineImminent && (
                            <Badge variant="outline" className="mt-1 border-rose-200 bg-rose-50 text-rose-700">
                              {t("tenders.badge.deadlineImminent")}
                            </Badge>
                          )}
                        </td>
                        <td className="whitespace-nowrap px-3 py-2">
                          <Badge className={STATUS_BADGE_STYLES[r.status]} variant="outline">
                            {statusLabel(r.status)}
                          </Badge>
                        </td>
                        <td className="whitespace-nowrap px-3 py-2">
                          <div className="flex items-center gap-2">
                            <Progress value={r.checklistCompletionPercent} className="h-1.5 w-20" />
                            <span className="text-xs tabular-nums text-muted-foreground">{r.checklistCompletionPercent}%</span>
                          </div>
                        </td>
                        <td className="whitespace-nowrap px-3 py-2 text-muted-foreground">{r.preparerName ?? "—"}</td>
                        <td className="whitespace-nowrap px-3 py-2 text-xs text-muted-foreground">{formatDate(r.updatedAt, lang)}</td>
                        <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                          <div className="flex justify-end gap-1">{renderRowActions(r)}</div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </>
        )}

        {/* Pagination */}
        {total > pageSize && (
          <div className="flex flex-col items-center justify-between gap-2 text-sm sm:flex-row">
            <span className="text-muted-foreground">
              {page} / {totalPages} · {total}
            </span>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                ←
              </Button>
              <Button size="sm" variant="outline" disabled={page >= totalPages} onClick={() => setPage(page + 1)}>
                →
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Create / edit dialog */}
      <Dialog open={dialogOpen} onOpenChange={(v) => (setDialogOpen(v), !v && setEditingDetail(null))}>
        <DialogContent className="max-h-[90vh] w-[95vw] max-w-2xl overflow-y-auto sm:w-full">
          <DialogHeader>
            <DialogTitle>
              {isEdit ? editingDetail!.name : t("tenders.new")}
            </DialogTitle>
            <DialogDescription className="text-xs">
              {isEdit ? (
                <>
                  {editingDetail!.code} · {statusLabel(editingDetail!.status)}
                  {editLocked && ` · ${t("tenders.form.readOnlyAfterSubmit")}`}
                </>
              ) : (
                t("tenders.form.createHint")
              )}
            </DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            <div className="space-y-1">
              <Label>{t("tenders.field.name")}</Label>
              <Input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                disabled={editLocked}
              />
            </div>

            <div className="space-y-1">
              <Label>{t("tenders.field.customer")}</Label>
              {isEdit ? (
                <Input value={editingDetail!.customerName} disabled />
              ) : (
                <SearchableSelect
                  value={form.customerId ? String(form.customerId) : ""}
                  onChange={(v) => setForm({ ...form, customerId: Number(v) || 0 })}
                  options={customerOptions}
                  placeholder={t("tenders.form.customerPlaceholder")}
                />
              )}
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <div className="space-y-1">
                <Label>{t("tenders.field.openingDate")}</Label>
                <Input
                  type="date"
                  value={toDateInputValue(form.openingDate)}
                  onChange={(e) => setForm({ ...form, openingDate: e.target.value || null })}
                  disabled={editLocked}
                />
              </div>
              <div className="space-y-1">
                <Label>{t("tenders.field.deadline")}</Label>
                <Input
                  type="datetime-local"
                  value={toDateTimeInputValue(form.submissionDeadline)}
                  onChange={(e) => setForm({ ...form, submissionDeadline: e.target.value })}
                  disabled={editLocked}
                />
              </div>
            </div>

            <div className="grid gap-3 md:grid-cols-2">
              <div className="space-y-1">
                <Label>{t("tenders.field.preparer")}</Label>
                {canPickPreparer ? (
                  <Select
                    value={form.preparerUserId ? String(form.preparerUserId) : ""}
                    onValueChange={(v) => setForm({ ...form, preparerUserId: v ? Number(v) : null })}
                    disabled={editLocked}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder={t("tenders.form.preparerPlaceholder")} />
                    </SelectTrigger>
                    <SelectContent className="max-h-64">
                      {preparers.map((u) => (
                        <SelectItem key={u.id} value={String(u.id)}>
                          {u.fullName || u.phoneNumber}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <Input
                    value={editingDetail?.preparerName ?? t("tenders.form.preparerReadOnlyHint")}
                    disabled
                  />
                )}
              </div>
              <div className="space-y-1">
                <Label>{t("tenders.field.infoSource")}</Label>
                <Input
                  value={form.infoSource ?? ""}
                  onChange={(e) => setForm({ ...form, infoSource: e.target.value })}
                  disabled={editLocked}
                />
              </div>
            </div>

            <div className="space-y-1">
              <Label>{t("tenders.field.note")}</Label>
              <Textarea
                value={form.note ?? ""}
                onChange={(e) => setForm({ ...form, note: e.target.value })}
                rows={2}
              />
            </div>

            {formError && <p className="text-sm text-rose-600">{formError}</p>}
          </div>

          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="ghost" onClick={() => setDialogOpen(false)} disabled={saving} className="w-full sm:w-auto">
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submitForm()} disabled={saving || !canManage} className="w-full sm:w-auto">
              {saving && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <DeletionImpactDialog
        open={deleting != null}
        impact={deleteImpact}
        loading={deleteImpactLoading}
        deleting={busyDelete}
        error={deleteError}
        onOpenChange={(open) => {
          if (!open && !busyDelete) {
            setDeleting(null);
            setDeleteImpact(null);
            setDeleteError(null);
          }
        }}
        onConfirm={confirmDelete}
        onCompleted={completeDelete}
      />
    </AdminLayout>
  );
};

export default AdminTenders;
