import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Search as SearchIcon, Pencil, Trash2, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, type ActivityCategoryResponse } from "@/services/adminApi";

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

const Categories = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [items, setItems] = useState<ActivityCategoryResponse[]>([]);
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<CategoryFormData>(emptyForm);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const result = await adminApi.getActivityCategories(true);
      setItems(result.data);
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const filtered = useMemo(
    () => items.filter((i) => i.name.toLowerCase().includes(q.trim().toLowerCase())),
    [items, q],
  );

  const startCreate = () => {
    setEditingId(null);
    setForm({ ...emptyForm, sortOrder: items.length + 1 });
  };

  const startEdit = (item: ActivityCategoryResponse) => {
    setEditingId(item.id);
    setForm({
      name: item.name,
      isActive: item.isActive,
      sortOrder: item.sortOrder,
    });
  };

  const submitForm = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!form.name.trim()) {
      toast({ title: t("form.required"), description: t("cat.name"), variant: "destructive" });
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        name: form.name.trim(),
        isActive: form.isActive,
        sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
      };

      if (editingId == null) {
        await adminApi.createActivityCategory(payload);
        toast({ title: t("form.created") });
      } else {
        await adminApi.updateActivityCategory(editingId, payload);
        toast({ title: t("form.updated") });
      }

      setEditingId(null);
      setForm(emptyForm);
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

  const remove = async (item: ActivityCategoryResponse) => {
    if (!window.confirm(t("form.confirmDelete"))) return;

    try {
      await adminApi.deleteActivityCategory(item.id);
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

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("cat.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {loading ? "..." : `${filtered.length} / ${items.length}`}
          </p>
        </div>
        <button onClick={startCreate} className="admin-btn-primary inline-flex items-center gap-2" type="button">
          <Plus className="w-4 h-4" /> {t("cat.add")}
        </button>
      </div>

      <div className="admin-card p-5 mb-5">
        <form onSubmit={submitForm} className="grid grid-cols-1 lg:grid-cols-4 gap-3 mb-4">
          <input
            value={form.name}
            onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))}
            placeholder={t("cat.name")}
            className="admin-input"
            required
          />
          <input
            type="number"
            value={form.sortOrder}
            onChange={(e) => setForm((prev) => ({ ...prev, sortOrder: Number(e.target.value) }))}
            placeholder={t("cat.order")}
            className="admin-input"
          />
          <label className="inline-flex items-center gap-2 px-3 rounded-xl border" style={{ borderColor: "hsl(var(--admin-border))" }}>
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(e) => setForm((prev) => ({ ...prev, isActive: e.target.checked }))}
            />
            <span className="text-sm font-semibold">{t("cat.published")}</span>
          </label>
          <div className="flex items-center gap-2">
            <button type="submit" className="admin-btn-primary" disabled={submitting}>
              {editingId == null ? t("form.create") : t("form.update")}
            </button>
            {editingId != null && (
              <button
                type="button"
                className="admin-btn-primary opacity-70"
                onClick={() => {
                  setEditingId(null);
                  setForm(emptyForm);
                }}
              >
                {t("common.cancel")}
              </button>
            )}
          </div>
        </form>

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
