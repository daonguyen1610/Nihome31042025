import { useMemo, useState } from "react";
import { Plus, Pencil, Check, X, Trash2, Download, Upload } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  getCountries,
  saveCountries,
  newId,
  type Country,
} from "@/lib/settingsStore";

const blank: Country = {
  id: "", name: "", allowsBilling: true, allowsShipping: true,
  twoLetterCode: "", threeLetterCode: "", numericCode: 0,
  subjectToVat: false, numberOfStates: 0, displayOrder: 100, published: true,
};

const Boolean = ({ on }: { on: boolean }) =>
  on ? (
    <Check className="w-5 h-5" style={{ color: "hsl(var(--admin-primary))" }} />
  ) : (
    <X className="w-5 h-5" style={{ color: "hsl(var(--admin-danger))" }} />
  );

const CountriesPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [rows, setRows] = useState<Country[]>(() => getCountries());
  const [editing, setEditing] = useState<Country | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<Country>(blank);
  const [page, setPage] = useState(1);
  const perPage = 15;

  const totalPages = Math.max(1, Math.ceil(rows.length / perPage));
  const pageRows = useMemo(() => rows.slice((page - 1) * perPage, page * perPage), [rows, page]);

  const persist = (next: Country[]) => { setRows(next); saveCountries(next); };
  const remove = (id: string) => {
    if (!confirm(t("form.confirmDelete"))) return;
    persist(rows.filter((r) => r.id !== id));
    toast({ title: t("form.deleted") });
  };
  const submit = () => {
    if (!draft.name.trim()) return;
    if (adding) {
      persist([{ ...draft, id: newId() }, ...rows]);
      toast({ title: t("form.created") });
    } else if (editing) {
      persist(rows.map((r) => (r.id === editing.id ? draft : r)));
      toast({ title: t("form.updated") });
    }
    setAdding(false); setEditing(null);
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-3 flex-wrap">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">
          {t("set.countries")}
        </h1>
        <div className="flex gap-2 flex-wrap">
          <button onClick={() => { setDraft(blank); setAdding(true); }} className="admin-btn-primary inline-flex items-center gap-2 px-4 py-2 text-sm">
            <Plus className="w-4 h-4" /> {t("set.add")}
          </button>
          <button className="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-bold text-white" style={{ background: "hsl(142 71% 40%)" }}>
            <Upload className="w-4 h-4" /> Export CSV
          </button>
          <button className="inline-flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-bold text-white" style={{ background: "hsl(142 71% 40%)" }}>
            <Download className="w-4 h-4" /> Import CSV
          </button>
        </div>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-4 py-3 font-bold">Name</th>
                <th className="px-4 py-3 font-bold">Billing</th>
                <th className="px-4 py-3 font-bold">Shipping</th>
                <th className="px-4 py-3 font-bold">2-letter</th>
                <th className="px-4 py-3 font-bold">3-letter</th>
                <th className="px-4 py-3 font-bold">Numeric</th>
                <th className="px-4 py-3 font-bold">VAT</th>
                <th className="px-4 py-3 font-bold">States</th>
                <th className="px-4 py-3 font-bold">Order</th>
                <th className="px-4 py-3 font-bold">{t("set.published")}</th>
                <th className="px-4 py-3 font-bold w-32">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {pageRows.map((r) => (
                <tr key={r.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-4 py-3 font-semibold">{r.name}</td>
                  <td className="px-4 py-3"><Boolean on={r.allowsBilling} /></td>
                  <td className="px-4 py-3"><Boolean on={r.allowsShipping} /></td>
                  <td className="px-4 py-3 font-mono text-xs">{r.twoLetterCode}</td>
                  <td className="px-4 py-3 font-mono text-xs">{r.threeLetterCode}</td>
                  <td className="px-4 py-3">{r.numericCode}</td>
                  <td className="px-4 py-3"><Boolean on={r.subjectToVat} /></td>
                  <td className="px-4 py-3">{r.numberOfStates}</td>
                  <td className="px-4 py-3">{r.displayOrder}</td>
                  <td className="px-4 py-3"><Boolean on={r.published} /></td>
                  <td className="px-4 py-3">
                    <div className="flex gap-1">
                      <button onClick={() => { setDraft(r); setEditing(r); }} className="px-2 py-1 rounded-md text-xs font-bold bg-muted">
                        <Pencil className="w-3 h-3" />
                      </button>
                      <button onClick={() => remove(r.id)} className="px-2 py-1 rounded-md text-xs font-bold text-white" style={{ background: "hsl(var(--admin-danger))" }}>
                        <Trash2 className="w-3 h-3" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="flex items-center justify-between gap-4 px-6 py-3 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
          <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
            {(page - 1) * perPage + 1} - {Math.min(page * perPage, rows.length)} of {rows.length} items
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

      {(adding || editing) && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" onClick={() => { setAdding(false); setEditing(null); }}>
          <div className="bg-white rounded-2xl w-full max-w-2xl p-6 max-h-[90vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <h3 className="font-display text-xl font-extrabold mb-4">{adding ? t("set.add") : t("common.edit")}</h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {[
                ["name", "Name", "text"],
                ["twoLetterCode", "Two letter ISO code", "text"],
                ["threeLetterCode", "Three letter ISO code", "text"],
                ["numericCode", "Numeric ISO code", "number"],
                ["numberOfStates", "Number of states", "number"],
                ["displayOrder", "Display order", "number"],
              ].map(([key, label, type]) => (
                <div key={key as string}>
                  <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>{label}</label>
                  <input type={type as string} value={(draft as any)[key as string]} onChange={(e) => setDraft({ ...draft, [key as string]: type === "number" ? +e.target.value : e.target.value } as Country)} className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none" style={{ borderColor: "hsl(var(--admin-border))" }} />
                </div>
              ))}
              {([
                ["allowsBilling", "Allows billing"],
                ["allowsShipping", "Allows shipping"],
                ["subjectToVat", "Subject to VAT"],
                ["published", "Published"],
              ] as const).map(([key, label]) => (
                <label key={key} className="flex items-center gap-2 mt-2">
                  <input type="checkbox" checked={draft[key]} onChange={(e) => setDraft({ ...draft, [key]: e.target.checked })} />
                  <span className="text-sm font-semibold">{label}</span>
                </label>
              ))}
            </div>
            <div className="flex justify-end gap-2 mt-5">
              <button onClick={() => { setAdding(false); setEditing(null); }} className="px-4 py-2 rounded-lg text-sm font-bold bg-muted">{t("common.cancel")}</button>
              <button onClick={submit} className="admin-btn-primary px-4 py-2 text-sm">{t("proc.save")}</button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
};

export default CountriesPage;
