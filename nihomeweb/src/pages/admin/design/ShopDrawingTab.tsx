import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, ChevronDown, ChevronUp, Loader2, Pencil, Plus, Send, ShieldCheck, Trash2, Undo2, XCircle } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { SearchableSelect } from "@/components/ui/searchable-select";
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
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { usePermissions } from "@/hooks/usePermissions";
import { ADMIN_PERMS } from "@/lib/adminPermissions";
import { extractApiError } from "@/lib/apiError";
import {
  SHOP_DRAWING_STATUSES,
  adminApi,
  type CreateShopDrawingRequest,
  type DesignProjectResponse,
  type MasterDataOption,
  type ShopDrawingResponse,
  type ShopDrawingStatus,
} from "@/services/adminApi";

const STATUS_BADGE: Record<ShopDrawingStatus, string> = {
  Drafting: "border-sky-200 bg-sky-50 text-sky-700",
  InReview: "border-amber-200 bg-amber-50 text-amber-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  PendingIfc: "border-indigo-200 bg-indigo-50 text-indigo-700",
  Released: "border-emerald-300 bg-emerald-100 text-emerald-800",
  Rejected: "border-rose-200 bg-rose-50 text-rose-700",
};

const STATUS_STAT_KEY: Record<ShopDrawingStatus, string> = {
  Drafting: "shopDrawing.stats.drafting",
  InReview: "shopDrawing.stats.inReview",
  Approved: "shopDrawing.stats.approved",
  PendingIfc: "shopDrawing.stats.pendingIfc",
  Released: "shopDrawing.stats.released",
  Rejected: "shopDrawing.status.Rejected",
};

const formatDate = (iso?: string | null, lang: string = "vi"): string => {
  if (!iso) return "—";
  try {
    return new Date(iso).toLocaleDateString(lang);
  } catch {
    return iso;
  }
};

interface UserOption {
  id: number;
  fullName: string;
}

interface FormState {
  disciplineCode: string;
  constructionItem: string;
  title: string;
  description: string;
  ownerUserId: number | null;
  note: string;
}

const emptyForm = (): FormState => ({
  disciplineCode: "",
  constructionItem: "",
  title: "",
  description: "",
  ownerUserId: null,
  note: "",
});

interface Props {
  project: DesignProjectResponse;
  onProjectMayHaveChanged?: () => void | Promise<void>;
}

/**
 * NIH-116 Shop Drawing tab. Renders drawings grouped by discipline then
 * construction item, exposes the state-machine actions, and provides
 * bulk-delete for drafts. Locked when the parent project is not at the
 * ShopDrawing stage — the Basic Design tab handles unlocking.
 */
export const ShopDrawingTab = ({ project }: Props) => {
  const { t, lang } = useI18n();
  const { toast } = useToast();
  const { has } = usePermissions();
  const canManage = has(ADMIN_PERMS.designShopManage);
  const canApprove = has(ADMIN_PERMS.designShopApprove);
  const canPickOwner = has(ADMIN_PERMS.users);

  const isShopStage = project.currentStage === "ShopDrawing";
  const isBeforeShop = project.currentStage === "Concept" || project.currentStage === "BasicDesign";

  const [rows, setRows] = useState<ShopDrawingResponse[]>([]);
  const [statusCounts, setStatusCounts] = useState<Partial<Record<ShopDrawingStatus, number>>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [disciplines, setDisciplines] = useState<MasterDataOption[]>([]);
  const [users, setUsers] = useState<UserOption[]>([]);

  // filter state
  const [filterDiscipline, setFilterDiscipline] = useState<string>("");
  const [filterStatus, setFilterStatus] = useState<string>("");
  const [search, setSearch] = useState<string>("");

  const fetchRows = useCallback(async () => {
    setError(null);
    try {
      const { data } = await adminApi.listShopDrawings({
        designProjectId: project.id,
        disciplineCode: filterDiscipline || undefined,
        status: filterStatus || undefined,
        search: search.trim() || undefined,
        pageSize: 200,
      });
      setRows(data.items ?? []);
      setStatusCounts(data.statusCounts ?? {});
    } catch (err) {
      setError(extractApiError(err));
    } finally {
      setLoading(false);
    }
  }, [project.id, filterDiscipline, filterStatus, search]);

  useEffect(() => {
    void fetchRows();
  }, [fetchRows]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const { data } = await adminApi.getMasterDataOptions("design_discipline");
        if (!cancelled) setDisciplines((data ?? []).filter((o) => o.isActive));
      } catch {
        // non-fatal
      }
      if (canPickOwner) {
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
          // no permission → owner picker stays hidden
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [canPickOwner]);

  const disciplineOptions = useMemo(
    () => disciplines.map((d) => ({ value: d.code, label: d.name })),
    [disciplines],
  );
  const userOptions = useMemo(
    () => users.map((u) => ({ value: String(u.id), label: u.fullName })),
    [users],
  );

  const disciplineLabelByCode = useMemo(() => {
    const m = new Map<string, string>();
    for (const d of disciplines) m.set(d.code, d.name);
    return m;
  }, [disciplines]);

  // Group by discipline → construction item so PMs see the site-team hierarchy.
  const grouped = useMemo(() => {
    const byDiscipline = new Map<string, Map<string, ShopDrawingResponse[]>>();
    for (const r of rows) {
      let byItem = byDiscipline.get(r.disciplineCode);
      if (!byItem) {
        byItem = new Map();
        byDiscipline.set(r.disciplineCode, byItem);
      }
      const bucket = byItem.get(r.constructionItem) ?? [];
      bucket.push(r);
      byItem.set(r.constructionItem, bucket);
    }
    return byDiscipline;
  }, [rows]);

  // -------- selection (per-row bulk delete) --------
  const draftableIds = useMemo(
    () => new Set(rows.filter((r) => r.status === "Drafting").map((r) => r.id)),
    [rows],
  );
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const toggleOne = (id: number, checked: boolean) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (checked) next.add(id);
      else next.delete(id);
      return next;
    });
  };
  const clearSelection = () => setSelectedIds(new Set());
  // when rows change, drop any selected ids that no longer exist
  useEffect(() => {
    setSelectedIds((prev) => {
      let mutated = false;
      const next = new Set<number>();
      for (const id of prev) {
        if (draftableIds.has(id)) next.add(id);
        else mutated = true;
      }
      return mutated ? next : prev;
    });
  }, [draftableIds]);

  // -------- create / edit dialog --------
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<ShopDrawingResponse | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm());
  const [saving, setSaving] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const isEdit = !!editing;

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm());
    setFormError(null);
    setDialogOpen(true);
  };
  const openEdit = (row: ShopDrawingResponse) => {
    setEditing(row);
    setForm({
      disciplineCode: row.disciplineCode,
      constructionItem: row.constructionItem,
      title: row.title,
      description: row.description ?? "",
      ownerUserId: row.ownerUserId ?? null,
      note: row.note ?? "",
    });
    setFormError(null);
    setDialogOpen(true);
  };

  const submitForm = async () => {
    setFormError(null);
    if (!form.title.trim()) {
      setFormError(t("shopDrawing.form.titleRequired"));
      return;
    }
    if (!form.disciplineCode) {
      setFormError(t("shopDrawing.form.disciplineRequired"));
      return;
    }
    if (!form.constructionItem.trim()) {
      setFormError(t("shopDrawing.form.constructionItemRequired"));
      return;
    }
    setSaving(true);
    try {
      if (isEdit && editing) {
        await adminApi.updateShopDrawing(editing.id, {
          disciplineCode: form.disciplineCode,
          constructionItem: form.constructionItem.trim(),
          title: form.title.trim(),
          description: form.description.trim() || null,
          ownerUserId: form.ownerUserId ?? null,
          note: form.note.trim() || null,
        });
        toast({ title: t("shopDrawing.updated") });
      } else {
        const payload: CreateShopDrawingRequest = {
          designProjectId: project.id,
          disciplineCode: form.disciplineCode,
          constructionItem: form.constructionItem.trim(),
          title: form.title.trim(),
          description: form.description.trim() || null,
          ownerUserId: form.ownerUserId ?? null,
          note: form.note.trim() || null,
        };
        await adminApi.createShopDrawing(payload);
        toast({ title: t("shopDrawing.created") });
      }
      setDialogOpen(false);
      setEditing(null);
      await fetchRows();
    } catch (err) {
      setFormError(extractApiError(err));
    } finally {
      setSaving(false);
    }
  };

  // -------- delete (single) confirmation --------
  const [deleting, setDeleting] = useState<ShopDrawingResponse | null>(null);
  const [busyDelete, setBusyDelete] = useState(false);
  const confirmDelete = async () => {
    if (!deleting) return;
    setBusyDelete(true);
    try {
      await adminApi.deleteShopDrawing(deleting.id);
      toast({ title: t("shopDrawing.deleted") });
      setDeleting(null);
      await fetchRows();
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

  // -------- bulk delete --------
  const [bulkConfirmOpen, setBulkConfirmOpen] = useState(false);
  const [busyBulkDelete, setBusyBulkDelete] = useState(false);
  const doBulkDelete = async () => {
    const ids = Array.from(selectedIds);
    if (ids.length === 0) return;
    setBusyBulkDelete(true);
    try {
      const { data } = await adminApi.bulkDeleteShopDrawings({ ids });
      const failed = (data.failures ?? []).length;
      toast({
        title: t("shopDrawing.bulk.result")
          .replace("{success}", String(data.deleted))
          .replace("{failed}", String(failed)),
        variant: failed > 0 && data.deleted === 0 ? "destructive" : undefined,
      });
      clearSelection();
      setBulkConfirmOpen(false);
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setBusyBulkDelete(false);
    }
  };

  // -------- transitions --------
  const [transitioning, setTransitioning] = useState<number | null>(null);
  const transition = async (row: ShopDrawingResponse, next: ShopDrawingStatus) => {
    setTransitioning(row.id);
    try {
      await adminApi.transitionShopDrawing(row.id, { status: next });
      toast({ title: t("shopDrawing.transitioned") });
      await fetchRows();
    } catch (err) {
      toast({
        title: t("common.error"),
        description: extractApiError(err),
        variant: "destructive",
      });
    } finally {
      setTransitioning(null);
    }
  };

  if (loading) {
    return (
      <div className="rounded-lg border border-slate-200 bg-white p-8 text-center text-sm text-slate-500">
        <Loader2 className="mx-auto mb-2 h-5 w-5 animate-spin" />
      </div>
    );
  }
  if (error) {
    return (
      <div className="rounded-lg border border-rose-200 bg-rose-50/60 p-4 text-sm text-rose-700">
        {error}
      </div>
    );
  }

  return (
    <div className="space-y-4" data-testid="shop-drawing-tab">
      {isBeforeShop ? (
        <p
          className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800"
          data-testid="shop-drawing-locked-before"
        >
          {t("shopDrawing.lockedBefore")}
        </p>
      ) : !isShopStage ? (
        <p className="rounded-md border border-slate-200 bg-slate-50 px-3 py-2 text-xs text-slate-700">
          {t("shopDrawing.lockedAfter")}
        </p>
      ) : null}

      {/* Status stat pills */}
      <section className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
        <StatPill label={t("shopDrawing.stats.total")} value={rows.length} icon={<ShieldCheck className="h-3.5 w-3.5" />} tone="slate" />
        {SHOP_DRAWING_STATUSES.filter((s) => s !== "Rejected").map((s) => (
          <StatPill
            key={s}
            label={t(STATUS_STAT_KEY[s])}
            value={statusCounts[s] ?? 0}
            tone={
              s === "Drafting" ? "sky"
                : s === "InReview" ? "amber"
                : s === "Approved" ? "emerald"
                : s === "PendingIfc" ? "indigo"
                : "emerald"
            }
          />
        ))}
      </section>

      {/* Toolbar: filters + create + bulk actions */}
      <section className="flex flex-wrap items-center gap-2 rounded-lg border border-slate-200 bg-white p-2 shadow-sm">
        <div className="flex-1 min-w-[180px]">
          <Input
            placeholder={t("shopDrawing.filter.search")}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="h-9"
            data-testid="shop-drawing-search"
          />
        </div>
        <Select value={filterDiscipline || "__all__"} onValueChange={(v) => setFilterDiscipline(v === "__all__" ? "" : v)}>
          <SelectTrigger className="h-9 w-[170px]" data-testid="shop-drawing-filter-discipline">
            <SelectValue placeholder={t("shopDrawing.filter.allDisciplines")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t("shopDrawing.filter.allDisciplines")}</SelectItem>
            {disciplineOptions.map((o) => (
              <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select value={filterStatus || "__all__"} onValueChange={(v) => setFilterStatus(v === "__all__" ? "" : v)}>
          <SelectTrigger className="h-9 w-[170px]" data-testid="shop-drawing-filter-status">
            <SelectValue placeholder={t("shopDrawing.filter.allStatuses")} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="__all__">{t("shopDrawing.filter.allStatuses")}</SelectItem>
            {SHOP_DRAWING_STATUSES.map((s) => (
              <SelectItem key={s} value={s}>{t(`shopDrawing.status.${s}`)}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="ml-auto flex flex-wrap items-center gap-2">
          {canManage && selectedIds.size > 0 ? (
            <>
              <span className="text-xs text-slate-500">{selectedIds.size} selected</span>
              <Button
                size="sm"
                variant="destructive"
                data-testid="shop-drawing-bulk-delete"
                onClick={() => setBulkConfirmOpen(true)}
              >
                <Trash2 className="mr-1 h-4 w-4" />
                {t("shopDrawing.bulk.delete")}
              </Button>
            </>
          ) : null}
          {canManage && isShopStage ? (
            <Button size="sm" onClick={openCreate} data-testid="shop-drawing-new">
              <Plus className="mr-1 h-4 w-4" />
              {t("shopDrawing.new")}
            </Button>
          ) : null}
        </div>
      </section>

      {rows.length === 0 ? (
        <div className="rounded-md border border-dashed p-10 text-center text-sm text-muted-foreground" data-testid="shop-drawing-empty">
          {t("shopDrawing.empty")}
        </div>
      ) : (
        <div className="space-y-4">
          {Array.from(grouped.entries()).map(([disciplineCode, byItem]) => (
            <DisciplineSection
              key={disciplineCode}
              disciplineCode={disciplineCode}
              disciplineLabel={disciplineLabelByCode.get(disciplineCode) ?? disciplineCode}
              itemMap={byItem}
              t={t}
              lang={lang}
              canManage={canManage}
              canApprove={canApprove}
              isShopStage={isShopStage}
              selectedIds={selectedIds}
              draftableIds={draftableIds}
              transitioningId={transitioning}
              onToggle={toggleOne}
              onEdit={openEdit}
              onDelete={setDeleting}
              onTransition={(row, next) => void transition(row, next)}
            />
          ))}
        </div>
      )}

      {/* create / edit dialog */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{isEdit ? t("shopDrawing.edit") : t("shopDrawing.new")}</DialogTitle>
            <DialogDescription>{t("shopDrawing.form.hint")}</DialogDescription>
          </DialogHeader>
          <div className="grid gap-3">
            <div>
              <Label>{t("shopDrawing.field.discipline")}</Label>
              <Select
                value={form.disciplineCode}
                onValueChange={(v) => setForm((f) => ({ ...f, disciplineCode: v }))}
                disabled={isEdit && editing?.status !== "Drafting"}
              >
                <SelectTrigger className="mt-1 h-9" data-testid="shop-drawing-form-discipline">
                  <SelectValue placeholder={t("shopDrawing.form.disciplinePlaceholder")} />
                </SelectTrigger>
                <SelectContent>
                  {disciplineOptions.map((o) => (
                    <SelectItem key={o.value} value={o.value}>
                      {o.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>{t("shopDrawing.field.constructionItem")}</Label>
              <Input
                value={form.constructionItem}
                onChange={(e) => setForm((f) => ({ ...f, constructionItem: e.target.value }))}
                placeholder={t("shopDrawing.form.constructionItemPlaceholder")}
                disabled={isEdit && editing?.status !== "Drafting"}
                data-testid="shop-drawing-form-item"
              />
            </div>
            <div>
              <Label>{t("shopDrawing.field.title")}</Label>
              <Input
                autoFocus
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                data-testid="shop-drawing-form-title"
              />
            </div>
            <div>
              <Label>{t("shopDrawing.field.description")}</Label>
              <Textarea
                rows={3}
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
              />
            </div>
            {canPickOwner ? (
              <div>
                <Label>{t("shopDrawing.field.owner")}</Label>
                <SearchableSelect
                  className="mt-1"
                  value={form.ownerUserId != null ? String(form.ownerUserId) : ""}
                  onChange={(v) =>
                    setForm((f) => ({ ...f, ownerUserId: v ? Number(v) : null }))
                  }
                  options={[{ value: "", label: t("shopDrawing.form.ownerNone") }, ...userOptions]}
                  placeholder={t("shopDrawing.form.ownerNone")}
                />
              </div>
            ) : null}
            <div>
              <Label>{t("shopDrawing.field.note")}</Label>
              <Textarea
                rows={2}
                value={form.note}
                onChange={(e) => setForm((f) => ({ ...f, note: e.target.value }))}
              />
            </div>
          </div>
          {formError ? <p className="text-sm text-rose-600" data-testid="shop-drawing-form-error">{formError}</p> : null}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submitForm()} disabled={saving} data-testid="shop-drawing-form-save">
              {t("common.save")}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* single-row delete confirmation */}
      <AlertDialog open={!!deleting} onOpenChange={(o) => (!o ? setDeleting(null) : undefined)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{t("shopDrawing.delete.confirmTitle")}</AlertDialogTitle>
            <AlertDialogDescription>
              {t("shopDrawing.delete.confirmBody").replace("{code}", deleting?.drawingCode ?? "")}
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
              {t("shopDrawing.delete.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* bulk delete confirmation */}
      <AlertDialog open={bulkConfirmOpen} onOpenChange={setBulkConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {t("shopDrawing.bulk.confirmTitle").replace("{count}", String(selectedIds.size))}
            </AlertDialogTitle>
            <AlertDialogDescription>{t("shopDrawing.bulk.deleteHint")}</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={busyBulkDelete}>{t("common.cancel")}</AlertDialogCancel>
            <AlertDialogAction
              className="bg-rose-600 hover:bg-rose-700"
              onClick={(e) => {
                e.preventDefault();
                void doBulkDelete();
              }}
              disabled={busyBulkDelete}
              data-testid="shop-drawing-bulk-delete-confirm"
            >
              {t("shopDrawing.delete.confirm")}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
};

// ------------------------------ stat pill ------------------------------

const TONE_BG: Record<string, string> = {
  slate: "border-slate-200 bg-slate-50 text-slate-700",
  sky: "border-sky-200 bg-sky-50 text-sky-700",
  amber: "border-amber-200 bg-amber-50 text-amber-700",
  emerald: "border-emerald-200 bg-emerald-50 text-emerald-700",
  indigo: "border-indigo-200 bg-indigo-50 text-indigo-700",
};

const StatPill = ({
  label,
  value,
  tone,
  icon,
}: {
  label: string;
  value: number;
  tone: keyof typeof TONE_BG | string;
  icon?: React.ReactNode;
}) => (
  <div className={cn("rounded-md border px-2.5 py-1.5", TONE_BG[tone] ?? TONE_BG.slate)}>
    <p className="flex items-center gap-1 text-[11px] font-medium uppercase tracking-wide opacity-80">
      {icon}
      {label}
    </p>
    <p className="mt-0.5 text-lg font-semibold leading-none">{value}</p>
  </div>
);

// ------------------------------ discipline section ------------------------------

const DisciplineSection = ({
  disciplineCode,
  disciplineLabel,
  itemMap,
  t,
  lang,
  canManage,
  canApprove,
  isShopStage,
  selectedIds,
  draftableIds,
  transitioningId,
  onToggle,
  onEdit,
  onDelete,
  onTransition,
}: {
  disciplineCode: string;
  disciplineLabel: string;
  itemMap: Map<string, ShopDrawingResponse[]>;
  t: (key: string) => string;
  lang: string;
  canManage: boolean;
  canApprove: boolean;
  isShopStage: boolean;
  selectedIds: Set<number>;
  draftableIds: Set<number>;
  transitioningId: number | null;
  onToggle: (id: number, checked: boolean) => void;
  onEdit: (row: ShopDrawingResponse) => void;
  onDelete: (row: ShopDrawingResponse) => void;
  onTransition: (row: ShopDrawingResponse, next: ShopDrawingStatus) => void;
}) => {
  const [collapsed, setCollapsed] = useState(false);
  const total = Array.from(itemMap.values()).reduce((n, arr) => n + arr.length, 0);
  return (
    <section className="rounded-lg border border-slate-200 bg-white shadow-sm" data-testid={`shop-drawing-discipline-${disciplineCode}`}>
      <header className="flex items-center justify-between border-b border-slate-100 px-3 py-2">
        <button
          type="button"
          className="flex items-center gap-1.5 text-left"
          onClick={() => setCollapsed((v) => !v)}
        >
          {collapsed ? <ChevronDown className="h-4 w-4 text-slate-400" /> : <ChevronUp className="h-4 w-4 text-slate-400" />}
          <p className="text-sm font-semibold text-slate-800">{disciplineLabel}</p>
        </button>
        <span className="text-xs text-slate-500">{total}</span>
      </header>
      {!collapsed ? (
        <div className="divide-y divide-slate-100">
          {Array.from(itemMap.entries()).map(([itemName, docs]) => (
            <div key={itemName} className="p-3">
              <p className="mb-2 text-xs font-medium uppercase tracking-wide text-slate-500">
                {itemName}
                <span className="ml-1 text-[10px] font-normal text-slate-400">· {docs.length}</span>
              </p>
              <ul className="space-y-2">
                {docs.map((row) => (
                  <li key={row.id} className="rounded-md border border-slate-100 bg-slate-50/40 p-2">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div className="flex min-w-0 items-start gap-2">
                        {canManage && draftableIds.has(row.id) ? (
                          <Checkbox
                            checked={selectedIds.has(row.id)}
                            onCheckedChange={(v) => onToggle(row.id, v === true)}
                            aria-label={row.drawingCode}
                            data-testid={`shop-drawing-check-${row.id}`}
                          />
                        ) : (
                          <span className="inline-block w-4" />
                        )}
                        <div className="min-w-0">
                          <p className="font-mono text-xs text-slate-500">{row.drawingCode}</p>
                          <p className="mt-0.5 break-words text-sm font-medium text-slate-900">{row.title}</p>
                          {row.ownerName ? (
                            <p className="mt-0.5 text-xs text-muted-foreground">{row.ownerName}</p>
                          ) : null}
                        </div>
                      </div>
                      <div className="flex flex-col items-end gap-1">
                        <Badge
                          variant="outline"
                          className={cn("whitespace-nowrap", STATUS_BADGE[row.status])}
                          data-testid={`shop-drawing-status-${row.id}`}
                        >
                          {t(`shopDrawing.status.${row.status}`)}
                        </Badge>
                        <span className="text-[10px] text-muted-foreground">{formatDate(row.updatedAt, lang)}</span>
                      </div>
                    </div>
                    {canManage && isShopStage ? (
                      <div className="mt-2 flex flex-wrap gap-1.5">
                        <RowActions
                          row={row}
                          t={t}
                          canApprove={canApprove}
                          busy={transitioningId === row.id}
                          onEdit={() => onEdit(row)}
                          onDelete={() => onDelete(row)}
                          onTransition={(next) => onTransition(row, next)}
                        />
                      </div>
                    ) : null}
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      ) : null}
    </section>
  );
};

// ------------------------------ row actions ------------------------------

const RowActions = ({
  row,
  t,
  canApprove,
  busy,
  onEdit,
  onDelete,
  onTransition,
}: {
  row: ShopDrawingResponse;
  t: (key: string) => string;
  canApprove: boolean;
  busy: boolean;
  onEdit: () => void;
  onDelete: () => void;
  onTransition: (next: ShopDrawingStatus) => void;
}) => {
  const s = row.status;
  const btn = "h-8 text-xs";
  return (
    <>
      {s === "Drafting" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit} data-testid={`shop-drawing-edit-${row.id}`}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          <Button size="sm" variant="secondary" className={btn} disabled={busy}
            onClick={() => onTransition("InReview")} data-testid={`shop-drawing-send-${row.id}`}>
            <Send className="mr-1 h-3.5 w-3.5" />
            {t("shopDrawing.action.sendReview")}
          </Button>
          <Button size="sm" variant="ghost" className={cn(btn, "text-rose-700 hover:bg-rose-50 hover:text-rose-800")}
            onClick={onDelete}>
            <Trash2 className="mr-1 h-3.5 w-3.5" />
            {t("shopDrawing.action.delete")}
          </Button>
        </>
      )}
      {s === "InReview" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          {canApprove ? (
            <Button size="sm" className={cn(btn, "bg-emerald-600 hover:bg-emerald-700")} disabled={busy}
              onClick={() => onTransition("Approved")} data-testid={`shop-drawing-approve-${row.id}`}>
              <CheckCircle2 className="mr-1 h-3.5 w-3.5" />
              {t("shopDrawing.action.approve")}
            </Button>
          ) : null}
          <Button size="sm" variant="ghost" className={btn} disabled={busy}
            onClick={() => onTransition("Drafting")}>
            <Undo2 className="mr-1 h-3.5 w-3.5" />
            {t("shopDrawing.action.backToDraft")}
          </Button>
          <Button size="sm" variant="ghost" className={cn(btn, "text-rose-700 hover:bg-rose-50")} disabled={busy}
            onClick={() => onTransition("Rejected")}>
            <XCircle className="mr-1 h-3.5 w-3.5" />
            {t("shopDrawing.action.reject")}
          </Button>
        </>
      )}
      {s === "Approved" && (
        <>
          <Button size="sm" variant="outline" className={btn} onClick={onEdit}>
            <Pencil className="mr-1 h-3.5 w-3.5" />
            {t("common.edit")}
          </Button>
          <Button size="sm" variant="secondary" className={btn} disabled={busy}
            onClick={() => onTransition("PendingIfc")} data-testid={`shop-drawing-queue-${row.id}`}>
            {t("shopDrawing.action.queueIfc")}
          </Button>
        </>
      )}
      {s === "PendingIfc" && (
        <>
          <Button size="sm" variant="ghost" className={btn} disabled={busy}
            onClick={() => onTransition("Approved")}>
            <Undo2 className="mr-1 h-3.5 w-3.5" />
            {t("shopDrawing.action.unqueueIfc")}
          </Button>
          <Button size="sm" variant="ghost" className={cn(btn, "text-rose-700 hover:bg-rose-50")} disabled={busy}
            onClick={() => onTransition("Rejected")}>
            <XCircle className="mr-1 h-3.5 w-3.5" />
            {t("shopDrawing.action.reject")}
          </Button>
        </>
      )}
      {s === "Released" && (
        <Badge variant="outline" className="border-emerald-300 bg-emerald-100 text-emerald-800">
          <CheckCircle2 className="mr-1 h-3.5 w-3.5" />
          {t("shopDrawing.status.Released")}
        </Badge>
      )}
      {s === "Rejected" && (
        <Badge variant="outline" className="border-rose-200 bg-rose-50 text-rose-700">
          <XCircle className="mr-1 h-3.5 w-3.5" />
          {t("shopDrawing.status.Rejected")}
        </Badge>
      )}
    </>
  );
};
