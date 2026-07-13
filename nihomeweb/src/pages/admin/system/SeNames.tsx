import { useMemo, useState } from "react";
import { Search, Trash2, Check, Pencil } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

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
  const allSelected = pageRows.length > 0 && pageRows.every((r) => selected.has(r.id));
  const someSelected = pageRows.some((r) => selected.has(r.id)) && !allSelected;

  const toggle = (id: number) => {
    const n = new Set(selected); if (n.has(id)) { n.delete(id); } else { n.add(id); } setSelected(n);
  };
  const toggleAll = (checked: boolean) => {
    const n = new Set(selected);
    pageRows.forEach((r) => (checked ? n.add(r.id) : n.delete(r.id)));
    setSelected(n);
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
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold">{t("sys.se.title")}</h1>
          <Button variant="destructive" onClick={removeSelected}>
            <Trash2 className="mr-1.5 h-4 w-4" /> {t("sys.queue.deleteSelected")}
          </Button>
        </header>

        <section className="rounded-lg border bg-card p-4">
          <div className="space-y-1.5">
            <Label htmlFor="se-name" className="text-xs">{t("set.name")}</Label>
            <div className="flex gap-2">
              <div className="relative flex-1">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  id="se-name"
                  value={q}
                  onChange={(e) => { setQ(e.target.value); setPage(1); }}
                  className="h-9 pl-9"
                  placeholder={t("sys.se.searchPh")}
                />
              </div>
              <Button size="sm">
                <Search className="mr-1.5 h-4 w-4" /> {t("set.search")}
              </Button>
            </div>
          </div>
        </section>

        <section className="overflow-hidden rounded-lg border bg-card">
          <div className="overflow-x-auto">
            <table className="w-full min-w-[900px] divide-y text-sm">
              <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                <tr>
                  <th className="w-10 px-4 py-3 text-left">
                    <Checkbox
                      checked={allSelected ? true : someSelected ? "indeterminate" : false}
                      onCheckedChange={(v) => toggleAll(v === true)}
                      aria-label={t("common.selectAll")}
                    />
                  </th>
                  <th className="px-4 py-3 text-left font-medium">ID</th>
                  <th className="px-4 py-3 text-left font-medium">{t("set.name")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.se.entityId")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.se.entityName")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.se.isActive")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.se.language")}</th>
                  <th className="px-4 py-3 text-left font-medium">{t("sys.se.editPage")}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {pageRows.map((r) => (
                  <tr key={r.id} className="hover:bg-muted/40 transition">
                    <td className="px-4 py-3">
                      <Checkbox
                        checked={selected.has(r.id)}
                        onCheckedChange={() => toggle(r.id)}
                        aria-label={`${t("common.selectAll")} · ${r.id}`}
                      />
                    </td>
                    <td className="px-4 py-3 font-mono text-xs">{r.id}</td>
                    <td className="px-4 py-3 text-xs font-medium">{r.name}</td>
                    <td className="px-4 py-3 text-xs">{r.entityId}</td>
                    <td className="px-4 py-3 text-xs">{r.entityName}</td>
                    <td className="px-4 py-3">{r.isActive && <Check className="h-4 w-4 text-emerald-600" />}</td>
                    <td className="px-4 py-3 text-xs">{r.language}</td>
                    <td className="px-4 py-3">
                      <Button size="icon" variant="ghost" className="h-8 w-8" aria-label={t("common.edit")}>
                        <Pencil className="h-3.5 w-3.5" />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <div className="flex items-center justify-between gap-4 border-t px-4 py-3">
            <p className="text-xs text-muted-foreground">
              {(page - 1) * perPage + 1} - {Math.min(page * perPage, filtered.length)} of {filtered.length} items
            </p>
            <div className="flex items-center gap-1">
              {Array.from({ length: Math.min(totalPages, 8) }, (_, i) => i + 1).map((p) => (
                <Button
                  key={p}
                  size="sm"
                  variant={p === page ? "default" : "ghost"}
                  className="h-8 w-8 p-0"
                  onClick={() => setPage(p)}
                >
                  {p}
                </Button>
              ))}
            </div>
          </div>
        </section>
      </div>
    </AdminLayout>
  );
};

export default SeNamesPage;
