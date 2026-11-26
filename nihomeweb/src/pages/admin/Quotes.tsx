import { useUiError } from "@/hooks/useUiError";
import { getUiError } from "@/lib/apiError";
import { FieldScope } from "@/components/ui/field-scope";
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
import { ListPanel, ListTabs, PageHeader, statusTabs } from "@/components/admin/ListLayout";
import { DeletionImpactDialog } from "@/components/admin/DeletionImpactDialog";
import { useI18n } from "@/lib/i18n";
import { useListExport } from "@/hooks/useListExport";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { cn, rowAction } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { useAppSelector } from "@/store";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { parseVnd } from "@/lib/numberFormat";
import { PageError, PageNoMatches, ListSkeleton } from "@/components/PageState";
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
import { formatDate, formatMoney, formatNumber } from "@/lib/displaySettings";

// -------- Static styling --------

const STATUS_STYLES: Record<QuoteStatus, string> = {
  Draft: "border-border bg-muted/30 text-foreground",
  PendingApproval: "border-warning/40 bg-warning-soft text-warning-strong",
  Approved: "border-success/30 bg-success-soft text-success-strong",
  SentToCustomer: "border-info/30 bg-info-soft text-info-strong",
  CustomerApproved: "border-success/40 bg-success-soft text-success-strong",
  Rejected: "border-destructive/20 bg-destructive/10 text-destructive",
  Expired: "border-warning/40 bg-warning-soft text-warning-strong",
  Cancelled: "border-border bg-muted text-muted-foreground",
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
  const uiError = useUiError();
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const currentUserId = useAppSelector((state) => state.auth.user?.userId ?? null);
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const canManage = has(ADMIN_PERMS.quotesManage);
  const canApprove = has(ADMIN_PERMS.quotesApprove);
  const canSend = has(ADMIN_PERMS.quotesSend);

  // ---------- list state ----------
  const [rows, setRows] = useState<QuoteListItemResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [tabCounts, setTabCounts] = useState<Record<string, number>>({});
  const tabCount = Object.values(tabCounts).reduce((sum, value) => sum + value, 0);
  const [filtersOpen, setFiltersOpen] = useState(false);
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

  // The filters shared by the visible page and the export.
  const listFilters = useMemo(() => {
    const params: QuoteListParams = {};
    if (customerIdFilter) params.customerId = customerIdFilter;
    if (statusFilter) params.status = statusFilter;
    if (minValue) params.minValue = Number(minValue);
    if (maxValue) params.maxValue = Number(maxValue);
    if (search.trim()) params.search = search.trim();
    return params;
  }, [customerIdFilter, statusFilter, minValue, maxValue, search]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const { data } = await adminApi.listQuotes({ ...listFilters, page, pageSize });
      setRows(data.items);
      setTotal(data.total);
      setTabCounts(data.statusCounts ?? {});
    } catch (err) {
      setError(uiError(err));
    } finally {
      setLoading(false);
    }
  }, [page, listFilters, uiError]);

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
      setCreateError(uiError(err));
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
        description: uiError(err),
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
      setDeleteError(uiError(err));
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
      setDeleteError(uiError(err));
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

  const { exporting, exportList } = useListExport();
  const exportQuotes = () =>
    exportList<QuoteListItemResponse>({
      filename: "quotes",
      loadPage: (nextPage) =>
        adminApi.listQuotes({ ...listFilters, page: nextPage, pageSize: 100 }),
      columns: [
        { header: t("quotes.field.code"), value: "code" },
        { header: t("quotes.field.opportunity"), value: (q) => q.opportunityName ?? "" },
        { header: t("quotes.field.customer"), value: (q) => q.customerName ?? "" },
        { header: t("quotes.field.method"), value: (q) => t(`quotes.method.${q.method}`) },
        { header: t("quotes.field.grandTotal"), value: "grandTotal" },
        { header: t("common.currency"), value: "currency" },
        { header: t("quotes.field.status"), value: (q) => t(`quotes.status.${q.status}`) },
        { header: t("quotes.field.validUntil"), value: (q) => formatDate(q.validUntil) },
        { header: t("quotes.field.owner"), value: (q) => q.ownerName ?? "" },
        { header: t("quotes.field.version"), value: "version" },
      ],
    });

  return (
    <AdminLayout>
      <div className="space-y-4">
        <PageHeader
          title={t("quotes.title")}
          count={tabCount}
          description={t("quotes.subtitle")}
          actions={
            <>
              <Button
                aria-label={t("common.refresh")}
                variant="outline"
                onClick={() => void fetchList()}
                disabled={loading}
              >
                <RefreshCw className={cn("h-4 w-4", loading && "animate-spin")} />
              </Button>
              <AdminExportButton
                onClick={() => void exportQuotes()}
                disabled={total === 0 || exporting}
                label={exporting ? t("common.exporting") : undefined}
              />
              {canManage && (
                <Button onClick={() => openCreate()}>
                  <Plus className="mr-1.5 h-4 w-4" />
                  {t("quotes.new")}
                </Button>
              )}
            </>
          }
        />

        <ListTabs
          label={t("quotes.field.status")}
          tabs={statusTabs(t("common.all"), QUOTE_STATUSES, tabCounts, statusFilter, (value) =>
            t(`quotes.status.${value}`),
          )}
          value={statusFilter}
          onChange={(next) => {
            setStatusFilter(next);
            setPage(1);
          }}
        />
        <ListPanel
          search={
            <>
              <div className="relative flex-1 min-w-[220px]">
                <Search className="pointer-events-none absolute left-2 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                <Input
                  aria-label={t("common.search")}
                  className="pl-8"
                  placeholder={t("quotes.filter.search")}
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                />
              </div>
            </>
          }
          filterCount={
            [minValue !== "", maxValue !== "", customerIdFilter !== undefined].filter(Boolean)
              .length
          }
          filtersOpen={filtersOpen}
          onFiltersOpenChange={setFiltersOpen}
          onClear={
            searchInput !== "" ||
            statusFilter !== "" ||
            [minValue !== "", maxValue !== "", customerIdFilter !== undefined].some(Boolean)
              ? () => {
                  setSearchInput("");
                  setSearch("");
                  setStatusFilter("");
                  setMinValue("");
                  setMaxValue("");
                  setPage(1);
                  setSearchParams({});
                }
              : undefined
          }
          filters={
            <>
              <div className="space-y-1">
                <Label className="text-xs" htmlFor="quote-minValue">
                  {t("quotes.filter.minValue")}
                </Label>
                <Input
                  id="quote-minValue"
                  aria-label={t("quotes.filter.minValue")}
                  inputMode="numeric"
                  value={minValue ? formatNumber(Number(minValue), lang) : ""}
                  onChange={(e) => setMinValue(String(parseVnd(e.target.value) || ""))}
                />
              </div>
              <div className="space-y-1">
                <Label className="text-xs" htmlFor="quote-maxValue">
                  {t("quotes.filter.maxValue")}
                </Label>
                <Input
                  id="quote-maxValue"
                  aria-label={t("quotes.filter.maxValue")}
                  inputMode="numeric"
                  value={maxValue ? formatNumber(Number(maxValue), lang) : ""}
                  onChange={(e) => setMaxValue(String(parseVnd(e.target.value) || ""))}
                />
              </div>
            </>
          }
        >
          {loading && rows.length === 0 ? (
            <div className="p-3">
              <ListSkeleton />
            </div>
          ) : error ? (
            <PageError message={error} onRetry={() => void fetchList()} />
          ) : rows.length === 0 && Object.keys(listFilters).length > 0 ? (
            <div className="p-3">
              <PageNoMatches
                onClear={() => {
                  setSearchInput("");
                  setSearch("");
                  setStatusFilter("");
                  setMinValue("");
                  setMaxValue("");
                  setPage(1);
                  setSearchParams({});
                }}
              />
            </div>
          ) : rows.length === 0 ? (
            <div className="p-10 text-center">
              <p className="text-muted-foreground">{t("quotes.empty")}</p>
              {canManage && (
                <Button variant="outline" className="mt-4" onClick={() => openCreate()}>
                  <Plus className="mr-1.5 h-4 w-4" />
                  {t("quotes.emptyCta")}
                </Button>
              )}
            </div>
          ) : (
            <>
              {/* Desktop table (xl+ only — sidebar-open laptops still cramp a
              9-col table at lg, so we defer to xl to avoid horizontal scroll). */}
              <div className="hidden overflow-x-auto xl:block">
                <table className="w-full divide-y text-sm">
                  <thead className="bg-muted/40 text-xs text-muted-foreground">
                    <tr>
                      <Th>{t("quotes.field.code")}</Th>
                      <Th>{t("quotes.field.opportunity")}</Th>
                      <Th>{t("quotes.field.customer")}</Th>
                      <Th className="text-right">{t("quotes.field.grandTotal")}</Th>
                      <Th>{t("quotes.field.status")}</Th>
                      <Th>{t("quotes.field.validUntil")}</Th>
                      <Th>{t("quotes.field.owner")}</Th>
                      <Th className="text-right">{t("quotes.field.version")}</Th>
                      <Th className="sticky right-0 z-10 min-w-36 bg-muted/40 text-right shadow-[-8px_0_12px_-10px_rgba(0,0,0,0.25)]">
                        &nbsp;
                      </Th>
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {rows.map((r) => {
                      const isPending = pendingAction === r.id;
                      return (
                        <tr
                          key={r.id}
                          className="cursor-pointer hover:bg-muted/20"
                          {...rowAction(() => navigate(`/admin/quotes/${r.id}`))}
                        >
                          <Td className="whitespace-nowrap" onClick={(e) => e.stopPropagation()}>
                            <Link
                              to={`/admin/quotes/${r.id}`}
                              className="font-medium text-primary hover:underline"
                            >
                              {r.code}
                            </Link>
                          </Td>
                          <Td className="min-w-40">{r.opportunityName ?? "—"}</Td>
                          <Td className="min-w-40">{r.customerName ?? "—"}</Td>
                          <Td className="whitespace-nowrap text-right font-medium">
                            {r.status === "Draft" && r.grandTotal === 0
                              ? t("quotes.inputPrice.pricePending")
                              : formatMoney(r.grandTotal, r.currency)}
                          </Td>
                          <Td>
                            <StatusCell status={r.status} expiring={r.isExpiringSoon} t={t} />
                          </Td>
                          <Td className="whitespace-nowrap text-muted-foreground">
                            {formatDate(r.validUntil)}
                          </Td>
                          <Td className="whitespace-nowrap">{r.ownerName ?? "—"}</Td>
                          <Td className="text-right">V{r.version}</Td>
                          <Td
                            className="sticky right-0 z-10 min-w-36 bg-card text-right shadow-[-8px_0_12px_-10px_rgba(0,0,0,0.25)]"
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
                            />
                          </Td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>

              {/* Card grid (<xl). Single col on phones, 2 cols from sm upward. */}
              <ul className="grid gap-2 sm:grid-cols-2 p-3 xl:hidden">
                {rows.map((r) => {
                  const isPending = pendingAction === r.id;
                  return (
                    <li
                      key={r.id}
                      className="cursor-pointer rounded-lg border bg-card p-3 shadow-sm hover:bg-muted/20"
                      {...rowAction(() => navigate(`/admin/quotes/${r.id}`))}
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
                            : formatMoney(r.grandTotal, r.currency)}
                        </div>
                      </div>
                      <div className="mb-2 flex items-center justify-between text-xs text-muted-foreground">
                        <span>
                          {t("quotes.field.validUntil")}: {formatDate(r.validUntil)}
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
            <div className="flex items-center justify-between border-t p-3 text-sm text-muted-foreground">
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
        </ListPanel>
      </div>

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
            <FieldScope className="min-w-0">
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
            </FieldScope>
            <FieldScope>
              <Label>{t("quotes.field.note")}</Label>
              <Textarea
                rows={3}
                maxLength={4000}
                value={createForm.note ?? ""}
                onChange={(event) => setCreateForm({ ...createForm, note: event.target.value })}
              />
            </FieldScope>
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
  <th className={cn("whitespace-nowrap px-3 py-2 text-left font-medium", className)}>{children}</th>
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
      className={cn(
        "h-8 px-2 text-muted-foreground hover:text-foreground",
        opts.danger && "hover:bg-destructive/10 hover:text-destructive",
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
        className="h-8 px-2 text-muted-foreground hover:text-foreground"
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
        className="inline-flex items-center gap-1 rounded bg-destructive/10 px-1.5 py-0.5 text-xs font-medium text-destructive"
        title={t("quotes.expiringSoon")}
      >
        <AlertTriangle className="h-3 w-3" />
        {t("quotes.expiringSoon")}
      </span>
    )}
  </div>
);

export default AdminQuotes;
