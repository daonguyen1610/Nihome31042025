import { useMemo, useState } from "react";
import { Plus, Pencil, Trash2, Search } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import {
  getAllSettings,
  saveAllSettings,
  newId,
  type SettingRow as SRow,
} from "@/lib/settingsStore";

const AllSettingsPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();
  const [rows, setRows] = useState<SRow[]>(() => getAllSettings());
  const [qName, setQName] = useState("");
  const [qValue, setQValue] = useState("");
  const [page, setPage] = useState(1);
  const perPage = 15;

  const [editing, setEditing] = useState<SRow | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<SRow>({ id: "", name: "", value: "", store: "All stores" });

  const filtered = useMemo(
    () =>
      rows.filter(
        (r) =>
          r.name.toLowerCase().includes(qName.toLowerCase()) &&
          r.value.toLowerCase().includes(qValue.toLowerCase()),
      ),
    [rows, qName, qValue],
  );

  const totalPages = Math.max(1, Math.ceil(filtered.length / perPage));
  const pageRows = filtered.slice((page - 1) * perPage, page * perPage);

  const persist = (next: SRow[]) => {
    setRows(next);
    saveAllSettings(next);
  };

  const startAdd = () => {
    setDraft({ id: newId(), name: "", value: "", store: "All stores" });
    setAdding(true);
  };
  const startEdit = (r: SRow) => {
    setDraft({ ...r });
    setEditing(r);
  };
  const remove = (id: string) => {
    if (!confirm(t("form.confirmDelete"))) return;
    persist(rows.filter((r) => r.id !== id));
    toast({ title: t("form.deleted") });
  };
  const submit = () => {
    if (!draft.name.trim()) return;
    if (adding) {
      persist([{ ...draft }, ...rows]);
      toast({ title: t("form.created") });
    } else if (editing) {
      persist(rows.map((r) => (r.id === editing.id ? draft : r)));
      toast({ title: t("form.updated") });
    }
    setAdding(false);
    setEditing(null);
  };

  return (
    <AdminLayout>
      <div className="mb-6">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">
          {t("set.all")}
        </h1>
      </div>

      {/* Search */}
      <div className="admin-card p-6 mb-5">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="text-xs uppercase tracking-wider font-bold mb-2 block" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("set.name")}
            </label>
            <input
              value={qName}
              onChange={(e) => setQName(e.target.value)}
              className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none"
              style={{ borderColor: "hsl(var(--admin-border))" }}
            />
          </div>
          <div>
            <label className="text-xs uppercase tracking-wider font-bold mb-2 block" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("set.value")}
            </label>
            <input
              value={qValue}
              onChange={(e) => setQValue(e.target.value)}
              className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none"
              style={{ borderColor: "hsl(var(--admin-border))" }}
            />
          </div>
        </div>
        <div className="mt-4">
          <button
            onClick={() => setPage(1)}
            className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm"
          >
            <Search className="w-4 h-4" /> {t("set.search")}
          </button>
        </div>
      </div>

      {/* Table */}
      <div className="admin-card overflow-hidden">
        <div className="px-6 py-3 border-b flex items-center justify-between" style={{ borderColor: "hsl(var(--admin-border))" }}>
          <button onClick={startAdd} className="admin-btn-primary inline-flex items-center gap-2 px-4 py-2 text-sm">
            <Plus className="w-4 h-4" /> {t("set.addRow")}
          </button>
          <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} {t("common.showing")}
          </p>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead style={{ background: "hsl(var(--admin-bg))" }}>
              <tr className="text-left">
                <th className="px-6 py-3 font-bold">{t("set.name")}</th>
                <th className="px-6 py-3 font-bold">{t("set.value")}</th>
                <th className="px-6 py-3 font-bold">{t("set.store")}</th>
                <th className="px-6 py-3 font-bold w-40">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {pageRows.map((r) => (
                <tr key={r.id} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                  <td className="px-6 py-3 font-mono text-xs">{r.name}</td>
                  <td className="px-6 py-3">{r.value}</td>
                  <td className="px-6 py-3">{r.store}</td>
                  <td className="px-6 py-3">
                    <div className="flex gap-2">
                      <button onClick={() => startEdit(r)} className="px-2.5 py-1.5 rounded-md text-xs font-bold bg-muted inline-flex items-center gap-1">
                        <Pencil className="w-3 h-3" /> {t("common.edit")}
                      </button>
                      <button onClick={() => remove(r.id)} className="px-2.5 py-1.5 rounded-md text-xs font-bold inline-flex items-center gap-1 text-white" style={{ background: "hsl(var(--admin-danger))" }}>
                        <Trash2 className="w-3 h-3" /> {t("common.delete")}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {pageRows.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-6 py-10 text-center text-sm" style={{ color: "hsl(var(--admin-muted))" }}>
                    {t("posts.empty")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* pagination */}
        <div className="flex items-center justify-between gap-4 px-6 py-3 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
          <p className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
            {(page - 1) * perPage + 1} - {Math.min(page * perPage, filtered.length)} of {filtered.length} items
          </p>
          <div className="flex items-center gap-1">
            {Array.from({ length: Math.min(totalPages, 8) }, (_, i) => i + 1).map((p) => (
              <button
                key={p}
                onClick={() => setPage(p)}
                className="w-8 h-8 rounded-md text-xs font-bold"
                style={
                  p === page
                    ? { background: "hsl(var(--admin-primary))", color: "white" }
                    : { background: "hsl(var(--admin-bg))" }
                }
              >
                {p}
              </button>
            ))}
          </div>
        </div>
      </div>

      {/* Editor modal */}
      {(adding || editing) && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" onClick={() => { setAdding(false); setEditing(null); }}>
          <div className="bg-white rounded-2xl w-full max-w-lg p-6" onClick={(e) => e.stopPropagation()}>
            <h3 className="font-display text-xl font-extrabold mb-4">
              {adding ? t("set.addRow") : t("common.edit")}
            </h3>
            <div className="space-y-3">
              <div>
                <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>{t("set.name")}</label>
                <input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none" style={{ borderColor: "hsl(var(--admin-border))" }} />
              </div>
              <div>
                <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>{t("set.value")}</label>
                <input value={draft.value} onChange={(e) => setDraft({ ...draft, value: e.target.value })} className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none" style={{ borderColor: "hsl(var(--admin-border))" }} />
              </div>
              <div>
                <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>{t("set.store")}</label>
                <input value={draft.store} onChange={(e) => setDraft({ ...draft, store: e.target.value })} className="w-full rounded-lg px-3 py-2 text-sm bg-white border outline-none" style={{ borderColor: "hsl(var(--admin-border))" }} />
              </div>
            </div>
            <div className="flex justify-end gap-2 mt-5">
              <button onClick={() => { setAdding(false); setEditing(null); }} className="px-4 py-2 rounded-lg text-sm font-bold bg-muted">
                {t("common.cancel")}
              </button>
              <button onClick={submit} className="admin-btn-primary px-4 py-2 text-sm">
                {t("proc.save")}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
};

export default AllSettingsPage;
