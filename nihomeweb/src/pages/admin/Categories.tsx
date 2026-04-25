import { useMemo, useState } from "react";
import { Plus, Search as SearchIcon, Pencil, Trash2, Check, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";

type Category = { id: string; name: string; published: boolean; order: number };

const seed: Category[] = [
  { id: "c1", name: "Tin tức", published: true, order: 1 },
  { id: "c2", name: "Dự án", published: true, order: 2 },
  { id: "c3", name: "Hoạt động", published: true, order: 3 },
  { id: "c4", name: "Tuyển dụng", published: true, order: 4 },
  { id: "c5", name: "Chứng nhận", published: false, order: 5 },
];

const KEY = "nicon_admin_categories_v1";
const load = (): Category[] => {
  try {
    const raw = localStorage.getItem(KEY);
    return raw ? (JSON.parse(raw) as Category[]) : seed;
  } catch {
    return seed;
  }
};
const save = (items: Category[]) => localStorage.setItem(KEY, JSON.stringify(items));

const Categories = () => {
  const { t } = useI18n();
  const [items, setItems] = useState<Category[]>(load);
  const [q, setQ] = useState("");

  const filtered = useMemo(
    () => items.filter((i) => i.name.toLowerCase().includes(q.toLowerCase())),
    [items, q],
  );

  const add = () => {
    const name = window.prompt(t("cat.name"));
    if (!name?.trim()) return;
    const next = [...items, { id: `c${Date.now()}`, name: name.trim(), published: true, order: items.length + 1 }];
    setItems(next);
    save(next);
    toast.success(t("form.created"));
  };

  const edit = (c: Category) => {
    const name = window.prompt(t("cat.name"), c.name);
    if (!name?.trim()) return;
    const next = items.map((i) => (i.id === c.id ? { ...i, name: name.trim() } : i));
    setItems(next);
    save(next);
    toast.success(t("form.updated"));
  };

  const remove = (c: Category) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    const next = items.filter((i) => i.id !== c.id);
    setItems(next);
    save(next);
    toast.success(t("form.deleted"));
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("cat.title")}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} / {items.length}
          </p>
        </div>
        <button onClick={add} className="admin-btn-primary inline-flex items-center gap-2">
          <Plus className="w-4 h-4" /> {t("cat.add")}
        </button>
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
            {filtered.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-5 py-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("cat.empty")}
                </td>
              </tr>
            ) : (
              filtered.map((c) => (
                <tr key={c.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-5 py-3 font-semibold">{c.name}</td>
                  <td className="px-5 py-3">
                    {c.published ? (
                      <Check className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />
                    ) : (
                      <X className="w-4 h-4" style={{ color: "hsl(var(--admin-danger))" }} />
                    )}
                  </td>
                  <td className="px-5 py-3">{c.order}</td>
                  <td className="px-5 py-3 text-right">
                    <button
                      onClick={() => edit(c)}
                      className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted mr-2"
                    >
                      <Pencil className="w-3.5 h-3.5" /> {t("common.edit")}
                    </button>
                    <button
                      onClick={() => remove(c)}
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
