import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Plus, Search as SearchIcon, Pencil, Trash2, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import {
  adminApi,
  type ActivityCategoryResponse,
  type NewsCategoryResponse,
  type ProjectCategoryResponse,
  type UpsertActivityCategoryRequest,
  type UpsertNewsCategoryRequest,
  type UpsertProjectCategoryRequest,
} from "@/services/adminApi";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { BulkActionBar } from "@/components/admin/BulkActionBar";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useBulkSelection } from "@/hooks/useBulkSelection";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Switch } from "@/components/ui/switch";

type CategoryKind = "activities" | "projects" | "news";

type CategoryItem = ActivityCategoryResponse | ProjectCategoryResponse | NewsCategoryResponse;

type CategoryFormData = {
  name: string;
  isActive: boolean;
  sortOrder: number;
};

const emptyForm: CategoryFormData = {
  name: "",
  isActive: true,
  sortOrder: 0,
};

const getErrorMessage = (error: unknown) => {
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

const parseTab = (raw: string | null): CategoryKind => {
  if (raw === "projects" || raw === "news") return raw;
  return "activities";
};

const Categories = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const kind = parseTab(searchParams.get("tab"));

  const [items, setItems] = useState<CategoryItem[]>([]);
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<CategoryFormData>(emptyForm);
  const [dialogOpen, setDialogOpen] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const result =
        kind === "projects"
          ? await adminApi.getProjectCategories(true)
          : kind === "news"
            ? await adminApi.getNewsCategories(true)
          : await adminApi.getActivityCategories(true);
      setItems(result.data);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setLoading(false);
    }
  }, [kind, t, toast]);

  useEffect(() => {
    setEditingId(null);
    setForm(emptyForm);
    setDialogOpen(false);
    setQ("");
    loadData();
  }, [loadData]);

  const filtered = useMemo(
    () => items.filter((i) => i.name.toLowerCase().includes(q.trim().toLowerCase())),
    [items, q],
  );

  const switchTab = (next: CategoryKind) => {
    if (next === kind) return;
    const params = new URLSearchParams(searchParams);
    if (next === "activities") {
      params.delete("tab");
    } else {
      params.set("tab", next);
    }
    setSearchParams(params, { replace: true });
  };

  const startCreate = () => {
    setEditingId(null);
    setForm({ ...emptyForm, sortOrder: items.length + 1 });
    setDialogOpen(true);
  };

  const startEdit = (item: CategoryItem) => {
    setEditingId(item.id);
    setForm({
      name: item.name,
      isActive: item.isActive,
      sortOrder: item.sortOrder,
    });
    setDialogOpen(true);
  };

  const closeDialog = () => {
    setDialogOpen(false);
    setEditingId(null);
    setForm(emptyForm);
  };

  const submitForm = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!form.name.trim()) {
      toast({ title: t("form.required"), description: t("cat.name"), variant: "destructive" });
      return;
    }

    setSubmitting(true);
    try {
      const payload: UpsertActivityCategoryRequest | UpsertProjectCategoryRequest | UpsertNewsCategoryRequest = {
        name: form.name.trim(),
        isActive: form.isActive,
        sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
      };

      if (editingId == null) {
        if (kind === "projects") {
          await adminApi.createProjectCategory(payload);
        } else if (kind === "news") {
          await adminApi.createNewsCategory(payload);
        } else {
          await adminApi.createActivityCategory(payload);
        }
        toast({ title: t("form.created") });
      } else {
        if (kind === "projects") {
          await adminApi.updateProjectCategory(editingId, payload);
        } else if (kind === "news") {
          await adminApi.updateNewsCategory(editingId, payload);
        } else {
          await adminApi.updateActivityCategory(editingId, payload);
        }
        toast({ title: t("form.updated") });
      }

      setEditingId(null);
      setForm(emptyForm);
      setDialogOpen(false);
      await loadData();
    } catch (error: unknown) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(error),
        variant: "destructive",
      });
    } finally {
      setSubmitting(false);
    }
  };

  const remove = async (item: CategoryItem) => {
    if (!window.confirm(t("form.confirmDelete"))) return;

    try {
      if (kind === "projects") {
        await adminApi.deleteProjectCategory(item.id);
      } else if (kind === "news") {
        await adminApi.deleteNewsCategory(item.id);
      } else {
        await adminApi.deleteActivityCategory(item.id);
      }
      setItems((prev) => prev.filter((i) => i.id !== item.id));
      toast({ title: t("form.deleted") });
    } catch (error: unknown) {
      toast({
        title: t("common.error"),
        description: getErrorMessage(error),
        variant: "destructive",
      });
    }
  };

  // Bulk delete uses a ref so the deleter always dispatches on the active tab.
  const kindRef = useRef(kind);
  useEffect(() => {
    kindRef.current = kind;
  }, [kind]);
  const visibleIds = useMemo(() => filtered.map((i) => i.id), [filtered]);
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
    deleteOne: (id) => {
      const currentKind = kindRef.current;
      if (currentKind === "projects") return adminApi.deleteProjectCategory(id);
      if (currentKind === "news") return adminApi.deleteNewsCategory(id);
      return adminApi.deleteActivityCategory(id);
    },
    onAfter: async () => {
      await loadData();
    },
  });
  useEffect(() => {
    clearSelection();
  }, [kind, q, clearSelection]);

  const handleExport = () => {
    downloadCsv({
      filename: createCsvFilename(
        kind === "projects"
          ? "admin-project-categories"
          : kind === "news"
            ? "admin-news-categories"
            : "admin-activity-categories",
      ),
      columns: [
        { header: "ID", value: "id" },
        { header: t("cat.name"), value: "name" },
        { header: t("cat.published"), value: (row) => (row.isActive ? "Yes" : "No") },
        { header: t("cat.order"), value: "sortOrder" },
      ],
      rows: filtered,
    });
  };

  const tabs: { key: CategoryKind; label: string }[] = [
    { key: "activities", label: t("cat.tabActivities") },
    { key: "news", label: t("cat.tabNews") },
    { key: "projects", label: t("cat.tabProjects") },
  ];

  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-semibold">{t("cat.title")}</h1>
            <p className="text-xs italic text-muted-foreground">
              {loading ? "..." : `${filtered.length} / ${items.length}`}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <AdminExportButton onClick={handleExport} disabled={loading || filtered.length === 0} />
            <Button type="button" onClick={startCreate}>
              <Plus className="mr-1.5 h-4 w-4" /> {t("cat.add")}
            </Button>
          </div>
        </header>

        <div className="flex items-center gap-1 border-b">
          {tabs.map((tab) => {
            const active = tab.key === kind;
            return (
              <button
                key={tab.key}
                type="button"
                onClick={() => switchTab(tab.key)}
                className={cn(
                  "-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors",
                  active
                    ? "border-primary text-primary"
                    : "border-transparent text-muted-foreground hover:text-foreground",
                )}
              >
                {tab.label}
              </button>
            );
          })}
        </div>

        <section className="rounded-lg border bg-card p-3">
          <div className="w-full sm:max-w-sm">
            <Label className="text-xs" htmlFor="category-search">{t("common.search")}</Label>
            <div className="relative">
              <SearchIcon className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="category-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t("cat.name")}
                className="h-9 pl-9"
              />
            </div>
          </div>
        </section>

        <Dialog open={dialogOpen} onOpenChange={(open) => (open ? setDialogOpen(true) : closeDialog())}>
          <DialogContent className="sm:max-w-md">
            <DialogHeader>
              <DialogTitle>
                {editingId == null ? t("cat.add") : t("common.edit")}
              </DialogTitle>
            </DialogHeader>
            <form onSubmit={submitForm} className="space-y-4">
              <div className="space-y-1.5">
                <Label className="text-xs" htmlFor="cat-name">{t("cat.name")}</Label>
                <Input
                  id="cat-name"
                  value={form.name}
                  onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))}
                  placeholder={t("cat.name")}
                  autoFocus
                  required
                />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs" htmlFor="cat-order">{t("cat.order")}</Label>
                <Input
                  id="cat-order"
                  type="number"
                  value={form.sortOrder}
                  onChange={(e) => setForm((prev) => ({ ...prev, sortOrder: Number(e.target.value) }))}
                />
              </div>
              <div className="flex items-center justify-between gap-3 rounded-md border px-4 py-3">
                <span className="text-sm font-medium">{t("cat.published")}</span>
                <Switch
                  checked={form.isActive}
                  onCheckedChange={(checked) => setForm((prev) => ({ ...prev, isActive: checked }))}
                  aria-label={t("cat.published")}
                />
              </div>
              <DialogFooter>
                <Button
                  type="button"
                  variant="outline"
                  onClick={closeDialog}
                  disabled={submitting}
                >
                  {t("common.cancel")}
                </Button>
                <Button type="submit" disabled={submitting}>
                  {submitting ? t("common.loading") : editingId == null ? t("form.create") : t("form.update")}
                </Button>
              </DialogFooter>
            </form>
          </DialogContent>
        </Dialog>

        <div className="space-y-2">
          <BulkActionBar
            selectedCount={selectedIds.size}
            bulkDeleting={bulkDeleting}
            onClear={clearSelection}
            onBulkDelete={() => void handleBulkDelete()}
          />
          <div className="overflow-x-auto rounded-lg border">
            <table className="min-w-[700px] w-full divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
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
                  <th className="px-3 py-3 text-left font-medium">{t("cat.name")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("cat.published")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-left font-medium">{t("cat.order")}</th>
                  <th className="whitespace-nowrap px-3 py-3 text-right font-medium">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {loading ? (
                  <tr>
                    <td colSpan={5} className="px-5 py-10 text-center text-muted-foreground">
                      Loading...
                    </td>
                  </tr>
                ) : filtered.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="px-5 py-10 text-center text-muted-foreground">
                      {t("cat.empty")}
                    </td>
                  </tr>
                ) : (
                  filtered.map((item) => (
                    <tr key={item.id} className="hover:bg-muted/40 transition">
                      <td className="px-3 py-3" onClick={(e) => e.stopPropagation()}>
                        <Checkbox
                          checked={selectedIds.has(item.id)}
                          onCheckedChange={(v) => toggleOne(item.id, v === true)}
                          aria-label={`${t("common.selectAll")} · ${item.name}`}
                        />
                      </td>
                      <td className="px-3 py-3 font-medium">{item.name}</td>
                      <td className="px-3 py-3">
                        {item.isActive ? (
                          <Check className="h-4 w-4 text-emerald-600" />
                        ) : (
                          <X className="h-4 w-4 text-muted-foreground" />
                        )}
                      </td>
                      <td className="whitespace-nowrap px-3 py-3">{item.sortOrder}</td>
                      <td className="whitespace-nowrap px-3 py-3 text-right">
                        <div className="inline-flex items-center gap-1">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => startEdit(item)}
                          >
                            <Pencil className="mr-1 h-3.5 w-3.5" /> {t("common.edit")}
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => remove(item)}
                            className="text-destructive hover:text-destructive"
                          >
                            <Trash2 className="mr-1 h-3.5 w-3.5" /> {t("common.delete")}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default Categories;
