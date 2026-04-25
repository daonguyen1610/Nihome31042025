import { useState } from "react";
import { Plus, Pencil, Check, X, Trash2 } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  getLanguages,
  saveLanguages,
  newId,
  type Language,
} from "@/lib/settingsStore";

const blank: Language = { id: "", name: "", flag: "🌐", culture: "", displayOrder: 0, published: true };

const LanguagesPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [rows, setRows] = useState<Language[]>(() => getLanguages());
  const [editing, setEditing] = useState<Language | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<Language>(blank);

  const persist = (next: Language[]) => { setRows(next); saveLanguages(next); };
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
      <div className="flex items-center justify-between mb-6">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">
          {t("set.languages")}
        </h1>
        <button onClick={() => { setDraft(blank); setAdding(true); }} className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm">
          <Plus className="w-4 h-4" /> {t("set.add")}
        </button>
      </div>

      <div className="admin-card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-6 py-3 font-bold">Name</th>
                <th className="px-6 py-3 font-bold">{t("set.flag")}</th>
                <th className="px-6 py-3 font-bold">{t("set.culture")}</th>
                <th className="px-6 py-3 font-bold">{t("set.displayOrder")}</th>
                <th className="px-6 py-3 font-bold">{t("set.published")}</th>
                <th className="px-6 py-3 font-bold w-40">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-6 py-4 font-semibold">{r.name}</td>
                  <td className="px-6 py-4 text-2xl">{r.flag}</td>
                  <td className="px-6 py-4 font-mono text-xs">{r.culture}</td>
                  <td className="px-6 py-4">{r.displayOrder}</td>
                  <td className="px-6 py-4">
                    {r.published ? (
                      <Check className="w-5 h-5" style={{ color: "hsl(var(--admin-primary))" }} />
                    ) : (
                      <X className="w-5 h-5" style={{ color: "hsl(var(--admin-danger))" }} />
                    )}
                  </td>
                  <td className="px-6 py-4">
                    <div className="flex gap-2">
                      <button onClick={() => { setDraft(r); setEditing(r); }} className="px-2.5 py-1.5 rounded-md text-xs font-bold bg-muted inline-flex items-center gap-1">
                        <Pencil className="w-3 h-3" /> {t("common.edit")}
                      </button>
                      <button onClick={() => remove(r.id)} className="px-2.5 py-1.5 rounded-md text-xs font-bold inline-flex items-center gap-1 text-white" style={{ background: "hsl(var(--admin-danger))" }}>
                        <Trash2 className="w-3 h-3" /> {t("common.delete")}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {(adding || editing) && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" onClick={() => { setAdding(false); setEditing(null); }}>
          <div className="bg-white rounded-2xl w-full max-w-lg p-6" onClick={(e) => e.stopPropagation()}>
            <h3 className="font-display text-xl font-extrabold mb-4">{adding ? t("set.add") : t("common.edit")}</h3>
            <div className="space-y-3">
              {[
                ["name", "Name", "text"],
                ["flag", "Flag (emoji)", "text"],
                ["culture", "Language culture", "text"],
                ["displayOrder", t("set.displayOrder"), "number"],
              ].map(([key, label, type]) => (
                <div key={key as string}>
                  <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>{label}</label>
                  <input type={type as string} value={(draft as Record<string, string | number | boolean>)[key as string]} onChange={(e) => setDraft({ ...draft, [key as string]: type === "number" ? +e.target.value : e.target.value } as Language)} className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none" style={{ borderColor: "hsl(var(--admin-border))" }} />
                </div>
              ))}
              <label className="flex items-center gap-2 mt-2">
                <input type="checkbox" checked={draft.published} onChange={(e) => setDraft({ ...draft, published: e.target.checked })} />
                <span className="text-sm font-semibold">Published</span>
              </label>
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

export default LanguagesPage;
