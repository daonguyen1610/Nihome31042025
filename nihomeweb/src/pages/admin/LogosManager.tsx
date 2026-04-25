import { useEffect, useState } from "react";
import { Plus, Pencil, Trash2, ExternalLink } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";
import { clientLogos, partnerLogos, supplierLogos, type LogoItem } from "@/data/clients";

type Kind = "clients" | "partners" | "suppliers";

const seedFor = (k: Kind): LogoItem[] =>
  k === "clients" ? clientLogos : k === "partners" ? partnerLogos : supplierLogos;

const keyFor = (k: Kind) => `nicon_admin_logos_${k}_v1`;

const LogosManager = ({ kind, titleKey }: { kind: Kind; titleKey: string }) => {
  const { t } = useI18n();
  const [items, setItems] = useState<LogoItem[]>([]);

  useEffect(() => {
    try {
      const raw = localStorage.getItem(keyFor(kind));
      setItems(raw ? JSON.parse(raw) : seedFor(kind));
    } catch {
      setItems(seedFor(kind));
    }
  }, [kind]);

  const persist = (list: LogoItem[]) => {
    setItems(list);
    localStorage.setItem(keyFor(kind), JSON.stringify(list));
  };

  const add = () => {
    const name = window.prompt("Tên")?.trim();
    if (!name) return;
    const img = window.prompt("URL hình ảnh")?.trim() ?? "";
    const href = window.prompt("Liên kết (tuỳ chọn)")?.trim() || undefined;
    persist([{ name, img, href }, ...items]);
    toast.success(t("form.created"));
  };

  const edit = (item: LogoItem, idx: number) => {
    const name = window.prompt("Tên", item.name)?.trim();
    if (!name) return;
    const img = window.prompt("URL hình ảnh", item.img)?.trim() ?? item.img;
    const href = window.prompt("Liên kết (tuỳ chọn)", item.href ?? "")?.trim() || undefined;
    const next = [...items];
    next[idx] = { name, img, href };
    persist(next);
    toast.success(t("form.updated"));
  };

  const remove = (idx: number) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    persist(items.filter((_, i) => i !== idx));
    toast.success(t("form.deleted"));
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t(titleKey)}</h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {items.length} mục
          </p>
        </div>
        <button onClick={add} className="admin-btn-primary inline-flex items-center gap-2">
          <Plus className="w-4 h-4" /> {t("common.new")}
        </button>
      </div>

      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4">
        {items.map((item, i) => (
          <div key={`${item.name}-${i}`} className="admin-card p-4 group">
            <div className="aspect-[4/3] rounded-xl bg-white border flex items-center justify-center overflow-hidden mb-3" style={{ borderColor: "hsl(var(--admin-border))" }}>
              <img src={item.img} alt={item.name} className="max-w-full max-h-full object-contain" />
            </div>
            <p className="font-semibold text-sm truncate">{item.name}</p>
            {item.href && (
              <a href={item.href} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-xs mt-1" style={{ color: "hsl(var(--admin-primary))" }}>
                <ExternalLink className="w-3 h-3" /> link
              </a>
            )}
            <div className="flex gap-1 mt-3">
              <button onClick={() => edit(item, i)} className="flex-1 inline-flex items-center justify-center gap-1 text-xs font-bold px-2 py-1.5 rounded-lg hover:bg-muted">
                <Pencil className="w-3 h-3" /> {t("common.edit")}
              </button>
              <button onClick={() => remove(i)} className="flex-1 inline-flex items-center justify-center gap-1 text-xs font-bold px-2 py-1.5 rounded-lg hover:bg-muted" style={{ color: "hsl(var(--admin-danger))" }}>
                <Trash2 className="w-3 h-3" /> {t("common.delete")}
              </button>
            </div>
          </div>
        ))}
      </div>
    </AdminLayout>
  );
};

export default LogosManager;
