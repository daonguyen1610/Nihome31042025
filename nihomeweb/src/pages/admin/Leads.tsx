import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Search, Trash2, ArrowRight, RefreshCw } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
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
import {
  adminApi,
  type CreateLeadRequest,
  type LeadListParams,
  type LeadResponse,
  type LeadStatus,
  type LeadActivityType,
  type MasterDataOption,
} from "@/services/adminApi";

const STATUSES: LeadStatus[] = [
  "New",
  "Contacted",
  "Interested",
  "NotInterested",
  "Converted",
  "Junk",
];
const ACTIVITY_TYPES: LeadActivityType[] = ["Call", "Email", "Meeting", "Note"];

// Soft-colored pills so every status looks equally weighted in the table.
// Kept as tailwind class strings so we don't need extra Badge variants.
const LEAD_STATUS_STYLES: Record<LeadStatus, string> = {
  New: "border-sky-200 bg-sky-50 text-sky-700",
  Contacted: "border-amber-200 bg-amber-50 text-amber-700",
  Interested: "border-emerald-200 bg-emerald-50 text-emerald-700",
  NotInterested: "border-slate-200 bg-slate-100 text-slate-600",
  Converted: "border-green-300 bg-green-100 text-green-800",
  Junk: "border-rose-200 bg-rose-50 text-rose-700",
};

const LEAD_STATUS_DOT: Record<LeadStatus, string> = {
  New: "bg-sky-500",
  Contacted: "bg-amber-500",
  Interested: "bg-emerald-500",
  NotInterested: "bg-slate-400",
  Converted: "bg-green-600",
  Junk: "bg-rose-500",
};

const emptyCreate: CreateLeadRequest = {
  name: "",
  companyName: "",
  phone: "",
  email: "",
  sourceCode: "",
  note: "",
};

const AdminLeads = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();

  const canConvert = has(ADMIN_PERMS.leadsConvert);
  const canSeeAll = has(ADMIN_PERMS.leadsViewAll);
  const canManage = has(ADMIN_PERMS.leadsManage);

  const [leads, setLeads] = useState<LeadResponse[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [sources, setSources] = useState<MasterDataOption[]>([]);
  const [statusFilter, setStatusFilter] = useState<LeadStatus | "">("");
  const [sourceFilter, setSourceFilter] = useState<string>("");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Debounce search input by 350ms so the list doesn't refetch on every keystroke.
  useEffect(() => {
    const handle = window.setTimeout(() => {
      setSearch(searchInput);
      setPage(1);
    }, 350);
    return () => window.clearTimeout(handle);
  }, [searchInput]);

  const [creating, setCreating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [createForm, setCreateForm] = useState<CreateLeadRequest>(emptyCreate);
  const [createError, setCreateError] = useState<string | null>(null);

  const [detail, setDetail] = useState<LeadResponse | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [activityType, setActivityType] = useState<LeadActivityType>("Call");
  const [activityContent, setActivityContent] = useState("");
  const [addingActivity, setAddingActivity] = useState(false);
  const [convertOpen, setConvertOpen] = useState(false);
  const [converting, setConverting] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const params: LeadListParams = { page, pageSize };
      if (statusFilter) params.status = statusFilter;
      if (sourceFilter) params.sourceCode = sourceFilter;
      if (search.trim()) params.search = search.trim();
      const { data } = await adminApi.listLeads(params);
      setLeads(data.items);
      setTotal(data.total);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  }, [page, statusFilter, sourceFilter, search]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);


  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.getMasterDataOptions("customer_source");
        if (!cancelled) setSources(data);
      } catch {
        // Non-fatal — form will still show but with an empty source dropdown.
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const openDetail = async (id: number) => {
    setDetailLoading(true);
    setDetail(null);
    try {
      const { data } = await adminApi.getLead(id);
      setDetail(data);
    } catch (err) {
      toast({ title: t("common.error"), description: (err as Error).message, variant: "destructive" });
    } finally {
      setDetailLoading(false);
    }
  };

  const closeDetail = () => {
    setDetail(null);
    setActivityContent("");
  };

  const handleCreate = async () => {
    setCreateError(null);
    if (!createForm.name.trim() || !createForm.sourceCode) {
      setCreateError(t("leads.validation.missingFields"));
      return;
    }
    if (!createForm.phone?.trim() && !createForm.email?.trim()) {
      setCreateError(t("leads.field.contactRequired"));
      return;
    }
    setSaving(true);
    try {
      await adminApi.createLead({
        name: createForm.name.trim(),
        companyName: createForm.companyName?.trim() || undefined,
        phone: createForm.phone?.trim() || undefined,
        email: createForm.email?.trim() || undefined,
        sourceCode: createForm.sourceCode,
        note: createForm.note?.trim() || undefined,
      });
      toast({ title: t("leads.created") });
      setCreating(false);
      setCreateForm(emptyCreate);
      await fetchList();
    } catch (err) {
      setCreateError((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async (id: number) => {
    if (!window.confirm(t("leads.deleteConfirm"))) return;
    try {
      await adminApi.deleteLead(id);
      toast({ title: t("leads.deleted") });
      await fetchList();
      if (detail?.id === id) closeDetail();
    } catch (err) {
      toast({ title: t("common.error"), description: (err as Error).message, variant: "destructive" });
    }
  };

  const visibleIds = useMemo(() => leads.map((l) => l.id), [leads]);
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
    deleteOne: (id) => adminApi.deleteLead(id),
    onAfter: async ({ success }) => {
      if (success > 0 && detail && selectedIds.has(detail.id)) closeDetail();
      await fetchList();
    },
  });

  useEffect(() => {
    clearSelection();
  }, [page, statusFilter, sourceFilter, search, clearSelection]);

  const handleAddActivity = async () => {
    if (!detail || !activityContent.trim()) return;
    setAddingActivity(true);
    try {
      await adminApi.addLeadActivity(detail.id, { type: activityType, content: activityContent.trim() });
      setActivityContent("");
      await openDetail(detail.id);
    } catch (err) {
      toast({ title: t("common.error"), description: (err as Error).message, variant: "destructive" });
    } finally {
      setAddingActivity(false);
    }
  };

  const handleConvert = async () => {
    if (!detail) return;
    setConverting(true);
    try {
      await adminApi.convertLead(detail.id);
      toast({ title: t("leads.convert.done") });
      setConvertOpen(false);
      await openDetail(detail.id);
      await fetchList();
    } catch (err) {
      toast({ title: t("common.error"), description: (err as Error).message, variant: "destructive" });
    } finally {
      setConverting(false);
    }
  };

  const sourceLabelByCode = useMemo(() => {
    const map = new Map<string, string>();
    sources.forEach((s) => map.set(s.code, s.name));
    return map;
  }, [sources]);

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("leads.title")}</h1>
            <p className="text-sm text-muted-foreground">{t("leads.subtitle")}</p>
            <p className="text-xs text-muted-foreground italic mt-1">
              {(canSeeAll ? t("leads.totalCount") : t("leads.myScopeCount")).replace(
                "{count}",
                total.toString(),
              )}
            </p>
          </div>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" onClick={() => void fetchList()}>
              <RefreshCw className="mr-1.5 h-4 w-4" /> {t("common.refresh")}
            </Button>
            <Button onClick={() => setCreating(true)}>
              <Plus className="mr-1.5 h-4 w-4" /> {t("leads.new")}
            </Button>
          </div>
        </header>

        <section className="flex flex-wrap items-end gap-3 rounded-lg border bg-card p-3">
          <div className="min-w-[220px] flex-1">
            <Label className="text-xs" htmlFor="lead-search">{t("leads.filter.search")}</Label>
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="lead-search"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder={t("leads.searchPlaceholder")}
                className="h-9 pl-9"
              />
            </div>
          </div>

          <div className="w-[170px]">
            <Label className="text-xs">{t("leads.filter.status")}</Label>
            <Select
              value={statusFilter || "__all"}
              onValueChange={(v) => {
                setPage(1);
                setStatusFilter(v === "__all" ? "" : (v as LeadStatus));
              }}
            >
              <SelectTrigger className="h-9">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">{t("leads.filter.all")}</SelectItem>
                {STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {t(`leads.status.${s}`)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="w-[170px]">
            <Label className="text-xs">{t("leads.filter.source")}</Label>
            <Select
              value={sourceFilter || "__all"}
              onValueChange={(v) => {
                setPage(1);
                setSourceFilter(v === "__all" ? "" : v);
              }}
            >
              <SelectTrigger className="h-9">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="__all">{t("leads.filter.all")}</SelectItem>
                {sources.map((s) => (
                  <SelectItem key={s.code} value={s.code}>
                    {s.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        </section>

        {loading ? (
          <PageLoading />
        ) : error ? (
          <PageError message={error} onRetry={() => void fetchList()} />
        ) : leads.length === 0 ? (
          <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
            <div className="rounded-full bg-muted p-3">
              <Search className="h-5 w-5" aria-hidden />
            </div>
            <p>{t("leads.empty")}</p>
            {canManage && (
              <Button size="sm" onClick={() => setCreating(true)}>
                <Plus className="mr-1.5 h-4 w-4" /> {t("leads.new")}
              </Button>
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
            <div className="overflow-x-auto rounded-lg border">
            <table className="min-w-[880px] w-full divide-y text-sm">
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
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("leads.field.name")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("leads.field.company")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("leads.field.phone")} / {t("leads.field.email")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("leads.field.source")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("leads.field.status")}</th>
                  {canSeeAll && (
                    <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("leads.field.owner")}</th>
                  )}
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("leads.field.createdAt")}</th>
                  <th className="px-3 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y">
                {leads.map((lead) => (
                  <tr
                    key={lead.id}
                    className="cursor-pointer hover:bg-muted/40"
                    onClick={() => void openDetail(lead.id)}
                  >
                    {canManage && (
                      <td
                        className="px-3 py-3"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <Checkbox
                          checked={selectedIds.has(lead.id)}
                          onCheckedChange={(v) => toggleOne(lead.id, v === true)}
                          aria-label={`${t("common.selectAll")} · ${lead.name}`}
                        />
                      </td>
                    )}
                    <td className="px-3 py-3 font-medium">{lead.name}</td>
                    <td className="px-3 py-3 text-muted-foreground">{lead.companyName || "—"}</td>
                    <td className="px-3 py-3 text-xs">
                      <div>{lead.phone || "—"}</div>
                      <div className="text-muted-foreground">{lead.email || ""}</div>
                    </td>
                    <td className="px-3 py-3 text-xs">
                      {sourceLabelByCode.get(lead.sourceCode) ?? lead.sourceCode}
                    </td>
                    <td className="px-3 py-3">
                      <Badge
                        variant="outline"
                        className={cn(
                          "gap-1.5 whitespace-nowrap font-medium",
                          LEAD_STATUS_STYLES[lead.status],
                        )}
                      >
                        <span
                          className={cn(
                            "h-1.5 w-1.5 rounded-full",
                            LEAD_STATUS_DOT[lead.status],
                          )}
                        />
                        {t(`leads.status.${lead.status}`)}
                      </Badge>
                    </td>
                    {canSeeAll && (
                      <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">{lead.ownerName || "—"}</td>
                    )}
                    <td className="whitespace-nowrap px-3 py-3 text-xs text-muted-foreground">
                      <div>{new Date(lead.createdAt).toLocaleDateString()}</div>
                      <div className="text-[11px] opacity-70">
                        {new Date(lead.createdAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
                      </div>
                    </td>
                    <td className="px-3 py-3 text-right">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={(e) => {
                          e.stopPropagation();
                          void handleDelete(lead.id);
                        }}
                        title={t("common.delete")}
                        aria-label={t("common.delete")}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            </div>
          </div>
        )}

        {total > pageSize && (
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>
              {(page - 1) * pageSize + 1}-{Math.min(page * pageSize, total)} / {total}
            </span>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                ‹
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => p + 1)}
                disabled={page * pageSize >= total}
              >
                ›
              </Button>
            </div>
          </div>
        )}
      </div>

      {/* Create dialog */}
      <Dialog open={creating} onOpenChange={setCreating}>
        <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{t("leads.new")}</DialogTitle>
            <DialogDescription>{t("leads.field.contactRequired")}</DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label>{t("leads.field.name")} *</Label>
              <Input
                value={createForm.name}
                onChange={(e) => setCreateForm({ ...createForm, name: e.target.value })}
                autoFocus
              />
            </div>
            <div>
              <Label>{t("leads.field.company")}</Label>
              <Input
                value={createForm.companyName ?? ""}
                onChange={(e) => setCreateForm({ ...createForm, companyName: e.target.value })}
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label>{t("leads.field.phone")}</Label>
                <Input
                  value={createForm.phone ?? ""}
                  onChange={(e) => setCreateForm({ ...createForm, phone: e.target.value })}
                />
              </div>
              <div>
                <Label>{t("leads.field.email")}</Label>
                <Input
                  type="email"
                  value={createForm.email ?? ""}
                  onChange={(e) => setCreateForm({ ...createForm, email: e.target.value })}
                />
              </div>
            </div>
            <div>
              <Label>{t("leads.field.source")} *</Label>
              <Select
                value={createForm.sourceCode || undefined}
                onValueChange={(v) => setCreateForm({ ...createForm, sourceCode: v })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {sources.map((s) => (
                    <SelectItem key={s.code} value={s.code}>
                      {s.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>{t("leads.field.note")}</Label>
              <Textarea
                value={createForm.note ?? ""}
                onChange={(e) => setCreateForm({ ...createForm, note: e.target.value })}
                rows={3}
              />
            </div>
            {createError && (
              <p className="rounded bg-destructive/10 px-3 py-2 text-xs text-destructive">{createError}</p>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCreating(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void handleCreate()} disabled={saving}>
              {saving ? "…" : t("leads.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Detail drawer (as wide dialog) */}
      <Dialog open={!!detail || detailLoading} onOpenChange={(open) => !open && closeDetail()}>
        <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto">
          {detailLoading ? (
            <>
              <DialogHeader>
                <DialogTitle className="sr-only">{t("leads.detail.title")}</DialogTitle>
                <DialogDescription className="sr-only">{t("common.loading")}</DialogDescription>
              </DialogHeader>
              <PageLoading />
            </>
          ) : detail ? (
            <>
              <DialogHeader>
                <DialogTitle className="flex flex-wrap items-center gap-2">
                  {detail.name}
                  <Badge
                    variant="outline"
                    className={cn(
                      "gap-1.5 whitespace-nowrap font-medium",
                      LEAD_STATUS_STYLES[detail.status],
                    )}
                  >
                    <span
                      className={cn("h-1.5 w-1.5 rounded-full", LEAD_STATUS_DOT[detail.status])}
                    />
                    {t(`leads.status.${detail.status}`)}
                  </Badge>
                </DialogTitle>
                <DialogDescription>
                  {detail.companyName ? `${detail.companyName} · ` : ""}
                  {sourceLabelByCode.get(detail.sourceCode) ?? detail.sourceCode}
                </DialogDescription>
              </DialogHeader>

              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <div className="text-xs text-muted-foreground">{t("leads.field.phone")}</div>
                  <div>{detail.phone || "—"}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">{t("leads.field.email")}</div>
                  <div>{detail.email || "—"}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">{t("leads.field.owner")}</div>
                  <div>{detail.ownerName || `#${detail.ownerUserId ?? "—"}`}</div>
                </div>
                <div>
                  <div className="text-xs text-muted-foreground">{t("leads.field.createdAt")}</div>
                  <div>{new Date(detail.createdAt).toLocaleString()}</div>
                </div>
                {detail.note && (
                  <div className="col-span-2">
                    <div className="text-xs text-muted-foreground">{t("leads.field.note")}</div>
                    <div className="whitespace-pre-wrap">{detail.note}</div>
                  </div>
                )}
                {detail.status === "Converted" && (
                  <div className="col-span-2 rounded bg-muted p-2 text-xs text-muted-foreground">
                    {t("leads.convert.locked")}
                    {detail.convertedCustomerId != null && ` · customerId=${detail.convertedCustomerId}`}
                    {detail.convertedOpportunityId != null && ` · opportunityId=${detail.convertedOpportunityId}`}
                  </div>
                )}
              </div>

              {/* Timeline */}
              <div className="mt-4">
                <h3 className="mb-2 text-sm font-medium">{t("leads.detail.timeline")}</h3>
                {detail.activities.length === 0 ? (
                  <p className="rounded border border-dashed p-4 text-center text-xs text-muted-foreground">
                    {t("leads.detail.noActivities")}
                  </p>
                ) : (
                  <ol className="space-y-2 border-l pl-4">
                    {detail.activities.map((a) => (
                      <li key={a.id} className="relative">
                        <span className="absolute -left-[19px] top-1 h-2.5 w-2.5 rounded-full bg-primary" />
                        <div className="text-xs text-muted-foreground">
                          {t(`leads.activity.${a.type}`)} · {new Date(a.createdAt).toLocaleString()}
                          {a.createdByName ? ` · ${a.createdByName}` : ""}
                        </div>
                        <div className="whitespace-pre-wrap text-sm">{a.content}</div>
                      </li>
                    ))}
                  </ol>
                )}

                {detail.status !== "Converted" && (
                  <div className="mt-3 flex flex-col gap-2 rounded border p-3">
                    <div className="flex flex-col gap-2 sm:flex-row">
                      <Select
                        value={activityType}
                        onValueChange={(v) => setActivityType(v as LeadActivityType)}
                      >
                        <SelectTrigger className="w-full sm:w-[140px]">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          {ACTIVITY_TYPES.map((tp) => (
                            <SelectItem key={tp} value={tp}>
                              {t(`leads.activity.${tp}`)}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                      <Textarea
                        value={activityContent}
                        onChange={(e) => setActivityContent(e.target.value)}
                        placeholder={t("leads.detail.activityContent")}
                        rows={2}
                        className="flex-1"
                      />
                    </div>
                    <Button
                      size="sm"
                      onClick={() => void handleAddActivity()}
                      disabled={!activityContent.trim() || addingActivity}
                    >
                      {addingActivity ? "…" : t("leads.detail.addActivity")}
                    </Button>
                  </div>
                )}
              </div>

              <DialogFooter>
                {canConvert && detail.status !== "Converted" && detail.status !== "Junk" && detail.status !== "NotInterested" && (
                  <Button onClick={() => setConvertOpen(true)}>
                    <ArrowRight className="mr-1.5 h-4 w-4" /> {t("leads.convert.button")}
                  </Button>
                )}
                <Button variant="outline" onClick={closeDetail}>
                  {t("common.close")}
                </Button>
              </DialogFooter>
            </>
          ) : null}
        </DialogContent>
      </Dialog>

      {/* Convert confirmation */}
      <Dialog open={convertOpen} onOpenChange={setConvertOpen}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>{t("leads.convert.confirmTitle")}</DialogTitle>
            <DialogDescription>{t("leads.convert.confirmBody")}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConvertOpen(false)} disabled={converting}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void handleConvert()} disabled={converting}>
              {converting ? "…" : t("leads.convert.button")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default AdminLeads;
