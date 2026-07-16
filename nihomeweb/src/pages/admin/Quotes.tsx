import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import {
  AlertTriangle,
  CheckCheck,
  Ban,
  Loader2,
  Pencil,
  Plus,
  RefreshCw,
  Search,
  Send,
  ThumbsUp,
  Trash2,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { formatVnd, parseVnd } from "@/lib/numberFormat";
import { PageLoading, PageError } from "@/components/PageState";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
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
  QUOTE_STATUSES,
  type CreateQuoteRequest,
  type OpportunityResponse,
  type QuoteListItemResponse,
  type QuoteListParams,
  type QuoteMethod,
  type QuoteStatus,
} from "@/services/adminApi";

// -------- Static styling --------

const STATUS_STYLES: Record<QuoteStatus, string> = {
  Draft: "border-slate-200 bg-slate-50 text-slate-700",
  PendingApproval: "border-amber-200 bg-amber-50 text-amber-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  SentToCustomer: "border-sky-200 bg-sky-50 text-sky-700",
  CustomerApproved: "border-green-300 bg-green-100 text-green-800",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
  Expired: "border-orange-200 bg-orange-50 text-orange-700",
  Cancelled: "border-zinc-200 bg-zinc-100 text-zinc-600",
};

// Which workflow buttons make sense given the current status.
const ACTIONS_BY_STATUS: Record<QuoteStatus, Array<"submit" | "approve" | "send" | "cancel" | "delete">> = {
  Draft: ["submit", "delete"],
  PendingApproval: ["approve", "cancel"],
  Approved: ["send", "cancel"],
  SentToCustomer: ["cancel"],
  CustomerApproved: [],
  Rejected: [],
  Expired: ["cancel"],
  Cancelled: [],
};

const emptyCreate = (): CreateQuoteRequest => ({
  opportunityId: 0,
  method: "UnitCost",
  areaSqm: null,
  unitPricePerSqm: null,
  packageDescription: "",
  items: [],
  discountPercent: 0,
  vatPercent: 8,
  validUntil: null,
  note: "",
});

// -------- Component --------

const AdminQuotes = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();

  const canManage = has(ADMIN_PERMS.quotesManage);
  const canApprove = has(ADMIN_PERMS.quotesApprove);
  const canSend = has(ADMIN_PERMS.quotesSend);

  // ---------- list state ----------
  const [rows, setRows] = useState<QuoteListItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // filters
  const [statusFilter, setStatusFilter] = useState<QuoteStatus | "">("");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [minValue, setMinValue] = useState<string>("");
  const [maxValue, setMaxValue] = useState<string>("");

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
      const params: QuoteListParams = { page, pageSize };
      if (statusFilter) params.status = statusFilter;
      if (minValue) params.minValue = Number(minValue);
      if (maxValue) params.maxValue = Number(maxValue);
      if (search.trim()) params.search = search.trim();
      const { data } = await adminApi.listQuotes(params);
      setRows(data.items);
      setTotal(data.total);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter, minValue, maxValue, search]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  // ---------- opportunities dropdown for create form ----------
  const [opportunities, setOpportunities] = useState<OpportunityResponse[]>([]);
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.listOpportunities({ pageSize: 100 });
        if (!cancelled) setOpportunities(data.items);
      } catch {
        /* non-fatal — dropdown will just be empty */
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  // ---------- create dialog ----------
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState<CreateQuoteRequest>(emptyCreate());
  const [saving, setSaving] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const openCreate = () => {
    setCreateForm(emptyCreate());
    setCreateError(null);
    setCreating(true);
  };

  const handleCreate = async () => {
    setCreateError(null);
    if (!createForm.opportunityId) {
      setCreateError(t("quotes.validation.pickOpportunity"));
      return;
    }
    if (createForm.method === "UnitCost") {
      if (!createForm.areaSqm || !createForm.unitPricePerSqm) {
        setCreateError(t("quotes.validation.unitCostRequired"));
        return;
      }
    } else if (!createForm.items || createForm.items.length === 0) {
      setCreateError(t("quotes.validation.boqRequired"));
      return;
    }
    setSaving(true);
    try {
      await adminApi.createQuote({
        ...createForm,
        packageDescription: createForm.packageDescription?.trim() || undefined,
        note: createForm.note?.trim() || undefined,
        validUntil: createForm.validUntil || null,
      });
      toast({ title: t("quotes.created") });
      setCreating(false);
      await fetchList();
    } catch (err) {
      setCreateError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // ---------- inline workflow actions ----------
  const [pendingAction, setPendingAction] = useState<number | null>(null);

  const runAction = async (
    id: number,
    fn: () => Promise<unknown>,
    successKey: string,
  ) => {
    setPendingAction(id);
    try {
      await fn();
      toast({ title: t(successKey) });
      await fetchList();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setPendingAction(null);
    }
  };

  const handleSubmit = (id: number) =>
    runAction(id, () => adminApi.submitQuote(id, {}), "quotes.updated");
  const handleApprove = (id: number) =>
    runAction(id, () => adminApi.approveQuote(id, {}), "quotes.updated");
  const handleSend = (id: number) =>
    runAction(id, () => adminApi.sendQuoteToCustomer(id, {}), "quotes.updated");
  const handleCancel = (id: number) =>
    runAction(
      id,
      () => adminApi.cancelQuote(id, { note: t("quotes.action.cancel") }),
      "quotes.updated",
    );
  const handleDelete = async (id: number) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    await runAction(id, () => adminApi.deleteQuote(id), "quotes.updated");
  };

  const totalPages = useMemo(
    () => (total > 0 ? Math.ceil(total / pageSize) : 1),
    [total],
  );

  return (
    <AdminLayout>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">{t("quotes.title")}</h1>
          <p className="text-sm text-muted-foreground">{t("quotes.subtitle")}</p>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => void fetchList()} disabled={loading}>
            <RefreshCw className={cn("h-4 w-4", loading && "animate-spin")} />
          </Button>
          {canManage && (
            <Button onClick={openCreate}>
              <Plus className="mr-1.5 h-4 w-4" />
              {t("quotes.new")}
            </Button>
          )}
        </div>
      </div>

      {/* Filters */}
      <div className="mb-3 flex flex-wrap items-end gap-2">
        <div className="relative flex-1 min-w-[220px]">
          <Search className="pointer-events-none absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            className="pl-8"
            placeholder={t("quotes.filter.search")}
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
          />
        </div>
        <div className="min-w-[180px]">
          <Select
            value={statusFilter || "__all__"}
            onValueChange={(v) => {
              setStatusFilter(v === "__all__" ? "" : (v as QuoteStatus));
              setPage(1);
            }}
          >
            <SelectTrigger>
              <SelectValue placeholder={t("quotes.filter.status")} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="__all__">{t("quotes.filter.all")}</SelectItem>
              {QUOTE_STATUSES.map((s) => (
                <SelectItem key={s} value={s}>
                  {t(`quotes.status.${s}`)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="w-[160px]">
          <Input
            inputMode="numeric"
            placeholder={t("quotes.filter.minValue")}
            value={minValue ? formatVnd(Number(minValue)) : ""}
            onChange={(e) => setMinValue(String(parseVnd(e.target.value) || ""))}
          />
        </div>
        <div className="w-[160px]">
          <Input
            inputMode="numeric"
            placeholder={t("quotes.filter.maxValue")}
            value={maxValue ? formatVnd(Number(maxValue)) : ""}
            onChange={(e) => setMaxValue(String(parseVnd(e.target.value) || ""))}
          />
        </div>
      </div>

      {loading && rows.length === 0 ? (
        <PageLoading />
      ) : error ? (
        <PageError message={error} onRetry={() => void fetchList()} />
      ) : rows.length === 0 ? (
        <div className="rounded-lg border bg-card p-10 text-center">
          <p className="text-muted-foreground">{t("quotes.empty")}</p>
          {canManage && (
            <Button className="mt-4" onClick={openCreate}>
              <Plus className="mr-1.5 h-4 w-4" />
              {t("quotes.emptyCta")}
            </Button>
          )}
        </div>
      ) : (
        <>
          {/* Desktop table (md+). Below md the layout switches to a card list. */}
          <div className="hidden overflow-x-auto rounded-lg border bg-card md:block">
            <table className="w-full min-w-[960px] divide-y text-sm">
              <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <Th>{t("quotes.field.code")}</Th>
                  <Th>{t("quotes.field.opportunity")}</Th>
                  <Th>{t("quotes.field.customer")}</Th>
                  <Th className="text-right">{t("quotes.field.grandTotal")}</Th>
                  <Th>{t("quotes.field.status")}</Th>
                  <Th>{t("quotes.field.validUntil")}</Th>
                  <Th className="hidden lg:table-cell">{t("quotes.field.owner")}</Th>
                  <Th className="hidden text-right lg:table-cell">{t("quotes.field.version")}</Th>
                  <Th className="w-[220px] text-right">&nbsp;</Th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((r) => {
                  const isPending = pendingAction === r.id;
                  return (
                    <tr key={r.id} className="hover:bg-muted/20">
                      <Td>
                        <Link
                          to={`/admin/quotes/${r.id}`}
                          className="font-medium text-primary hover:underline"
                        >
                          {r.code}
                        </Link>
                      </Td>
                      <Td>{r.opportunityName ?? "—"}</Td>
                      <Td>{r.customerName ?? "—"}</Td>
                      <Td className="whitespace-nowrap text-right font-medium">
                        {formatVnd(r.grandTotal)} ₫
                      </Td>
                      <Td>
                        <StatusCell status={r.status} expiring={r.isExpiringSoon} t={t} />
                      </Td>
                      <Td className="whitespace-nowrap text-muted-foreground">
                        {new Date(r.validUntil).toLocaleDateString()}
                      </Td>
                      <Td className="hidden lg:table-cell">{r.ownerName ?? "—"}</Td>
                      <Td className="hidden text-right lg:table-cell">V{r.version}</Td>
                      <Td className="text-right">
                        <RowActions
                          row={r}
                          canManage={canManage}
                          canApprove={canApprove}
                          canSend={canSend}
                          pending={isPending}
                          onSubmit={handleSubmit}
                          onApprove={handleApprove}
                          onSend={handleSend}
                          onCancel={handleCancel}
                          onDelete={handleDelete}
                          t={t}
                        />
                      </Td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* Mobile card list (<md). Same data, no horizontal scroll. */}
          <ul className="grid gap-2 md:hidden">
            {rows.map((r) => {
              const isPending = pendingAction === r.id;
              return (
                <li
                  key={r.id}
                  className="rounded-lg border bg-card p-3 shadow-sm"
                >
                  <div className="mb-1 flex items-start justify-between gap-2">
                    <Link
                      to={`/admin/quotes/${r.id}`}
                      className="text-base font-semibold text-primary hover:underline"
                    >
                      {r.code}
                    </Link>
                    <span className="whitespace-nowrap text-xs text-muted-foreground">
                      V{r.version}
                    </span>
                  </div>
                  <div className="mb-2 text-sm text-foreground">
                    {r.opportunityName ?? "—"}
                  </div>
                  {r.customerName && (
                    <div className="mb-2 text-xs text-muted-foreground">
                      {r.customerName}
                    </div>
                  )}
                  <div className="mb-2 flex items-center justify-between gap-2">
                    <StatusCell status={r.status} expiring={r.isExpiringSoon} t={t} />
                    <div className="whitespace-nowrap text-sm font-semibold">
                      {formatVnd(r.grandTotal)} ₫
                    </div>
                  </div>
                  <div className="mb-2 flex items-center justify-between text-xs text-muted-foreground">
                    <span>
                      {t("quotes.field.validUntil")}:{" "}
                      {new Date(r.validUntil).toLocaleDateString()}
                    </span>
                    {r.ownerName && <span className="truncate">{r.ownerName}</span>}
                  </div>
                  <div className="-mx-1 flex flex-wrap items-center gap-1 border-t pt-2">
                    <RowActions
                      row={r}
                      canManage={canManage}
                      canApprove={canApprove}
                      canSend={canSend}
                      pending={isPending}
                      onSubmit={handleSubmit}
                      onApprove={handleApprove}
                      onSend={handleSend}
                      onCancel={handleCancel}
                      onDelete={handleDelete}
                      t={t}
                      compact
                    />
                  </div>
                </li>
              );
            })}
          </ul>
        </>
      )}

      {rows.length > 0 && totalPages > 1 && (
        <div className="mt-3 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} / {total}
          </span>
          <div className="flex gap-1">
            <Button
              size="sm"
              variant="outline"
              disabled={page <= 1}
              onClick={() => setPage((p) => Math.max(1, p - 1))}
            >
              {t("common.prev")}
            </Button>
            <Button
              size="sm"
              variant="outline"
              disabled={page >= totalPages}
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
            >
              {t("common.next")}
            </Button>
          </div>
        </div>
      )}

      {/* Create dialog — UnitCost mode inline; BOQ mode surfaces on Detail page (NIH-93). */}
      <Dialog open={creating} onOpenChange={(o) => !o && setCreating(false)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t("quotes.new")}</DialogTitle>
            <DialogDescription>{t("quotes.subtitle")}</DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label>{t("quotes.field.opportunity")}</Label>
              <SearchableSelect
                value={createForm.opportunityId ? String(createForm.opportunityId) : null}
                onChange={(v) => setCreateForm({ ...createForm, opportunityId: Number(v) })}
                options={opportunities.map((o) => ({
                  value: String(o.id),
                  label: o.name,
                  hint: o.customerName,
                  keywords: `${o.customerName ?? ""} ${o.id}`,
                }))}
                placeholder="—"
                searchPlaceholder={t("quotes.filter.search")}
                emptyText={t("quotes.empty")}
              />
            </div>
            <div>
              <Label>{t("quotes.field.method")}</Label>
              <Select
                value={createForm.method}
                onValueChange={(v) =>
                  setCreateForm({ ...createForm, method: v as QuoteMethod })
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="UnitCost">{t("quotes.method.UnitCost")}</SelectItem>
                  <SelectItem value="Boq">{t("quotes.method.Boq")}</SelectItem>
                </SelectContent>
              </Select>
            </div>
            {createForm.method === "UnitCost" ? (
              <>
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <Label>{t("quotes.field.areaSqm")}</Label>
                    <Input
                      inputMode="decimal"
                      value={createForm.areaSqm ?? ""}
                      onChange={(e) =>
                        setCreateForm({
                          ...createForm,
                          areaSqm: e.target.value ? Number(e.target.value) : null,
                        })
                      }
                    />
                  </div>
                  <div>
                    <Label>{t("quotes.field.unitPricePerSqm")}</Label>
                    <Input
                      inputMode="numeric"
                      value={
                        createForm.unitPricePerSqm
                          ? formatVnd(createForm.unitPricePerSqm)
                          : ""
                      }
                      onChange={(e) =>
                        setCreateForm({
                          ...createForm,
                          unitPricePerSqm: parseVnd(e.target.value) || null,
                        })
                      }
                    />
                  </div>
                </div>
                <div>
                  <Label>{t("quotes.field.packageDescription")}</Label>
                  <Textarea
                    rows={2}
                    value={createForm.packageDescription ?? ""}
                    onChange={(e) =>
                      setCreateForm({
                        ...createForm,
                        packageDescription: e.target.value,
                      })
                    }
                  />
                </div>
              </>
            ) : (
              <div className="rounded-md border border-dashed p-3 text-xs text-muted-foreground">
                {t("quotes.validation.boqRequired")}
                <br />
                {/* Slice 2B (NIH-93) will replace this hint with the full inline BOQ editor. */}
                <em>
                  BOQ editor lands next — for now create the quote and edit lines on the detail page.
                </em>
              </div>
            )}
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>{t("quotes.field.discountPercent")}</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={createForm.discountPercent}
                  onChange={(e) =>
                    setCreateForm({
                      ...createForm,
                      discountPercent: Number(e.target.value),
                    })
                  }
                />
              </div>
              <div>
                <Label>{t("quotes.field.vatPercent")}</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={createForm.vatPercent}
                  onChange={(e) =>
                    setCreateForm({
                      ...createForm,
                      vatPercent: Number(e.target.value),
                    })
                  }
                />
              </div>
            </div>
            <div>
              <Label>{t("quotes.field.validUntil")}</Label>
              <Input
                type="date"
                value={createForm.validUntil?.slice(0, 10) ?? ""}
                onChange={(e) =>
                  setCreateForm({
                    ...createForm,
                    validUntil: e.target.value ? `${e.target.value}T23:59:59Z` : null,
                  })
                }
              />
            </div>
            <div>
              <Label>{t("quotes.field.note")}</Label>
              <Textarea
                rows={2}
                value={createForm.note ?? ""}
                onChange={(e) => setCreateForm({ ...createForm, note: e.target.value })}
              />
            </div>
            {createError && (
              <p className="text-sm text-destructive">{createError}</p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void handleCreate()} disabled={saving}>
              {saving && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />}
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

// -------- small local presentation helpers --------

const Th = ({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) => (
  <th className={cn("px-3 py-2 text-left font-medium tracking-wide", className)}>
    {children}
  </th>
);

const Td = ({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) => <td className={cn("px-3 py-2 align-middle", className)}>{children}</td>;

// -------- shared row/card action bar --------

interface RowActionsProps {
  row: QuoteListItemResponse;
  canManage: boolean;
  canApprove: boolean;
  canSend: boolean;
  pending: boolean;
  onSubmit: (id: number) => void;
  onApprove: (id: number) => void;
  onSend: (id: number) => void;
  onCancel: (id: number) => void;
  onDelete: (id: number) => Promise<void> | void;
  t: (k: string) => string;
  /** When true, action labels render alongside the icon for touch targets. */
  compact?: boolean;
}

const RowActions = ({
  row,
  canManage,
  canApprove,
  canSend,
  pending,
  onSubmit,
  onApprove,
  onSend,
  onCancel,
  onDelete,
  t,
  compact,
}: RowActionsProps) => {
  const actions = ACTIONS_BY_STATUS[row.status];
  const btn = (
    label: string,
    icon: React.ReactNode,
    onClick: () => void,
    opts: { danger?: boolean } = {},
  ) => (
    <Button
      variant="ghost"
      size="sm"
      className={cn(
        "h-8 px-2",
        opts.danger && "text-destructive hover:text-destructive",
      )}
      title={label}
      onClick={onClick}
      disabled={pending}
    >
      {icon}
      {compact && <span className="ml-1 text-xs">{label}</span>}
    </Button>
  );
  return (
    <div
      className={cn(
        "flex flex-wrap gap-1",
        compact ? "justify-start" : "justify-end",
      )}
    >
      {canManage && actions.includes("submit") &&
        btn(t("quotes.action.submit"), <ThumbsUp className="h-3.5 w-3.5" />, () => onSubmit(row.id))}
      {canApprove && actions.includes("approve") &&
        btn(t("quotes.action.approve"), <CheckCheck className="h-3.5 w-3.5" />, () => onApprove(row.id))}
      {canSend && actions.includes("send") &&
        btn(t("quotes.action.send"), <Send className="h-3.5 w-3.5" />, () => onSend(row.id))}
      {canManage && actions.includes("cancel") &&
        btn(t("quotes.action.cancel"), <Ban className="h-3.5 w-3.5" />, () => onCancel(row.id))}
      <Button
        asChild
        variant="ghost"
        size="sm"
        className="h-8 px-2"
        title={t("common.edit")}
        disabled={pending}
      >
        <Link to={`/admin/quotes/${row.id}`}>
          <Pencil className="h-3.5 w-3.5" />
          {compact && <span className="ml-1 text-xs">{t("common.edit")}</span>}
        </Link>
      </Button>
      {canManage && actions.includes("delete") &&
        btn(t("quotes.action.delete"), <Trash2 className="h-3.5 w-3.5" />, () => void onDelete(row.id), { danger: true })}
    </div>
  );
};

const StatusCell = ({
  status,
  expiring,
  t,
}: {
  status: QuoteStatus;
  expiring: boolean;
  t: (k: string) => string;
}) => (
  <div className="flex flex-wrap items-center gap-1.5">
    <Badge
      variant="outline"
      className={cn("whitespace-nowrap", STATUS_STYLES[status])}
    >
      {t(`quotes.status.${status}`)}
    </Badge>
    {expiring && (
      <span
        className="inline-flex items-center gap-1 rounded bg-rose-50 px-1.5 py-0.5 text-[10px] font-medium text-rose-700"
        title={t("quotes.expiringSoon")}
      >
        <AlertTriangle className="h-3 w-3" />
        {t("quotes.expiringSoon")}
      </span>
    )}
  </div>
);

export default AdminQuotes;
