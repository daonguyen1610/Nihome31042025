import { useMemo, useState } from "react";
import { Search, Trash2, Check, Pencil } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";

type SeName = {
  id: number;
  name: string;
  entityId: number;
  entityName: string;
  isActive: boolean;
  language: string;
};

const KEY = "nicon_admin_se_names_v1";

const buildSeed = (): SeName[] => {
  const langs = ["English", "Viet Nam", "Korea", "China", "Japan"];
  const entities = ["Activities", "ProjectsUnderConstruction", "News", "Posts", "Services", "Categories"];
  return Array.from({ length: 120 }, (_, i) => ({
    id: 2400 + i,
    name: i % 7 === 0 ? `2025-${["entry", "post", "article"][i % 3]}-${i}` : `${["activity", "project", "news"][i % 3]}-${i}`,
    entityId: 40 + (i % 60),
    entityName: entities[i % entities.length],
    isActive: i % 9 !== 0,
    language: langs[i % langs.length],
  }));
};

const load = (): SeName[] => {
  try { const raw = localStorage.getItem(KEY); return raw ? JSON.parse(raw) : buildSeed(); } catch { return buildSeed(); }
};
const save = (items: SeName[]) => localStorage.setItem(KEY, JSON.stringify(items));

const SeNamesPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [items, setItems] = useState<SeName[]>(load);
  const [q, setQ] = useState("");
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [page, setPage] = useState(1);
  const perPage = 15;

  const filtered = useMemo(() =>
    items.filter((i) => !q || i.name.toLowerCase().includes(q.toLowerCase())),
    [items, q]
  );
  const totalPages = Math.max(1, Math.ceil(filtered.length / perPage));
  const pageRows = filtered.slice((page - 1) * perPage, page * perPage);

  const toggle = (id: number) => {
    const n = new Set(selected); n.has(id) ? n.delete(id) : n.add(id); setSelected(n);
  };
  const toggleAll = () => {
    if (pageRows.every((r) => selected.has(r.id))) {
      const n = new Set(selected); pageRows.forEach((r) => n.delete(r.id)); setSelected(n);
    } else {
      const n = new Set(selected); pageRows.forEach((r) => n.add(r.id)); setSelected(n);
    }
  };
  const removeSelected = () => {
    if (selected.size === 0) return;
    if (!confirm(t("form.confirmDelete"))) return;
    const next = items.filter((i) => !selected.has(i.id));
    setItems(next); save(next); setSelected(new Set());
    toast({ title: t("form.deleted") });
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-3 flex-wrap">
        <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("sys.se.title")}</h1>
        <button onClick={removeSelected} className="inline-flex items-center gap-2 text-sm font-bold px-4 py-2 rounded-lg text-white" style={{ background: "hsl(var(--admin-danger))" }}>
          <Trash2 className="w-4 h-4" /> {t("sys.queue.deleteSelected")}
        </button>
      </div>

      <div className="admin-card p-5 mb-5">
        <label className="block">
          <span className="text-xs font-bold mb-1 inline-block" style={{ color: "hsl(var(--admin-muted))" }}>{t("set.name")}</span>
          <div className="flex gap-2">
            <input value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} className="admin-input flex-1" placeholder={t("sys.se.searchPh")} />
            <button className="admin-btn-primary inline-flex items-center gap-2 px-4 py-2 text-sm">
              <Search className="w-4 h-4" /> {t("set.search")}
            </button>
          </div>
        </label>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm min-w-[900px]">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-4 py-3 w-10"><input type="checkbox" checked={pageRows.length > 0 && pageRows.every((r) => selected.has(r.id))} onChange={toggleAll} /></th>
                <th className="px-4 py-3 font-bold">ID</th>
                <th className="px-4 py-3 font-bold">{t("set.name")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.se.entityId")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.se.entityName")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.se.isActive")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.se.language")}</th>
                <th className="px-4 py-3 font-bold">{t("sys.se.editPage")}</th>
              </tr>
            </thead>
            <tbody>
              {pageRows.map((r) => (
                <tr key={r.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-4 py-3"><input type="checkbox" checked={selected.has(r.id)} onChange={() => toggle(r.id)} /></td>
                  <td className="px-4 py-3 text-xs font-mono">{r.id}</td>
                  <td className="px-4 py-3 text-xs font-semibold">{r.name}</td>
                  <td className="px-4 py-3 text-xs">{r.entityId}</td>
                  <td className="px-4 py-3 text-xs">{r.entityName}</td>
                  <td className="px-4 py-3">{r.isActive && <Check className="w-4 h-4" style={{ color: "hsl(var(--admin-primary))" }} />}</td>
                  <td className="px-4 py-3 text-xs">{r.language}</td>
                  <td className="px-4 py-3">
                    <button className="px-2 py-1 rounded-md text-xs font-bold bg-muted"><Pencil className="w-3.5 h-3.5" /></button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="flex items-center justify-between gap-4 px-6 py-3 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
          <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
            {(page - 1) * perPage + 1} - {Math.min(page * perPage, filtered.length)} of {filtered.length} items
          </p>
          <div className="flex items-center gap-1">
            {Array.from({ length: Math.min(totalPages, 8) }, (_, i) => i + 1).map((p) => (
              <button key={p} onClick={() => setPage(p)} className="w-8 h-8 rounded-md text-xs font-bold"
                style={p === page ? { background: "hsl(var(--admin-primary))", color: "white" } : { background: "hsl(var(--admin-bg))" }}>
                {p}
              </button>
            ))}
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

export default SeNamesPage;
