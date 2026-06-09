import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { Plus, Search as SearchIcon, Pencil, Trash2, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  adminApi,
  type ActivityCategoryResponse,
  type ProjectCategoryResponse,
  type UpsertActivityCategoryRequest,
  type UpsertProjectCategoryRequest,
} from "@/services/adminApi";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Switch } from "@/components/ui/switch";

type CategoryKind = "posts" | "projects";

type CategoryItem = ActivityCategoryResponse | ProjectCategoryResponse;

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
    error.response.data !== null &&
    "message" in error.response.data &&
    typeof error.response.data.message === "string"
  ) {
    return error.response.data.message;
  }

  return undefined;
};

const parseTab = (raw: string | null): CategoryKind =>
  raw === "projects" ? "projects" : "posts";

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
    if (next === "posts") {
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
      const payload: UpsertActivityCategoryRequest | UpsertProjectCategoryRequest = {
        name: form.name.trim(),
        isActive: form.isActive,
        sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
      };

      if (editingId == null) {
        if (kind === "projects") {
          await adminApi.createProjectCategory(payload);
        } else {
          await adminApi.createActivityCategory(payload);
        }
        toast({ title: t("form.created") });
      } else {
        if (kind === "projects") {
          await adminApi.updateProjectCategory(editingId, payload);
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

  const handleExport = () => {
    downloadCsv({
      filename: createCsvFilename(
        kind === "projects" ? "admin-project-categories" : "admin-activity-categories",
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
    { key: "posts", label: t("cat.tabPosts") },
    { key: "projects", label: t("cat.tabProjects") },
  ];

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("cat.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {loading ? "..." : `${filtered.length} / ${items.length}`}
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <AdminExportButton onClick={handleExport} disabled={loading || filtered.length === 0} />
          <button onClick={startCreate} className="admin-btn-primary inline-flex items-center gap-2" type="button">
            <Plus className="w-4 h-4" /> {t("cat.add")}
          </button>
        </div>
      </div>

      <div className="flex items-center gap-2 border-b mb-5" style={{ borderColor: "hsl(var(--admin-border))" }}>
        {tabs.map((tab) => {
          const active = tab.key === kind;
          return (
            <button
              key={tab.key}
              type="button"
              onClick={() => switchTab(tab.key)}
              className="px-4 py-2 text-sm font-semibold border-b-2 -mb-px transition-colors"
              style={{
                borderColor: active ? "hsl(var(--admin-primary))" : "transparent",
                color: active ? "hsl(var(--admin-primary))" : "hsl(var(--admin-muted))",
              }}
            >
              {tab.label}
            </button>
          );
        })}
      </div>

      <div className="admin-card p-5 mb-5">
        <div className="flex items-center gap-2 max-w-md">
          <SearchIcon className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t("cat.name")}
            className="admin-input flex-1"
          />
        </div>
      </div>

      <Dialog open={dialogOpen} onOpenChange={(open) => (open ? setDialogOpen(true) : closeDialog())}>
        <DialogContent className="admin-scope sm:max-w-md">
          <DialogHeader>
            <DialogTitle className="font-display text-xl font-extrabold">
              {editingId == null ? t("cat.add") : t("common.edit")}
            </DialogTitle>
          </DialogHeader>
          <form onSubmit={submitForm} className="space-y-4">
            <div>
              <label className="text-xs font-bold uppercase tracking-wider" htmlFor="cat-name">
                {t("cat.name")}
              </label>
              <input
                id="cat-name"
                value={form.name}
                onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))}
                placeholder={t("cat.name")}
                className="admin-input mt-1 w-full"
                autoFocus
                required
              />
            </div>
            <div>
              <label className="text-xs font-bold uppercase tracking-wider" htmlFor="cat-order">
                {t("cat.order")}
              </label>
              <input
                id="cat-order"
                type="number"
                value={form.sortOrder}
                onChange={(e) => setForm((prev) => ({ ...prev, sortOrder: Number(e.target.value) }))}
                className="admin-input mt-1 w-full"
              />
            </div>
            <div
              className="flex items-center justify-between gap-3 rounded-xl border px-4 py-3"
              style={{ borderColor: "hsl(var(--admin-border))" }}
            >
              <span className="text-sm font-semibold">{t("cat.published")}</span>
              <Switch
                checked={form.isActive}
                onCheckedChange={(checked) => setForm((prev) => ({ ...prev, isActive: checked }))}
                aria-label={t("cat.published")}
              />
            </div>
            <DialogFooter>
              <button
                type="button"
                className="admin-btn-primary opacity-70"
                onClick={closeDialog}
                disabled={submitting}
              >
                {t("common.cancel")}
              </button>
              <button type="submit" className="admin-btn-primary" disabled={submitting}>
                {submitting ? t("common.loading") : editingId == null ? t("form.create") : t("form.update")}
              </button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <div className="admin-card overflow-hidden">
        <table className="w-full text-sm">
          <thead style={{ background: "hsl(var(--admin-bg))" }}>
            <tr className="text-left">
              <th className="px-5 py-3 font-semibold">{t("cat.name")}</th>
              <th className="px-5 py-3 font-semibold">{t("cat.published")}</th>
              <th className="px-5 py-3 font-semibold">{t("cat.order")}</th>
              <th className="px-5 py-3 font-semibold text-right">{t("common.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={4} className="px-5 py-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                  Loading...
                </td>
              </tr>
            ) : filtered.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-5 py-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("cat.empty")}
                </td>
              </tr>
            ) : (
              filtered.map((item) => (
                <tr key={item.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-5 py-3 font-semibold">{item.name}</td>
                  <td className="px-5 py-3">
                    {item.isActive ? (
                      <Check className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />
                    ) : (
                      <X className="w-4 h-4" style={{ color: "hsl(var(--admin-danger))" }} />
                    )}
                  </td>
                  <td className="px-5 py-3">{item.sortOrder}</td>
                  <td className="px-5 py-3 text-right">
                    <button
                      onClick={() => startEdit(item)}
                      className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted mr-2"
                    >
                      <Pencil className="w-3.5 h-3.5" /> {t("common.edit")}
                    </button>
                    <button
                      onClick={() => remove(item)}
                      className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted"
                      style={{ color: "hsl(var(--admin-danger))" }}
                    >
                      <Trash2 className="w-3.5 h-3.5" /> {t("common.delete")}
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </AdminLayout>
  );
};

export default Categories;
