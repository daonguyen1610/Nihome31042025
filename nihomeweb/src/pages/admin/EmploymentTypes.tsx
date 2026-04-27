import { useCallback, useEffect, useMemo, useState } from "react";
import { Plus, Search as SearchIcon, Pencil, Trash2, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { adminApi, type EmploymentTypeResponse } from "@/services/adminApi";

type EmploymentTypeFormData = {
  code: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
};

const emptyForm: EmploymentTypeFormData = {
  code: "",
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

const EmploymentTypes = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [items, setItems] = useState<EmploymentTypeResponse[]>([]);
  const [q, setQ] = useState("");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [editingId, setEditingId] = useState<number | null>(null);
  const [form, setForm] = useState<EmploymentTypeFormData>(emptyForm);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const result = await adminApi.getEmploymentTypes(true);
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

  const filtered = useMemo(() => {
    const normalizedQ = q.trim().toLowerCase();
    return items.filter((i) =>
      i.name.toLowerCase().includes(normalizedQ) || i.code.toLowerCase().includes(normalizedQ),
    );
  }, [items, q]);

  const startCreate = () => {
    setEditingId(null);
    setForm({ ...emptyForm, sortOrder: items.length + 1 });
  };

  const startEdit = (item: EmploymentTypeResponse) => {
    setEditingId(item.id);
    setForm({
      code: item.code,
      name: item.name,
      isActive: item.isActive,
      sortOrder: item.sortOrder,
    });
  };

  const submitForm = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!form.code.trim() || !form.name.trim()) {
      toast({ title: t("form.required"), description: "Mã và tên không được để trống", variant: "destructive" });
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        code: form.code.trim(),
        name: form.name.trim(),
        isActive: form.isActive,
        sortOrder: Number.isFinite(form.sortOrder) ? form.sortOrder : 0,
      };

      if (editingId == null) {
        await adminApi.createEmploymentType(payload);
        toast({ title: t("form.created") });
      } else {
        await adminApi.updateEmploymentType(editingId, payload);
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

  const remove = async (item: EmploymentTypeResponse) => {
    if (!window.confirm(t("form.confirmDelete"))) return;

    try {
      await adminApi.deleteEmploymentType(item.id);
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
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">Hình thức làm việc</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {loading ? "..." : `${filtered.length} / ${items.length}`}
          </p>
        </div>
        <button onClick={startCreate} className="admin-btn-primary inline-flex items-center gap-2" type="button">
          <Plus className="w-4 h-4" /> Thêm hình thức
        </button>
      </div>

      <div className="admin-card p-5 mb-5">
        <form onSubmit={submitForm} className="grid grid-cols-1 lg:grid-cols-5 gap-3 mb-4">
          <input
            value={form.code}
            onChange={(e) => setForm((prev) => ({ ...prev, code: e.target.value }))}
            placeholder="Mã (vd: full-time)"
            className="admin-input"
            required
          />
          <input
            value={form.name}
            onChange={(e) => setForm((prev) => ({ ...prev, name: e.target.value }))}
            placeholder="Tên hiển thị (vd: Toàn thời gian)"
            className="admin-input"
            required
          />
          <input
            type="number"
            value={form.sortOrder}
            onChange={(e) => setForm((prev) => ({ ...prev, sortOrder: Number(e.target.value) }))}
            placeholder="Thứ tự"
            className="admin-input"
          />
          <label className="inline-flex items-center gap-2 px-3 rounded-xl border" style={{ borderColor: "hsl(var(--admin-border))" }}>
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(e) => setForm((prev) => ({ ...prev, isActive: e.target.checked }))}
            />
            <span className="text-sm font-semibold">Kích hoạt</span>
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
            placeholder="Tìm theo mã hoặc tên"
            className="admin-input flex-1"
          />
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        <table className="w-full text-sm">
          <thead style={{ background: "hsl(var(--admin-bg))" }}>
            <tr className="text-left">
              <th className="px-5 py-3 font-semibold">Mã</th>
              <th className="px-5 py-3 font-semibold">Tên hiển thị</th>
              <th className="px-5 py-3 font-semibold">Kích hoạt</th>
              <th className="px-5 py-3 font-semibold">Thứ tự</th>
              <th className="px-5 py-3 font-semibold text-right">{t("common.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={5} className="px-5 py-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                  Loading...
                </td>
              </tr>
            ) : filtered.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-5 py-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                  Chưa có hình thức làm việc nào.
                </td>
              </tr>
            ) : (
              filtered.map((item) => (
                <tr key={item.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-5 py-3 font-mono text-xs">{item.code}</td>
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

export default EmploymentTypes;
