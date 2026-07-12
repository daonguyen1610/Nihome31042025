import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Search, Trash2, RefreshCw, LayoutGrid, List, Pencil } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import { formatVnd } from "@/lib/numberFormat";
import { PageLoading, PageError } from "@/components/PageState";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  adminApi,
  OPPORTUNITY_STAGES,
  type CreateOpportunityRequest,
  type CustomerResponse,
  type MasterDataOption,
  type OpportunityActivityType,
  type OpportunityListParams,
  type OpportunityPipelineColumn,
  type OpportunityResponse,
  type OpportunityStage,
  type UpdateOpportunityRequest,
} from "@/services/adminApi";

const ACTIVITY_TYPES: OpportunityActivityType[] = ["Call", "Email", "Meeting", "Note"];

const stageBadgeVariant = (stage: OpportunityStage): "secondary" | "outline" | "default" | "destructive" => {
  switch (stage) {
    case "Won":
      return "default";
    case "Lost":
      return "destructive";
    case "Negotiation":
    case "Proposal":
      return "secondary";
    default:
      return "outline";
  }
};

const emptyCreate = (): CreateOpportunityRequest => ({
  name: "",
  customerId: 0,
  estimatedValue: 0,
  winProbability: 20,
  stage: "Prospecting",
});

const AdminOpportunities = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();

  const canManage = has(ADMIN_PERMS.opportunitiesManage);
  const canSeeAll = has(ADMIN_PERMS.opportunitiesViewAll);

  // ---------- data ----------
  const [rows, setRows] = useState<OpportunityResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [view, setView] = useState<"table" | "pipeline">("table");
  const [pipelineColumns, setPipelineColumns] = useState<OpportunityPipelineColumn[]>([]);
  const [pipelineLoading, setPipelineLoading] = useState(false);

  const [page, setPage] = useState(1);
  const pageSize = 20;

  // filters
  const [stageFilter, setStageFilter] = useState<OpportunityStage | "">("");
  const [customerFilter, setCustomerFilter] = useState<number | "">("");
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

  // masters
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [lostReasons, setLostReasons] = useState<MasterDataOption[]>([]);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: OpportunityListParams = { page, pageSize };
      if (stageFilter) params.stage = stageFilter;
      if (customerFilter) params.customerId = customerFilter;
      if (minValue) params.minValue = Number(minValue);
      if (maxValue) params.maxValue = Number(maxValue);
      if (search.trim()) params.search = search.trim();
      const { data } = await adminApi.listOpportunities(params);
      setRows(data.items);
      setTotal(data.total);
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [page, stageFilter, customerFilter, minValue, maxValue, search]);

  const fetchPipeline = useCallback(async () => {
    setPipelineLoading(true);
    try {
      const { data } = await adminApi.getOpportunityPipeline({
        customerId: customerFilter || undefined,
        minValue: minValue ? Number(minValue) : undefined,
        maxValue: maxValue ? Number(maxValue) : undefined,
      });
      setPipelineColumns(data.columns);
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setPipelineLoading(false);
    }
  }, [customerFilter, minValue, maxValue, toast, t]);

  useEffect(() => {
    if (view === "table") void fetchList();
    else void fetchPipeline();
  }, [view, fetchList, fetchPipeline]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.listCustomers({ pageSize: 100 });
        if (!cancelled) setCustomers(data.items);
      } catch {
        /* non-fatal — dropdown will just be empty */
      }
      try {
        const { data } = await adminApi.getMasterDataOptions("opportunity_lost_reason");
        if (!cancelled) setLostReasons(data);
      } catch {
        /* non-fatal */
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const customerLabel = useMemo(() => {
    const map = new Map<number, string>();
    customers.forEach((c) => map.set(c.id, c.name));
    return map;
  }, [customers]);

  // ---------- create ----------
  const [creating, setCreating] = useState(false);
  const [createForm, setCreateForm] = useState<CreateOpportunityRequest>(emptyCreate());
  const [saving, setSaving] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const openCreate = () => {
    setCreateForm(emptyCreate());
    setCreateError(null);
    setCreating(true);
  };

  const handleCreate = async () => {
    setCreateError(null);
    if (!createForm.name.trim() || !createForm.customerId) {
      setCreateError(t("opportunities.validation.missingFields"));
      return;
    }
    setSaving(true);
    try {
      await adminApi.createOpportunity({
        ...createForm,
        name: createForm.name.trim(),
        note: createForm.note?.trim() || undefined,
        expectedCloseDate: createForm.expectedCloseDate || null,
      });
      toast({ title: t("opportunities.created") });
      setCreating(false);
      await fetchList();
    } catch (err) {
      setCreateError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // ---------- detail + edit ----------
  const [detail, setDetail] = useState<OpportunityResponse | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [editing, setEditing] = useState(false);
  const [editForm, setEditForm] = useState<UpdateOpportunityRequest | null>(null);
  const [savingEdit, setSavingEdit] = useState(false);
  const [activityType, setActivityType] = useState<OpportunityActivityType>("Note");
  const [activityContent, setActivityContent] = useState("");
  const [addingActivity, setAddingActivity] = useState(false);

  // stage change UX
  const [stageTarget, setStageTarget] = useState<OpportunityStage | null>(null);
  const [changingStage, setChangingStage] = useState(false);
  const [lostReason, setLostReason] = useState<string>("");
  const [lostNote, setLostNote] = useState("");
  const [wonQuote, setWonQuote] = useState<string>("");

  const openDetail = async (id: number, options: { startEditing?: boolean } = {}) => {
    setDetailLoading(true);
    setDetail(null);
    setEditing(false);
    setEditForm(null);
    try {
      const { data } = await adminApi.getOpportunity(id);
      setDetail(data);
      if (options.startEditing && canManage) {
        setEditForm({
          name: data.name,
          customerId: data.customerId,
          ownerUserId: data.ownerUserId,
          estimatedValue: data.estimatedValue,
          winProbability: data.winProbability,
          expectedCloseDate: data.expectedCloseDate,
          note: data.note,
        });
        setEditing(true);
      }
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setDetailLoading(false);
    }
  };

  const closeDetail = () => {
    setDetail(null);
    setEditing(false);
    setEditForm(null);
    setActivityContent("");
    setStageTarget(null);
    setLostReason("");
    setLostNote("");
    setWonQuote("");
  };

  const handleSaveEdit = async () => {
    if (!detail || !editForm) return;
    setSavingEdit(true);
    try {
      const { data } = await adminApi.updateOpportunity(detail.id, {
        ...editForm,
        name: editForm.name.trim(),
        note: editForm.note?.trim() || undefined,
        expectedCloseDate: editForm.expectedCloseDate || null,
      });
      setDetail(data);
      setEditing(false);
      toast({ title: t("opportunities.updated") });
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setSavingEdit(false);
    }
  };

  const handleDelete = async (id: number) => {
    if (!window.confirm(t("opportunities.deleteConfirm"))) return;
    try {
      await adminApi.deleteOpportunity(id);
      toast({ title: t("opportunities.deleted") });
      if (detail?.id === id) closeDetail();
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    }
  };

  const handleAddActivity = async () => {
    if (!detail || !activityContent.trim()) return;
    setAddingActivity(true);
    try {
      await adminApi.addOpportunityActivity(detail.id, {
        type: activityType,
        content: activityContent.trim(),
      });
      setActivityContent("");
      const { data } = await adminApi.getOpportunity(detail.id);
      setDetail(data);
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setAddingActivity(false);
    }
  };

  const openStageChange = (target: OpportunityStage) => {
    setStageTarget(target);
    setLostReason("");
    setLostNote("");
    setWonQuote("");
  };

  const handleChangeStage = async () => {
    if (!detail || !stageTarget) return;
    setChangingStage(true);
    try {
      const { data } = await adminApi.changeOpportunityStage(detail.id, {
        targetStage: stageTarget,
        wonQuoteId: stageTarget === "Won" && wonQuote ? Number(wonQuote) : undefined,
        lostReasonCode: stageTarget === "Lost" ? lostReason : undefined,
        lostNote: stageTarget === "Lost" ? lostNote.trim() : undefined,
      });
      setDetail(data);
      setStageTarget(null);
      toast({ title: t("opportunities.updated") });
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
    } finally {
      setChangingStage(false);
    }
  };

  // ---------- bulk ----------
  const visibleIds = useMemo(() => rows.map((r) => r.id), [rows]);
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
    deleteOne: (id) => adminApi.deleteOpportunity(id),
    onAfter: async ({ success }) => {
      if (success > 0 && detail && selectedIds.has(detail.id)) closeDetail();
      await fetchList();
    },
  });

  useEffect(() => {
    clearSelection();
  }, [page, stageFilter, customerFilter, minValue, maxValue, search, clearSelection]);

  // ---------- render ----------
  return (
    <AdminLayout>
      <div className="space-y-6 p-6">
        <header className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 className="text-2xl font-semibold">{t("opportunities.title")}</h1>
            <p className="text-muted-foreground text-sm max-w-3xl">{t("opportunities.subtitle")}</p>
            <p className="text-xs text-muted-foreground mt-1">
              {(canSeeAll ? t("opportunities.totalCount") : t("opportunities.myScopeCount"))
                .replace("{count}", total.toString())}
            </p>
          </div>
          <div className="flex flex-wrap gap-2 items-center">
            <div className="flex rounded-md border overflow-hidden">
              <button
                type="button"
                className={`flex items-center gap-1.5 px-3 py-1.5 text-sm ${view === "table" ? "bg-muted" : ""}`}
                onClick={() => setView("table")}
                aria-label={t("opportunities.viewToggle.table")}
              >
                <List className="h-4 w-4" /> {t("opportunities.viewToggle.table")}
              </button>
              <button
                type="button"
                className={`flex items-center gap-1.5 px-3 py-1.5 text-sm border-l ${view === "pipeline" ? "bg-muted" : ""}`}
                onClick={() => setView("pipeline")}
                aria-label={t("opportunities.viewToggle.pipeline")}
              >
                <LayoutGrid className="h-4 w-4" /> {t("opportunities.viewToggle.pipeline")}
              </button>
            </div>
            <Button variant="outline" size="sm" onClick={() => (view === "table" ? void fetchList() : void fetchPipeline())}>
              <RefreshCw className="mr-1.5 h-4 w-4" /> {t("common.refresh")}
            </Button>
            {canManage && (
              <Button onClick={openCreate}>
                <Plus className="mr-1.5 h-4 w-4" /> {t("opportunities.new")}
              </Button>
            )}
          </div>
        </header>

        <section className="grid gap-3 md:grid-cols-6">
          <div className="relative md:col-span-2">
            <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
            <Input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              placeholder={t("opportunities.searchPlaceholder")}
              className="pl-8"
            />
          </div>
          <div>
            <Label className="text-xs">{t("opportunities.filter.stage")}</Label>
            <Select value={stageFilter || "all"} onValueChange={(v) => { setStageFilter(v === "all" ? "" : (v as OpportunityStage)); setPage(1); }}>
              <SelectTrigger><SelectValue placeholder={t("opportunities.filter.all")} /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t("opportunities.filter.all")}</SelectItem>
                {OPPORTUNITY_STAGES.map((s) => (
                  <SelectItem key={s} value={s}>{t(`opportunities.stage.${s}`)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label className="text-xs">{t("opportunities.filter.customer")}</Label>
            <Select value={customerFilter ? String(customerFilter) : "all"} onValueChange={(v) => { setCustomerFilter(v === "all" ? "" : Number(v)); setPage(1); }}>
              <SelectTrigger><SelectValue placeholder={t("opportunities.filter.all")} /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{t("opportunities.filter.all")}</SelectItem>
                {customers.map((c) => (
                  <SelectItem key={c.id} value={String(c.id)}>{c.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label className="text-xs">{t("opportunities.filter.minValue")}</Label>
            <Input type="number" min={0} value={minValue} onChange={(e) => { setMinValue(e.target.value); setPage(1); }} />
          </div>
          <div>
            <Label className="text-xs">{t("opportunities.filter.maxValue")}</Label>
            <Input type="number" min={0} value={maxValue} onChange={(e) => { setMaxValue(e.target.value); setPage(1); }} />
          </div>
        </section>

        {view === "table" ? (
          loading ? (
            <PageLoading />
          ) : error ? (
            <PageError message={error} onRetry={() => void fetchList()} />
          ) : rows.length === 0 ? (
            <div className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
              {t("opportunities.empty")}
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
              <div className="overflow-x-auto rounded-lg border">
                <table className="min-w-full divide-y text-sm">
                  <thead className="bg-muted/50">
                    <tr>
                      {canManage && (
                        <th className="w-10 px-3 py-2 text-left">
                          <Checkbox
                            checked={allVisibleSelected ? true : someVisibleSelected ? "indeterminate" : false}
                            onCheckedChange={(v) => toggleAllVisible(v === true)}
                            aria-label={t("common.selectAll")}
                          />
                        </th>
                      )}
                      <th className="px-3 py-2 text-left font-medium">{t("opportunities.field.name")}</th>
                      <th className="px-3 py-2 text-left font-medium">{t("opportunities.field.customer")}</th>
                      <th className="px-3 py-2 text-right font-medium">{t("opportunities.field.estimatedValue")}</th>
                      <th className="px-3 py-2 text-left font-medium">{t("opportunities.field.winProbability")}</th>
                      <th className="px-3 py-2 text-left font-medium">{t("opportunities.field.expectedCloseDate")}</th>
                      <th className="px-3 py-2 text-left font-medium">{t("opportunities.field.stage")}</th>
                      {canSeeAll && (
                        <th className="px-3 py-2 text-left font-medium">{t("opportunities.field.owner")}</th>
                      )}
                      {canManage && <th className="px-3 py-2" />}
                    </tr>
                  </thead>
                  <tbody className="divide-y">
                    {rows.map((o) => (
                      <tr
                        key={o.id}
                        className="cursor-pointer hover:bg-muted/40"
                        onClick={() => void openDetail(o.id)}
                      >
                        {canManage && (
                          <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
                            <Checkbox
                              checked={selectedIds.has(o.id)}
                              onCheckedChange={(v) => toggleOne(o.id, v === true)}
                              aria-label={`${t("common.selectAll")} · ${o.name}`}
                            />
                          </td>
                        )}
                        <td className="px-3 py-2 font-medium">{o.name}</td>
                        <td className="px-3 py-2">{o.customerName ?? customerLabel.get(o.customerId) ?? `#${o.customerId}`}</td>
                        <td className="px-3 py-2 text-right tabular-nums">{formatVnd(o.estimatedValue)}</td>
                        <td className="px-3 py-2 text-xs">{o.winProbability}%</td>
                        <td className="px-3 py-2 text-xs text-muted-foreground">
                          {o.expectedCloseDate ? new Date(o.expectedCloseDate).toLocaleDateString() : "—"}
                        </td>
                        <td className="px-3 py-2">
                          <Badge variant={stageBadgeVariant(o.stage)}>{t(`opportunities.stage.${o.stage}`)}</Badge>
                        </td>
                        {canSeeAll && (
                          <td className="px-3 py-2 text-xs text-muted-foreground">{o.ownerName || "—"}</td>
                        )}
                        {canManage && (
                          <td className="px-3 py-2 text-right">
                            <div className="flex justify-end gap-1">
                              <Button
                                variant="ghost"
                                size="icon"
                                onClick={(e) => { e.stopPropagation(); void openDetail(o.id, { startEditing: true }); }}
                                title={t("common.edit")}
                                aria-label={t("common.edit")}
                              >
                                <Pencil className="h-4 w-4" />
                              </Button>
                              <Button
                                variant="ghost"
                                size="icon"
                                onClick={(e) => { e.stopPropagation(); void handleDelete(o.id); }}
                                title={t("common.delete")}
                                aria-label={t("common.delete")}
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </div>
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )
        ) : (
          pipelineLoading ? (
            <PageLoading />
          ) : (
            <div className="grid gap-3 xl:grid-cols-6 lg:grid-cols-3 md:grid-cols-2 grid-cols-1">
              {pipelineColumns.map((col) => (
                <div key={col.stage} className="rounded-lg border bg-muted/20 flex flex-col">
                  <div className="px-3 py-2 border-b bg-muted/40 rounded-t-lg">
                    <div className="text-sm font-medium">{t(`opportunities.stage.${col.stage}`)}</div>
                    <div className="text-xs text-muted-foreground tabular-nums">
                      {t("opportunities.pipeline.total")
                        .replace("{count}", col.count.toString())
                        .replace("{value}", formatVnd(col.totalValue))}
                    </div>
                  </div>
                  <div className="p-2 space-y-2 min-h-[120px]">
                    {col.items.length === 0 ? (
                      <div className="text-xs text-muted-foreground py-6 text-center">
                        {t("opportunities.pipeline.empty")}
                      </div>
                    ) : col.items.map((o) => (
                      <button
                        key={o.id}
                        type="button"
                        className="w-full text-left rounded-md border bg-background p-2 text-xs hover:border-primary transition"
                        onClick={() => void openDetail(o.id)}
                      >
                        <div className="font-medium text-sm truncate">{o.name}</div>
                        <div className="text-muted-foreground truncate">{o.customerName ?? `#${o.customerId}`}</div>
                        <div className="mt-1 flex items-center justify-between">
                          <span className="tabular-nums font-medium">{formatVnd(o.estimatedValue)}</span>
                          <span className="text-muted-foreground">{o.winProbability}%</span>
                        </div>
                      </button>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          )
        )}

        {view === "table" && total > pageSize && (
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>{(page - 1) * pageSize + 1}-{Math.min(page * pageSize, total)} / {total}</span>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={() => setPage((p) => Math.max(1, p - 1))} disabled={page === 1}>‹</Button>
              <Button variant="outline" size="sm" onClick={() => setPage((p) => p + 1)} disabled={page * pageSize >= total}>›</Button>
            </div>
          </div>
        )}
      </div>

      {/* ---------- Create dialog ---------- */}
      <Dialog open={creating} onOpenChange={(o) => !o && setCreating(false)}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{t("opportunities.new")}</DialogTitle>
            <DialogDescription>{t("opportunities.subtitle")}</DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label>{t("opportunities.field.name")}</Label>
              <Input value={createForm.name} onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })} />
            </div>
            <div>
              <Label>{t("opportunities.field.customer")}</Label>
              <Select
                value={createForm.customerId ? String(createForm.customerId) : ""}
                onValueChange={(v) => setCreateForm({ ...createForm, customerId: Number(v) })}
              >
                <SelectTrigger><SelectValue placeholder="—" /></SelectTrigger>
                <SelectContent>
                  {customers.map((c) => (
                    <SelectItem key={c.id} value={String(c.id)}>{c.name}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>{t("opportunities.field.estimatedValue")}</Label>
                <Input
                  type="number"
                  min={0}
                  value={createForm.estimatedValue}
                  onChange={(e) => setCreateForm({ ...createForm, estimatedValue: Number(e.target.value) })}
                />
              </div>
              <div>
                <Label>{t("opportunities.field.winProbability")}</Label>
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={createForm.winProbability}
                  onChange={(e) => setCreateForm({ ...createForm, winProbability: Number(e.target.value) })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>{t("opportunities.field.expectedCloseDate")}</Label>
                <Input
                  type="date"
                  value={createForm.expectedCloseDate?.slice(0, 10) ?? ""}
                  onChange={(e) => setCreateForm({ ...createForm, expectedCloseDate: e.target.value || null })}
                />
              </div>
              <div>
                <Label>{t("opportunities.field.stage")}</Label>
                <Select
                  value={createForm.stage ?? "Prospecting"}
                  onValueChange={(v) => setCreateForm({ ...createForm, stage: v as OpportunityStage })}
                >
                  <SelectTrigger><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {OPPORTUNITY_STAGES.filter((s) => s !== "Won" && s !== "Lost").map((s) => (
                      <SelectItem key={s} value={s}>{t(`opportunities.stage.${s}`)}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <Label>{t("opportunities.field.note")}</Label>
              <Textarea rows={3} value={createForm.note ?? ""} onChange={(e) => setCreateForm({ ...createForm, note: e.target.value })} />
            </div>
            {createError && <p className="text-destructive text-sm">{createError}</p>}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)} disabled={saving}>{t("common.cancel")}</Button>
            <Button onClick={() => void handleCreate()} disabled={saving}>{t("common.save")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ---------- Detail dialog ---------- */}
      <Dialog open={!!detail || detailLoading} onOpenChange={(o) => !o && closeDetail()}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          {detailLoading && !detail ? (
            <>
              <DialogHeader className="sr-only">
                <DialogTitle>{t("common.loading")}</DialogTitle>
              </DialogHeader>
              <PageLoading />
            </>
          ) : detail && (
            <>
              <DialogHeader>
                <DialogTitle className="flex items-center gap-2 flex-wrap">
                  {detail.name}
                  <Badge variant={stageBadgeVariant(detail.stage)}>{t(`opportunities.stage.${detail.stage}`)}</Badge>
                  <span className="text-sm font-normal text-muted-foreground">
                    · {formatVnd(detail.estimatedValue)} · {detail.winProbability}%
                  </span>
                </DialogTitle>
                <DialogDescription>
                  {detail.customerName ?? customerLabel.get(detail.customerId) ?? `#${detail.customerId}`}
                  {detail.ownerName && <> · {detail.ownerName}</>}
                </DialogDescription>
              </DialogHeader>

              <Tabs defaultValue="general">
                <TabsList>
                  <TabsTrigger value="general">{t("opportunities.tab.general")}</TabsTrigger>
                  <TabsTrigger value="timeline">
                    {t("opportunities.tab.timeline")} ({detail.activities.length})
                  </TabsTrigger>
                </TabsList>

                <TabsContent value="general" className="space-y-4 pt-3">
                  {!editing ? (
                    <div className="grid grid-cols-2 gap-4 text-sm">
                      <div>
                        <div className="text-xs text-muted-foreground">{t("opportunities.field.customer")}</div>
                        <div>{detail.customerName ?? `#${detail.customerId}`}</div>
                      </div>
                      <div>
                        <div className="text-xs text-muted-foreground">{t("opportunities.field.owner")}</div>
                        <div>{detail.ownerName ?? (detail.ownerUserId ? `#${detail.ownerUserId}` : "—")}</div>
                      </div>
                      <div>
                        <div className="text-xs text-muted-foreground">{t("opportunities.field.estimatedValue")}</div>
                        <div className="tabular-nums">{formatVnd(detail.estimatedValue)}</div>
                      </div>
                      <div>
                        <div className="text-xs text-muted-foreground">{t("opportunities.field.winProbability")}</div>
                        <div>{detail.winProbability}%</div>
                      </div>
                      <div>
                        <div className="text-xs text-muted-foreground">{t("opportunities.field.expectedCloseDate")}</div>
                        <div>{detail.expectedCloseDate ? new Date(detail.expectedCloseDate).toLocaleDateString() : "—"}</div>
                      </div>
                      <div>
                        <div className="text-xs text-muted-foreground">{t("opportunities.field.createdAt")}</div>
                        <div>{new Date(detail.createdAt).toLocaleString()}</div>
                      </div>
                      <div>
                        <div className="text-xs text-muted-foreground">{t("opportunities.field.updatedAt")}</div>
                        <div>{new Date(detail.updatedAt).toLocaleString()}</div>
                      </div>
                      {detail.closedAt && (
                        <div>
                          <div className="text-xs text-muted-foreground">{t("opportunities.field.closedAt")}</div>
                          <div>{new Date(detail.closedAt).toLocaleString()}</div>
                        </div>
                      )}
                      {detail.stage === "Won" && (detail.wonQuoteId || detail.wonTenderId) && (
                        <div className="col-span-2">
                          <div className="text-xs text-muted-foreground">
                            {detail.wonQuoteId ? t("opportunities.field.wonQuote") : t("opportunities.field.wonTender")}
                          </div>
                          <div>#{detail.wonQuoteId ?? detail.wonTenderId}</div>
                        </div>
                      )}
                      {detail.stage === "Lost" && detail.lostReasonCode && (
                        <>
                          <div>
                            <div className="text-xs text-muted-foreground">{t("opportunities.field.lostReason")}</div>
                            <div>
                              {lostReasons.find((r) => r.code === detail.lostReasonCode)?.name ?? detail.lostReasonCode}
                            </div>
                          </div>
                          {detail.lostNote && (
                            <div className="col-span-2">
                              <div className="text-xs text-muted-foreground">{t("opportunities.field.lostNote")}</div>
                              <div className="whitespace-pre-wrap">{detail.lostNote}</div>
                            </div>
                          )}
                        </>
                      )}
                      {detail.note && (
                        <div className="col-span-2">
                          <div className="text-xs text-muted-foreground">{t("opportunities.field.note")}</div>
                          <div className="whitespace-pre-wrap">{detail.note}</div>
                        </div>
                      )}
                    </div>
                  ) : editForm && (
                    <div className="space-y-3">
                      <div>
                        <Label>{t("opportunities.field.name")}</Label>
                        <Input value={editForm.name} onChange={(e) => setEditForm({ ...editForm, name: e.target.value })} />
                      </div>
                      <div>
                        <Label>{t("opportunities.field.customer")}</Label>
                        <Select
                          value={String(editForm.customerId)}
                          onValueChange={(v) => setEditForm({ ...editForm, customerId: Number(v) })}
                        >
                          <SelectTrigger><SelectValue /></SelectTrigger>
                          <SelectContent>
                            {customers.map((c) => (
                              <SelectItem key={c.id} value={String(c.id)}>{c.name}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>
                      <div className="grid grid-cols-2 gap-3">
                        <div>
                          <Label>{t("opportunities.field.estimatedValue")}</Label>
                          <Input
                            type="number"
                            min={0}
                            value={editForm.estimatedValue}
                            onChange={(e) => setEditForm({ ...editForm, estimatedValue: Number(e.target.value) })}
                          />
                        </div>
                        <div>
                          <Label>{t("opportunities.field.winProbability")}</Label>
                          <Input
                            type="number"
                            min={0}
                            max={100}
                            value={editForm.winProbability}
                            onChange={(e) => setEditForm({ ...editForm, winProbability: Number(e.target.value) })}
                          />
                        </div>
                      </div>
                      <div>
                        <Label>{t("opportunities.field.expectedCloseDate")}</Label>
                        <Input
                          type="date"
                          value={editForm.expectedCloseDate?.slice(0, 10) ?? ""}
                          onChange={(e) => setEditForm({ ...editForm, expectedCloseDate: e.target.value || null })}
                        />
                      </div>
                      <div>
                        <Label>{t("opportunities.field.note")}</Label>
                        <Textarea rows={3} value={editForm.note ?? ""} onChange={(e) => setEditForm({ ...editForm, note: e.target.value })} />
                      </div>
                      <div className="flex gap-2">
                        <Button onClick={() => void handleSaveEdit()} disabled={savingEdit}>{t("common.save")}</Button>
                        <Button variant="outline" onClick={() => setEditing(false)} disabled={savingEdit}>{t("common.cancel")}</Button>
                      </div>
                    </div>
                  )}

                  {/* Stage transition buttons — hidden when in edit mode or already terminal */}
                  {!editing && canManage && detail.stage !== "Won" && detail.stage !== "Lost" && (
                    <div className="rounded-md border p-3 space-y-2 bg-muted/30">
                      <div className="text-sm font-medium">{t("opportunities.stage.change")}</div>
                      <div className="flex flex-wrap gap-2">
                        {OPPORTUNITY_STAGES.filter((s) => s !== detail.stage).map((s) => (
                          <Button
                            key={s}
                            size="sm"
                            variant={s === "Won" ? "default" : s === "Lost" ? "destructive" : "outline"}
                            onClick={() => {
                              if (s === "Won" || s === "Lost") openStageChange(s);
                              else {
                                setStageTarget(s);
                                void (async () => {
                                  setChangingStage(true);
                                  try {
                                    const { data } = await adminApi.changeOpportunityStage(detail.id, { targetStage: s });
                                    setDetail(data);
                                    setStageTarget(null);
                                    toast({ title: t("opportunities.updated") });
                                    await fetchList();
                                  } catch (err) {
                                    toast({ title: t("common.error"), description: extractApiError(err), variant: "destructive" });
                                  } finally {
                                    setChangingStage(false);
                                  }
                                })();
                              }
                            }}
                            disabled={changingStage}
                          >
                            {t(`opportunities.stage.${s}`)}
                          </Button>
                        ))}
                      </div>
                    </div>
                  )}
                </TabsContent>

                <TabsContent value="timeline" className="space-y-3 pt-3">
                  {canManage && (
                    <div className="rounded-md border p-3 space-y-2">
                      <div className="text-sm font-medium">{t("opportunities.activity.new")}</div>
                      <div className="grid grid-cols-1 gap-2 sm:grid-cols-[160px_1fr]">
                        <Select value={activityType} onValueChange={(v) => setActivityType(v as OpportunityActivityType)}>
                          <SelectTrigger><SelectValue /></SelectTrigger>
                          <SelectContent>
                            {ACTIVITY_TYPES.map((tp) => (
                              <SelectItem key={tp} value={tp}>{t(`opportunities.activity.type.${tp}`)}</SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        <Textarea rows={2} value={activityContent} onChange={(e) => setActivityContent(e.target.value)} />
                      </div>
                      <div className="flex justify-end">
                        <Button size="sm" onClick={() => void handleAddActivity()} disabled={addingActivity || !activityContent.trim()}>
                          {t("common.save")}
                        </Button>
                      </div>
                    </div>
                  )}
                  {detail.activities.length === 0 ? (
                    <div className="text-sm text-muted-foreground text-center py-4">—</div>
                  ) : (
                    <div className="space-y-2">
                      {detail.activities.map((a) => (
                        <div key={a.id} className="rounded-md border p-2 text-sm">
                          <div className="flex items-center gap-2 text-xs text-muted-foreground">
                            <Badge variant="outline">{t(`opportunities.activity.type.${a.type}`)}</Badge>
                            <span>{new Date(a.occurredAt).toLocaleString()}</span>
                            {a.createdByName && <span>· {a.createdByName}</span>}
                          </div>
                          <div className="mt-1 whitespace-pre-wrap">{a.content}</div>
                        </div>
                      ))}
                    </div>
                  )}
                </TabsContent>
              </Tabs>

              <DialogFooter>
                <Button variant="outline" onClick={closeDetail}>{t("common.close")}</Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>

      {/* ---------- Won / Lost sub-dialog ---------- */}
      <Dialog open={stageTarget === "Won" || stageTarget === "Lost"} onOpenChange={(o) => !o && setStageTarget(null)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {stageTarget === "Won" ? t("opportunities.stage.won.title") : t("opportunities.stage.lost.title")}
            </DialogTitle>
            <DialogDescription>
              {stageTarget === "Won" ? t("opportunities.stage.won.description") : t("opportunities.stage.lost.description")}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            {stageTarget === "Won" ? (
              <div>
                <Label>{t("opportunities.field.wonQuote")}</Label>
                <Input type="number" min={0} value={wonQuote} onChange={(e) => setWonQuote(e.target.value)} placeholder="—" />
              </div>
            ) : (
              <>
                <div>
                  <Label>{t("opportunities.field.lostReason")}</Label>
                  <Select value={lostReason} onValueChange={setLostReason}>
                    <SelectTrigger><SelectValue placeholder="—" /></SelectTrigger>
                    <SelectContent>
                      {lostReasons.map((r) => (
                        <SelectItem key={r.code} value={r.code}>{r.name}</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label>{t("opportunities.field.lostNote")}</Label>
                  <Textarea rows={3} value={lostNote} onChange={(e) => setLostNote(e.target.value)} />
                </div>
              </>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setStageTarget(null)} disabled={changingStage}>{t("common.cancel")}</Button>
            <Button
              onClick={() => void handleChangeStage()}
              disabled={changingStage || (stageTarget === "Lost" && (!lostReason || !lostNote.trim()))}
            >
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default AdminOpportunities;
