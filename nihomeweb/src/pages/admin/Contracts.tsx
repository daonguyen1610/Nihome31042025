import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Pencil, Trash2, Search as SearchIcon, Download, AlertTriangle } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  adminApi,
  CONTRACT_STATUSES,
  type ContractListParams,
  type ContractResponse,
  type ContractStatus,
  type CustomerResponse,
  type UpsertContractRequest,
} from "@/services/adminApi";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { PageLoading, PageError } from "@/components/PageState";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";

// Number of days before end-date that triggers the red "ending soon" badge.
const ENDING_SOON_DAYS = 30;

const STATUS_VARIANT: Record<ContractStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  Signed: "border-blue-200 bg-blue-50 text-blue-700",
  InProgress: "border-emerald-200 bg-emerald-50 text-emerald-700",
  OnHold: "border-amber-200 bg-amber-50 text-amber-700",
  Completed: "border-purple-200 bg-purple-50 text-purple-700",
  Cancelled: "border-rose-200 bg-rose-50 text-rose-700",
};

type FormData = {
  contractNumber: string;
  customerId: number | null;
  status: ContractStatus;
  signedDate: string;
  startDate: string;
  endDate: string;
  value: number;
  scopeOfWork: string;
  note: string;
};

const emptyForm: FormData = {
  contractNumber: "",
  customerId: null,
  status: "Draft",
  signedDate: "",
  startDate: "",
  endDate: "",
  value: 0,
  scopeOfWork: "",
  note: "",
};

const toIsoDate = (value?: string | null): string => {
  if (!value) return "";
  return value.slice(0, 10);
};

const toIsoTimestamp = (value: string): string | null => {
  if (!value) return null;
  const d = new Date(value + "T00:00:00Z");
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
};

const formatCurrency = (value: number, lang: string): string => {
  try {
    return new Intl.NumberFormat(lang === "vi" ? "vi-VN" : "en-US").format(value);
  } catch {
    return value.toString();
  }
};

const formatDate = (value?: string | null): string => {
  if (!value) return "—";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  const yyyy = d.getUTCFullYear();
  const mm = String(d.getUTCMonth() + 1).padStart(2, "0");
  const dd = String(d.getUTCDate()).padStart(2, "0");
  return `${dd}/${mm}/${yyyy}`;
};

const isEndingSoon = (contract: ContractResponse): boolean => {
  if (contract.status !== "InProgress" || !contract.endDate) return false;
  const end = new Date(contract.endDate).getTime();
  if (Number.isNaN(end)) return false;
  const diffDays = (end - Date.now()) / (1000 * 60 * 60 * 24);
  return diffDays <= ENDING_SOON_DAYS;
};

const getErrorMessage = (error: unknown): string | undefined => {
  if (
    typeof error === "object" &&
    error !== null &&
    "response" in error &&
    typeof error.response === "object" &&
    error.response !== null &&
    "data" in error.response &&
    typeof error.response.data === "object" &&
    error.response.data !== null
  ) {
    const data = error.response.data as { detail?: unknown; message?: unknown };
    if (typeof data.detail === "string") return data.detail;
    if (typeof data.message === "string") return data.message;
  }
  return undefined;
};

const Contracts = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.contractsManage);

  const [contracts, setContracts] = useState<ContractResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [statusFilter, setStatusFilter] = useState<ContractStatus | "all">("all");
  const [customerFilter, setCustomerFilter] = useState<number | "all">("all");
  const [signedFrom, setSignedFrom] = useState("");
  const [signedTo, setSignedTo] = useState("");
  const [valueMin, setValueMin] = useState("");
  const [valueMax, setValueMax] = useState("");
  const [search, setSearch] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: ContractListParams = { page: 1, pageSize: 100 };
      if (statusFilter !== "all") params.status = statusFilter;
      if (customerFilter !== "all") params.customerId = customerFilter;
      if (signedFrom) params.signedFrom = toIsoTimestamp(signedFrom) ?? undefined;
      if (signedTo) params.signedTo = toIsoTimestamp(signedTo) ?? undefined;
      const minNum = Number(valueMin);
      if (valueMin && !Number.isNaN(minNum)) params.valueMin = minNum;
      const maxNum = Number(valueMax);
      if (valueMax && !Number.isNaN(maxNum)) params.valueMax = maxNum;
      if (search.trim()) params.search = search.trim();

      const [contractsRes, customersRes] = await Promise.all([
        adminApi.listContracts(params),
        customers.length === 0
          ? adminApi.listCustomers({ pageSize: 200 })
          : Promise.resolve({ data: { total: customers.length, page: 1, pageSize: customers.length, items: customers } }),
      ]);
      setContracts(contractsRes.data.items);
      setTotal(contractsRes.data.total);
      if (customers.length === 0) {
        setCustomers(customersRes.data.items);
      }
    } catch (err) {
      setError(getErrorMessage(err) ?? t("common.error"));
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [statusFilter, customerFilter, signedFrom, signedTo, valueMin, valueMax, search, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const resetFilters = () => {
    setStatusFilter("all");
    setCustomerFilter("all");
    setSignedFrom("");
    setSignedTo("");
    setValueMin("");
    setValueMax("");
    setSearch("");
  };

  const filtersActive =
    statusFilter !== "all" ||
    customerFilter !== "all" ||
    signedFrom !== "" ||
    signedTo !== "" ||
    valueMin !== "" ||
    valueMax !== "" ||
    search !== "";

  // -------- bulk selection --------
  const visibleIds = useMemo(() => contracts.map((c) => c.id), [contracts]);
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
    deleteOne: (id) => adminApi.deleteContract(id),
    onAfter: async () => {
      await load();
    },
  });
  useEffect(() => {
    clearSelection();
    // Clear the bulk selection whenever the filter set changes so a stale
    // selection cannot silently outlive its visible row.
  }, [statusFilter, customerFilter, signedFrom, signedTo, valueMin, valueMax, search, clearSelection]);

  // -------- dialog / form --------
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<FormData>(emptyForm);
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const openCreate = () => {
    setEditingId(null);
    setForm({ ...emptyForm });
    setFormError(null);
    setDialogOpen(true);
  };
  const openEdit = (row: ContractResponse) => {
    setEditingId(row.id);
    setForm({
      contractNumber: row.contractNumber,
      customerId: row.customerId,
      status: row.status,
      signedDate: toIsoDate(row.signedDate),
      startDate: toIsoDate(row.startDate),
      endDate: toIsoDate(row.endDate),
      value: row.value,
      scopeOfWork: row.scopeOfWork ?? "",
      note: row.note ?? "",
    });
    setFormError(null);
    setDialogOpen(true);
  };

  const submit = async () => {
    setFormError(null);
    if (form.customerId == null) {
      setFormError(t("form.required"));
      return;
    }
    if (form.signedDate && form.startDate && form.startDate < form.signedDate) {
      // Sanity check: start date shouldn't be before signed date.
      setFormError(t("form.invalidDateRange"));
      return;
    }
    if (form.startDate && form.endDate && form.endDate < form.startDate) {
      setFormError(t("form.invalidDateRange"));
      return;
    }
    const payload: UpsertContractRequest = {
      contractNumber: form.contractNumber.trim() || null,
      customerId: form.customerId,
      status: form.status,
      signedDate: toIsoTimestamp(form.signedDate),
      startDate: toIsoTimestamp(form.startDate),
      endDate: toIsoTimestamp(form.endDate),
      value: Number.isFinite(form.value) ? Math.max(0, form.value) : 0,
      scopeOfWork: form.scopeOfWork.trim() || null,
      note: form.note.trim() || null,
    };
    setSaving(true);
    try {
      if (editingId != null) {
        await adminApi.updateContract(editingId, payload);
        toast({ title: t("form.updated") });
      } else {
        await adminApi.createContract(payload);
        toast({ title: t("form.created") });
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      setFormError(getErrorMessage(err) ?? t("common.error"));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (row: ContractResponse) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteContract(row.id);
      toast({ title: t("form.deleted") });
      await load();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(err),
        variant: "destructive",
      });
    }
  };

  const exportCsv = () => {
    downloadCsv({
      filename: createCsvFilename("contracts"),
      columns: [
        { header: t("contracts.field.number"), value: "contractNumber" },
        { header: t("contracts.field.customer"), value: (r) => r.customerName ?? "" },
        { header: t("contracts.field.status"), value: (r) => t(`contracts.status.${r.status}`) },
        { header: t("contracts.field.signedDate"), value: (r) => r.signedDate ?? "" },
        { header: t("contracts.field.startDate"), value: (r) => r.startDate ?? "" },
        { header: t("contracts.field.endDate"), value: (r) => r.endDate ?? "" },
        { header: t("contracts.field.value"), value: "value" },
        { header: t("contracts.field.owner"), value: (r) => r.ownerName ?? "" },
      ],
      rows: contracts,
    });
  };

  if (loading && contracts.length === 0) {
    return (
      <AdminLayout>
        <div className="p-4 sm:p-6"><PageLoading /></div>
      </AdminLayout>
    );
  }
  if (error) {
    return (
      <AdminLayout>
        <div className="p-4 sm:p-6"><PageError message={error} onRetry={() => void load()} /></div>
      </AdminLayout>
    );
  }

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0">
            <h1 className="text-2xl font-semibold">{t("contracts.title")}</h1>
            <p className="mt-1 text-sm text-muted-foreground">{t("contracts.subtitle")}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={exportCsv} disabled={contracts.length === 0}>
              <Download className="mr-1.5 h-4 w-4" /> {t("contracts.exportCsv")}
            </Button>
            {canManage && (
              <Button onClick={openCreate}>
                <Plus className="mr-1.5 h-4 w-4" /> {t("contracts.newContract")}
              </Button>
            )}
          </div>
        </header>

        {/* Filters */}
        <section className="grid gap-3 rounded-lg border bg-card p-3 sm:grid-cols-2 lg:grid-cols-4">
          <div className="min-w-0 space-y-1">
            <Label className="text-xs" htmlFor="c-search">{t("common.search")}</Label>
            <div className="relative">
              <SearchIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="c-search"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={t("contracts.searchPlaceholder")}
                className="h-9 pl-9"
              />
            </div>
          </div>
          <div className="min-w-0 space-y-1">
            <Label className="text-xs" htmlFor="c-status">{t("contracts.filter.status")}</Label>
            <Select value={statusFilter} onValueChange={(v) => setStatusFilter(v as ContractStatus | "all")}>
              <SelectTrigger id="c-status" className="h-9"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t("contracts.filter.allStatuses")}</SelectItem>
                {CONTRACT_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>{t(`contracts.status.${s}`)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="min-w-0 space-y-1">
            <Label className="text-xs" htmlFor="c-customer">{t("contracts.field.customer")}</Label>
            <Select
              value={customerFilter === "all" ? "all" : String(customerFilter)}
              onValueChange={(v) => setCustomerFilter(v === "all" ? "all" : Number(v))}
            >
              <SelectTrigger id="c-customer" className="h-9"><SelectValue /></SelectTrigger>
              <SelectContent className="max-h-72">
                <SelectItem value="all">{t("contracts.filter.allCustomers")}</SelectItem>
                {customers.map((c) => (
                  <SelectItem key={c.id} value={String(c.id)}>{c.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="min-w-0 space-y-1">
            <Label className="text-xs">{t("contracts.filter.signedRange")}</Label>
            <div className="flex gap-1">
              <Input type="date" value={signedFrom} onChange={(e) => setSignedFrom(e.target.value)} className="h-9 min-w-0 flex-1" />
              <Input type="date" value={signedTo} onChange={(e) => setSignedTo(e.target.value)} className="h-9 min-w-0 flex-1" />
            </div>
          </div>
          <div className="min-w-0 space-y-1 sm:col-span-2 lg:col-span-2">
            <Label className="text-xs">{t("contracts.filter.valueRange")}</Label>
            <div className="flex gap-1">
              <Input
                type="number" min={0} value={valueMin}
                onChange={(e) => setValueMin(e.target.value)}
                placeholder="min" className="h-9 min-w-0 flex-1"
              />
              <Input
                type="number" min={0} value={valueMax}
                onChange={(e) => setValueMax(e.target.value)}
                placeholder="max" className="h-9 min-w-0 flex-1"
              />
            </div>
          </div>
          <div className="flex items-end gap-2">
            <p className="text-xs italic text-muted-foreground">{contracts.length} / {total}</p>
            {filtersActive && (
              <Button variant="ghost" size="sm" onClick={resetFilters}>
                {t("common.reset")}
              </Button>
            )}
          </div>
        </section>

        {/* List */}
        {contracts.length === 0 ? (
          <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            <div className="rounded-full bg-muted p-3">
              <SearchIcon className="h-5 w-5" aria-hidden />
            </div>
            {filtersActive ? (
              <>
                <p>{t("contracts.noMatch")}</p>
                <Button variant="outline" size="sm" onClick={resetFilters}>{t("common.reset")}</Button>
              </>
            ) : (
              <>
                <p>{t("contracts.empty")}</p>
                {canManage && (
                  <Button size="sm" onClick={openCreate}>
                    <Plus className="mr-1.5 h-4 w-4" /> {t("contracts.newContract")}
                  </Button>
                )}
              </>
            )}
          </div>
        ) : (
          <div className="space-y-2">
            {canManage && (
              <BulkActionBar
                selectedCount={selectedIds.size}
                bulkDeleting={bulkDeleting}
                onClear={clearSelection}
                onBulkDelete={() => void handleBulkDelete()}
              />
            )}

            {/* Mobile / tablet cards (<lg) */}
            <ul className="grid gap-3 lg:hidden">
              {contracts.map((row) => {
                const endingSoon = isEndingSoon(row);
                return (
                  <li key={row.id} className="rounded-lg border bg-card p-3 shadow-sm">
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex min-w-0 items-start gap-2">
                        {canManage && (
                          <span onClick={(e) => e.stopPropagation()} className="pt-0.5">
                            <Checkbox
                              checked={selectedIds.has(row.id)}
                              onCheckedChange={(v) => toggleOne(row.id, v === true)}
                              aria-label={`${t("common.selectAll")} · ${row.contractNumber}`}
                            />
                          </span>
                        )}
                        <div className="min-w-0">
                          <h3 className="break-words text-sm font-semibold leading-tight">{row.customerName ?? "—"}</h3>
                          <p className="mt-0.5 break-all font-mono text-xs text-muted-foreground">{row.contractNumber}</p>
                        </div>
                      </div>
                      <div className="flex flex-col items-end gap-1">
                        <Badge variant="outline" className={STATUS_VARIANT[row.status]}>
                          {t(`contracts.status.${row.status}`)}
                        </Badge>
                        {endingSoon && (
                          <Badge variant="outline" className="border-rose-300 bg-rose-50 text-rose-700" title={t("contracts.deadlineBadgeTitle")}>
                            <AlertTriangle className="mr-1 h-3 w-3" />
                            {t("contracts.deadlineBadge")}
                          </Badge>
                        )}
                      </div>
                    </div>
                    <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
                      <dt className="text-muted-foreground">{t("contracts.field.value")}</dt>
                      <dd className="font-semibold">{formatCurrency(row.value, lang)}</dd>
                      <dt className="text-muted-foreground">{t("contracts.field.signedDate")}</dt>
                      <dd>{formatDate(row.signedDate)}</dd>
                      <dt className="text-muted-foreground">{t("contracts.field.endDate")}</dt>
                      <dd>{formatDate(row.endDate)}</dd>
                      {row.ownerName && (
                        <>
                          <dt className="text-muted-foreground">{t("contracts.field.owner")}</dt>
                          <dd>{row.ownerName}</dd>
                        </>
                      )}
                    </dl>
                    {canManage && (
                      <div className="mt-3 flex flex-wrap items-center justify-end gap-1 border-t pt-2">
                        <Button variant="ghost" size="sm" onClick={() => openEdit(row)}>
                          <Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}
                        </Button>
                        <Button
                          variant="ghost" size="sm"
                          onClick={() => remove(row)}
                          className="text-destructive hover:text-destructive"
                        >
                          <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}
                        </Button>
                      </div>
                    )}
                  </li>
                );
              })}
            </ul>

            {/* Desktop table (lg+) */}
            <div className="hidden overflow-x-auto rounded-lg border lg:block">
              <table className="w-full min-w-[1000px] divide-y text-sm">
                <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                  <tr>
                    {canManage && (
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
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("contracts.field.number")}</th>
                    <th className="min-w-[220px] px-3 py-3 text-left font-medium">{t("contracts.field.customer")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("contracts.field.signedDate")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("contracts.field.endDate")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("contracts.field.value")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("contracts.field.status")}</th>
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("contracts.field.owner")}</th>
                    {canManage && (
                      <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
                    )}
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {contracts.map((row) => {
                    const endingSoon = isEndingSoon(row);
                    return (
                      <tr key={row.id} className="hover:bg-muted/40 transition">
                        {canManage && (
                          <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                            <Checkbox
                              checked={selectedIds.has(row.id)}
                              onCheckedChange={(v) => toggleOne(row.id, v === true)}
                              aria-label={`${t("common.selectAll")} · ${row.contractNumber}`}
                            />
                          </td>
                        )}
                        <td className="whitespace-nowrap px-3 py-3 font-mono text-xs">{row.contractNumber}</td>
                        <td className="min-w-[220px] px-3 py-3 font-medium">{row.customerName ?? "—"}</td>
                        <td className="whitespace-nowrap px-3 py-3">{formatDate(row.signedDate)}</td>
                        <td className="whitespace-nowrap px-3 py-3">
                          <div className="flex items-center gap-2">
                            {formatDate(row.endDate)}
                            {endingSoon && (
                              <Badge
                                variant="outline"
                                className="border-rose-300 bg-rose-50 text-rose-700"
                                title={t("contracts.deadlineBadgeTitle")}
                              >
                                <AlertTriangle className="mr-1 h-3 w-3" />
                                {t("contracts.deadlineBadge")}
                              </Badge>
                            )}
                          </div>
                        </td>
                        <td className="whitespace-nowrap px-3 py-3 text-right font-semibold">
                          {formatCurrency(row.value, lang)}
                        </td>
                        <td className="whitespace-nowrap px-3 py-3">
                          <Badge variant="outline" className={STATUS_VARIANT[row.status]}>
                            {t(`contracts.status.${row.status}`)}
                          </Badge>
                        </td>
                        <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">{row.ownerName ?? "—"}</td>
                        {canManage && (
                          <td className="whitespace-nowrap px-3 py-3 text-right">
                            <div className="inline-flex items-center gap-1">
                              <Button variant="ghost" size="sm" onClick={() => openEdit(row)}>
                                <Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}
                              </Button>
                              <Button
                                variant="ghost" size="sm"
                                onClick={() => remove(row)}
                                className="text-destructive hover:text-destructive"
                              >
                                <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}
                              </Button>
                            </div>
                          </td>
                        )}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>

      {/* Create / edit dialog */}
      <Dialog
        open={dialogOpen}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) {
            setEditingId(null);
            setFormError(null);
          }
        }}
      >
        <DialogContent className="w-[95vw] max-w-xl max-h-[90vh] overflow-y-auto sm:w-full">
          <DialogHeader>
            <DialogTitle>
              {editingId != null ? t("contracts.editModalTitle") : t("contracts.createModalTitle")}
            </DialogTitle>
            <DialogDescription>{t("contracts.numberHint")}</DialogDescription>
          </DialogHeader>

          <div className="space-y-3">
            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="c-number" className="text-xs">{t("contracts.field.number")}</Label>
                <Input
                  id="c-number"
                  value={form.contractNumber}
                  onChange={(e) => setForm({ ...form, contractNumber: e.target.value })}
                  className="h-9 font-mono"
                  placeholder="HD-YYYY-NNNN"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="c-status-form" className="text-xs">{t("contracts.field.status")} *</Label>
                <Select value={form.status} onValueChange={(v) => setForm({ ...form, status: v as ContractStatus })}>
                  <SelectTrigger id="c-status-form" className="h-9"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {CONTRACT_STATUSES.map((s) => (
                      <SelectItem key={s} value={s}>{t(`contracts.status.${s}`)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="c-customer-form" className="text-xs">{t("contracts.field.customer")} *</Label>
              <Select
                value={form.customerId != null ? String(form.customerId) : ""}
                onValueChange={(v) => setForm({ ...form, customerId: Number(v) })}
              >
                <SelectTrigger id="c-customer-form" className="h-9"><SelectValue placeholder="—" /></SelectTrigger>
                <SelectContent className="max-h-72">
                  {customers.map((c) => (
                    <SelectItem key={c.id} value={String(c.id)}>{c.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              <div className="space-y-1.5">
                <Label htmlFor="c-signed" className="text-xs">{t("contracts.field.signedDate")}</Label>
                <Input id="c-signed" type="date" value={form.signedDate}
                  onChange={(e) => setForm({ ...form, signedDate: e.target.value })} className="h-9" />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="c-start" className="text-xs">{t("contracts.field.startDate")}</Label>
                <Input id="c-start" type="date" value={form.startDate}
                  onChange={(e) => setForm({ ...form, startDate: e.target.value })} className="h-9" />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="c-end" className="text-xs">{t("contracts.field.endDate")}</Label>
                <Input id="c-end" type="date" value={form.endDate}
                  onChange={(e) => setForm({ ...form, endDate: e.target.value })} className="h-9" />
              </div>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="c-value" className="text-xs">{t("contracts.field.value")} *</Label>
              <Input
                id="c-value" type="number" min={0} value={form.value}
                onChange={(e) => setForm({ ...form, value: Number(e.target.value) || 0 })}
                className="h-9"
              />
              <p className="text-xs text-muted-foreground">{formatCurrency(form.value, lang)} ₫</p>
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="c-scope" className="text-xs">{t("contracts.field.scope")}</Label>
              <Textarea
                id="c-scope" rows={3} value={form.scopeOfWork}
                onChange={(e) => setForm({ ...form, scopeOfWork: e.target.value })}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="c-note" className="text-xs">{t("contracts.field.note")}</Label>
              <Textarea
                id="c-note" rows={2} value={form.note}
                onChange={(e) => setForm({ ...form, note: e.target.value })}
              />
            </div>

            {formError && <p className="text-sm text-destructive">{formError}</p>}
          </div>

          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={saving}>
              {saving ? t("common.saving") : t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default Contracts;
