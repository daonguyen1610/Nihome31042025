import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
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
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { useAppSelector } from "@/store";
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
  type QuoteStatus,
  type DeletionImpactResponse,
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
const ACTIONS_BY_STATUS: Record<QuoteStatus, Array<"submit" | "approve" | "send" | "cancel">> = {
  Draft: ["submit"],
  PendingApproval: ["approve", "cancel"],
  Approved: ["send", "cancel"],
  SentToCustomer: ["cancel"],
  CustomerApproved: [],
  Rejected: [],
  Expired: ["cancel"],
  Cancelled: [],
};

type BasicQuoteDraft = Pick<CreateQuoteRequest, "opportunityId" | "note">;

const emptyCreate = (): BasicQuoteDraft => ({
  opportunityId: 0,
  note: "",
});

const canRaiseQuote = (opportunity: OpportunityResponse) =>
  opportunity.operationalProjectId != null &&
  opportunity.stage !== "Lost" &&
  !(opportunity.stage === "Won" && opportunity.wonQuoteId != null);

// -------- Component --------

const AdminQuotes = () => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId ?? null);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

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
  const customerIdParam = Number(searchParams.get("customerId"));
  const customerIdFilter =
    Number.isInteger(customerIdParam) && customerIdParam > 0 ? customerIdParam : undefined;

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
      if (customerIdFilter) params.customerId = customerIdFilter;
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
  }, [page, statusFilter, minValue, maxValue, search, customerIdFilter]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  // ---------- create dialog ----------
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState<BasicQuoteDraft>(emptyCreate());
  const [saving, setSaving] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // ---------- server-backed opportunity picker ----------
  const [opportunities, setOpportunities] = useState<OpportunityResponse[]>([]);
  const [opportunitySearch, setOpportunitySearch] = useState("");
  const [opportunitiesLoading, setOpportunitiesLoading] = useState(false);
  const [opportunitiesLoadFailed, setOpportunitiesLoadFailed] = useState(false);
  useEffect(() => {
    if (!creating) return;
    let cancelled = false;
    const timer = window.setTimeout(async () => {
      setOpportunitiesLoading(true);
      setOpportunitiesLoadFailed(false);
      try {
        const [listResponse, selectedResponse] = await Promise.all([
          adminApi.listOpportunities({
            pageSize: 50,
            quoteEligibleOnly: true,
            search: opportunitySearch.trim() || undefined,
          }),
          createForm.opportunityId
            ? adminApi.getOpportunity(createForm.opportunityId).catch(() => null)
            : Promise.resolve(null),
        ]);
        if (!cancelled) {
          const eligible = listResponse.data.items.filter(canRaiseQuote);
          const selected = selectedResponse?.data;
          setOpportunities(
            selected && canRaiseQuote(selected) && !eligible.some((item) => item.id === selected.id)
              ? [selected, ...eligible]
              : eligible,
          );
        }
      } catch {
        if (!cancelled) {
          setOpportunities([]);
          setOpportunitiesLoadFailed(true);
        }
      } finally {
        if (!cancelled) setOpportunitiesLoading(false);
      }
    }, 300);
    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [creating, opportunitySearch, createForm.opportunityId]);

  const openedFromQuery = useRef(false);
  const openCreate = (opportunityId = 0) => {
    setCreateForm({ ...emptyCreate(), opportunityId });
    setOpportunitySearch("");
    setCreateError(null);
    setCreating(true);
  };

  useEffect(() => {
    if (openedFromQuery.current) return;
    if (searchParams.get("create") !== "1") return;
    // ?create=1 alone (e.g. returning from catalog setup) opens an empty form;
    // with an opportunityId it is preselected.
    const opportunityId = Number(searchParams.get("opportunityId"));
    openedFromQuery.current = true;
    openCreate(Number.isInteger(opportunityId) && opportunityId > 0 ? opportunityId : 0);
  }, [searchParams]);

  const handleCreate = async () => {
    setCreateError(null);
    if (!createForm.opportunityId) {
      setCreateError(t("quotes.validation.pickOpportunity"));
      return;
    }
    setSaving(true);
    try {
      const { data } = await adminApi.createQuote({
        opportunityId: createForm.opportunityId,
        note: createForm.note?.trim() || undefined,
        discountPercent: 0,
        vatPercent: 8,
        draftOnly: true,
      });
      toast({ title: t("quotes.created") });
      setCreating(false);
      navigate(`/admin/quotes/${data.id}`);
    } catch (err) {
      setCreateError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };
  // ---------- inline workflow actions ----------
  const [pendingAction, setPendingAction] = useState<number | null>(null);

  const runAction = async (id: number, fn: () => Promise<unknown>, successKey: string) => {
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

  const rowVersionFor = (id: number) => rows.find((row) => row.id === id)?.rowVersion;
  const handleSubmit = (id: number) => navigate(`/admin/quotes/${id}`);
  const handleApprove = (id: number) => navigate(`/admin/quotes/${id}`);
  const handleSend = (id: number) => navigate(`/admin/quotes/${id}`);
  const handleCancel = (id: number) =>
    runAction(
      id,
      () =>
        adminApi.cancelQuote(id, {
          note: t("quotes.action.cancel"),
          rowVersion: rowVersionFor(id),
        }),
      "quotes.updated",
    );
  const [deletingQuote, setDeletingQuote] = useState<QuoteListItemResponse | null>(null);
  const [deleteImpact, setDeleteImpact] = useState<DeletionImpactResponse | null>(null);
  const [deleteImpactLoading, setDeleteImpactLoading] = useState(false);
  const [deleteBusy, setDeleteBusy] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const handleDelete = async (id: number) => {
    const quote = rows.find((row) => row.id === id);
    if (!quote) return;
    setDeletingQuote(quote);
    setDeleteImpact(null);
    setDeleteError(null);
    setDeleteImpactLoading(true);
    try {
      const response = await adminApi.getQuoteDeletionImpact(id);
      setDeleteImpact(response.data);
    } catch (err) {
      setDeleteError(extractApiError(err));
    } finally {
      setDeleteImpactLoading(false);
    }
  };

  const confirmDelete = async (confirmation: string) => {
    if (!deletingQuote || !deleteImpact) return null;
    setDeleteBusy(true);
    setDeleteError(null);
    try {
      const response = await adminApi.deleteQuote(deletingQuote.id, {
        planToken: deleteImpact.planToken,
        confirmation,
        rowVersion: deletingQuote.rowVersion,
      });
      return response.status === 204 ? null : response.data;
    } catch (err) {
      setDeleteError(extractApiError(err));
      throw err;
    } finally {
      setDeleteBusy(false);
    }
  };

  const completeDelete = async () => {
    setDeletingQuote(null);
    setDeleteImpact(null);
    setDeleteError(null);
    toast({ title: t("quotes.deleted") });
    await fetchList();
  };

  const totalPages = useMemo(() => (total > 0 ? Math.ceil(total / pageSize) : 1), [total]);

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
            <Button onClick={() => openCreate()}>
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
            <Button className="mt-4" onClick={() => openCreate()}>
              <Plus className="mr-1.5 h-4 w-4" />
              {t("quotes.emptyCta")}
            </Button>
          )}
        </div>
      ) : (
        <>
          {/* Desktop table (xl+ only — sidebar-open laptops still cramp a
              9-col table at lg, so we defer to xl to avoid horizontal scroll). */}
          <div className="hidden overflow-x-auto rounded-lg border bg-card xl:block">
            <table className="w-full min-w-[900px] divide-y text-sm">
              <thead className="bg-muted/40 text-xs uppercase text-muted-foreground">
                <tr>
                  <Th>{t("quotes.field.code")}</Th>
                  <Th>{t("quotes.field.opportunity")}</Th>
                  <Th>{t("quotes.field.customer")}</Th>
                  <Th className="text-right">{t("quotes.field.grandTotal")}</Th>
                  <Th>{t("quotes.field.status")}</Th>
                  <Th>{t("quotes.field.validUntil")}</Th>
                  <Th>{t("quotes.field.owner")}</Th>
                  <Th className="text-right">{t("quotes.field.version")}</Th>
                  <Th className="w-[220px] text-right">&nbsp;</Th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {rows.map((r) => {
                  const isPending = pendingAction === r.id;
                  return (
                    <tr
                      key={r.id}
                      className="cursor-pointer hover:bg-muted/20"
                      onClick={() => navigate(`/admin/quotes/${r.id}`)}
                    >
                      <Td className="whitespace-nowrap" onClick={(e) => e.stopPropagation()}>
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
                        {r.status === "Draft" && r.grandTotal === 0
                          ? t("quotes.inputPrice.pricePending")
                          : `${formatVnd(r.grandTotal)} ₫`}
                      </Td>
                      <Td>
                        <StatusCell status={r.status} expiring={r.isExpiringSoon} t={t} />
                      </Td>
                      <Td className="whitespace-nowrap text-muted-foreground">
                        {new Date(r.validUntil).toLocaleDateString()}
                      </Td>
                      <Td>{r.ownerName ?? "—"}</Td>
                      <Td className="text-right">V{r.version}</Td>
                      <Td className="text-right" onClick={(e) => e.stopPropagation()}>
                        <RowActions
                          row={r}
                          canManage={canManage}
                          canApprove={canApprove && r.approvalApproverUserId === currentUserId}
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

          {/* Card grid (<xl). Single col on phones, 2 cols from sm upward. */}
          <ul className="grid gap-2 sm:grid-cols-2 xl:hidden">
            {rows.map((r) => {
              const isPending = pendingAction === r.id;
              return (
                <li
                  key={r.id}
                  className="cursor-pointer rounded-lg border bg-card p-3 shadow-sm hover:bg-muted/20"
                  onClick={() => navigate(`/admin/quotes/${r.id}`)}
                >
                  <div className="mb-1 flex items-start justify-between gap-2">
                    <div className="flex min-w-0 items-start gap-2">
                      <Link
                        to={`/admin/quotes/${r.id}`}
                        className="text-base font-semibold text-primary hover:underline"
                        onClick={(e) => e.stopPropagation()}
                      >
                        {r.code}
                      </Link>
                    </div>
                    <span className="whitespace-nowrap text-xs text-muted-foreground">
                      V{r.version}
                    </span>
                  </div>
                  <div className="mb-2 text-sm text-foreground">{r.opportunityName ?? "—"}</div>
                  {r.customerName && (
                    <div className="mb-2 text-xs text-muted-foreground">{r.customerName}</div>
                  )}
                  <div className="mb-2 flex items-center justify-between gap-2">
                    <StatusCell status={r.status} expiring={r.isExpiringSoon} t={t} />
                    <div className="whitespace-nowrap text-sm font-semibold">
                      {r.status === "Draft" && r.grandTotal === 0
                        ? t("quotes.inputPrice.pricePending")
                        : `${formatVnd(r.grandTotal)} ₫`}
                    </div>
                  </div>
                  <div className="mb-2 flex items-center justify-between text-xs text-muted-foreground">
                    <span>
                      {t("quotes.field.validUntil")}: {new Date(r.validUntil).toLocaleDateString()}
                    </span>
                    {r.ownerName && <span className="truncate">{r.ownerName}</span>}
                  </div>
                  <div
                    className="-mx-1 flex flex-wrap items-center gap-1 border-t pt-2"
                    onClick={(e) => e.stopPropagation()}
                  >
                    <RowActions
                      row={r}
                      canManage={canManage}
                      canApprove={canApprove && r.approvalApproverUserId === currentUserId}
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

      {/* Start with the Opportunity; output pricing belongs in detail after Procurement. */}
      <Dialog open={creating} onOpenChange={(open) => !open && setCreating(false)}>
        <DialogContent
          className="min-w-0 w-[calc(100vw-1rem)] max-w-xl overflow-x-hidden"
          data-testid="quote-create-dialog"
        >
          <DialogHeader className="min-w-0">
            <DialogTitle>{t("quotes.new")}</DialogTitle>
            <DialogDescription>{t("quotes.createBasicHint")}</DialogDescription>
          </DialogHeader>
          <div className="min-w-0 space-y-4">
            <div className="min-w-0">
              <Label>{t("quotes.field.opportunity")}</Label>
              <SearchableSelect
                className="min-w-0"
                value={createForm.opportunityId ? String(createForm.opportunityId) : null}
                onChange={(value) => setCreateForm({ ...createForm, opportunityId: Number(value) })}
                options={opportunities.map((opportunity) => ({
                  value: String(opportunity.id),
                  label: `#${opportunity.id} · ${opportunity.name}`,
                  hint: [opportunity.customerName, opportunity.ownerName]
                    .filter(Boolean)
                    .join(" · "),
                }))}
                placeholder={t("quotes.opportunityPicker.placeholder")}
                searchPlaceholder={t("quotes.opportunityPicker.search")}
                emptyText={t("quotes.opportunityPicker.empty")}
                searchValue={opportunitySearch}
                onSearchValueChange={setOpportunitySearch}
                shouldFilter={false}
                loading={opportunitiesLoading}
                loadingText={t("common.loading")}
              />
              {opportunitiesLoadFailed && (
                <p className="mt-1 text-xs text-destructive">
                  {t("quotes.opportunityPicker.loadError")}
                </p>
              )}
              <p className="mt-1 text-xs text-muted-foreground">
                {t("quotes.opportunityPicker.hint")}
              </p>
            </div>
            <div>
              <Label>{t("quotes.field.note")}</Label>
              <Textarea
                rows={3}
                maxLength={4000}
                value={createForm.note ?? ""}
                onChange={(event) => setCreateForm({ ...createForm, note: event.target.value })}
              />
            </div>
            {createError && <p className="text-sm text-destructive">{createError}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              data-testid="quote-create-save"
              onClick={() => void handleCreate()}
              disabled={saving}
            >
              {saving && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />}
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <DeletionImpactDialog
        open={deletingQuote !== null}
        impact={deleteImpact}
        loading={deleteImpactLoading}
        deleting={deleteBusy}
        error={deleteError}
        onOpenChange={(open) => {
          if (!open && !deleteBusy) {
            setDeletingQuote(null);
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

// -------- small local presentation helpers --------

const Th = ({ children, className }: { children: React.ReactNode; className?: string }) => (
  <th className={cn("px-3 py-2 text-left font-medium tracking-wide", className)}>{children}</th>
);

const Td = ({
  children,
  className,
  onClick,
}: {
  children: React.ReactNode;
  className?: string;
  onClick?: React.MouseEventHandler<HTMLTableCellElement>;
}) => (
  <td className={cn("px-3 py-2 align-middle", className)} onClick={onClick}>
    {children}
  </td>
);

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
  const actions = ACTIONS_BY_STATUS[row.status].filter(
    (action) => action !== "submit" || row.grandTotal > 0,
  );
  const btn = (
    label: string,
    icon: React.ReactNode,
    onClick: () => void,
    opts: { danger?: boolean } = {},
  ) => (
    <Button
      variant="ghost"
      size="sm"
      className={cn("h-8 px-2", opts.danger && "text-destructive hover:text-destructive")}
      title={label}
      onClick={onClick}
      disabled={pending}
    >
      {icon}
      {compact && <span className="ml-1 text-xs">{label}</span>}
    </Button>
  );
  return (
    <div className={cn("flex flex-wrap gap-1", compact ? "justify-start" : "justify-end")}>
      {canManage &&
        actions.includes("submit") &&
        btn(t("quotes.action.submit"), <ThumbsUp className="h-3.5 w-3.5" />, () =>
          onSubmit(row.id),
        )}
      {canApprove &&
        actions.includes("approve") &&
        btn(t("quotes.action.approve"), <CheckCheck className="h-3.5 w-3.5" />, () =>
          onApprove(row.id),
        )}
      {canSend &&
        actions.includes("send") &&
        btn(t("quotes.action.send"), <Send className="h-3.5 w-3.5" />, () => onSend(row.id))}
      {canManage &&
        actions.includes("cancel") &&
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
      {canManage &&
        btn(
          t("quotes.action.delete"),
          <Trash2 className="h-3.5 w-3.5" />,
          () => void onDelete(row.id),
          { danger: true },
        )}
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
    <Badge variant="outline" className={cn("whitespace-nowrap", STATUS_STYLES[status])}>
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
