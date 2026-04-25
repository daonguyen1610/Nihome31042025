import { useMemo, useState } from "react";
import { Plus, Search as SearchIcon, Pencil, Trash2, X } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";
import { useProcesses } from "@/hooks/useContentApi";
import { adminApi } from "@/services/adminApi";

type ProcessItem = {
  id: number;
  groupKey: string;
  title: string;
  code?: string;
};

type Props = {
  groupKey: string;
  titleKey: string;
};

type EditorState = {
  open: boolean;
  mode: "create" | "edit";
  draft: { id?: number; title: string; code: string };
};

const emptyDraft = () => ({ title: "", code: "" });

const ProcessList = ({ groupKey, titleKey }: Props) => {
  const { t } = useI18n();
  const [q, setQ] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [editor, setEditor] = useState<EditorState>({
    open: false,
    mode: "create",
    draft: emptyDraft(),
  });

  const { data, loading, error, refetch } = useProcesses();
  const items = (data?.[groupKey] ?? []) as ProcessItem[];

  const filtered = useMemo(
    () =>
      items.filter(
        (i) =>
          i.title.toLowerCase().includes(q.toLowerCase()) ||
          (i.code ?? "").toLowerCase().includes(q.toLowerCase()),
      ),
    [items, q],
  );

  const openCreate = () => setEditor({ open: true, mode: "create", draft: emptyDraft() });
  const openEdit = (p: ProcessItem) =>
    setEditor({
      open: true,
      mode: "edit",
      draft: { id: p.id, title: p.title, code: p.code ?? "" },
    });

  const closeEditor = () =>
    setEditor((s) => ({ ...s, open: false, draft: emptyDraft() }));

  const updateDraft = (patch: Partial<EditorState["draft"]>) =>
    setEditor((s) => ({ ...s, draft: { ...s.draft, ...patch } }));

  const submit = async () => {
    if (!editor.draft.title.trim()) {
      toast.error(t("proc.title"));
      return;
    }

    setSubmitting(true);
    try {
      if (editor.mode === "create") {
        await adminApi.createProcess({
          groupKey,
          title: editor.draft.title.trim(),
          code: editor.draft.code.trim() || undefined,
          sortOrder: items.length,
        });
        toast.success(t("form.created"));
      } else if (editor.draft.id != null) {
        const current = items.find((x) => x.id === editor.draft.id);
        const sortOrder = current ? items.findIndex((x) => x.id === current.id) : 0;
        await adminApi.updateProcess(editor.draft.id, {
          groupKey,
          title: editor.draft.title.trim(),
          code: editor.draft.code.trim() || undefined,
          sortOrder,
        });
        toast.success(t("form.updated"));
      }

      closeEditor();
      await refetch();
    } catch {
      toast.error(t("common.error"));
    } finally {
      setSubmitting(false);
    }
  };

  const remove = async (p: ProcessItem) => {
    if (!window.confirm(t("form.confirmDelete"))) return;

    try {
      await adminApi.deleteProcess(p.id);
      toast.success(t("form.deleted"));
      await refetch();
    } catch {
      toast.error(t("common.error"));
    }
  };

  return (
    <AdminLayout>
      <div className="flex items-center justify-between mb-6 gap-4 flex-wrap">
        <div>
          <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">
            {t(titleKey)}
          </h1>
          <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
            {filtered.length} / {items.length}
          </p>
        </div>
        <button
          onClick={openCreate}
          className="admin-btn-primary inline-flex items-center justify-center gap-2 px-5 py-2.5 whitespace-nowrap leading-none"
        >
          <Plus className="w-4 h-4 shrink-0" />
          <span>{t("proc.add")}</span>
        </button>
      </div>

      <div className="admin-card p-5 mb-5">
        <div className="flex items-center gap-2 max-w-md">
          <SearchIcon className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t("proc.searchPh")}
            className="admin-input flex-1"
          />
        </div>
      </div>

      {loading ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("common.loading")}
        </div>
      ) : error ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-danger))" }}>
          {error}
        </div>
      ) : (
        <div className="space-y-3">
          {filtered.length === 0 ? (
            <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("proc.empty")}
            </div>
          ) : (
            filtered.map((p) => (
              <div
                key={p.id}
                className="admin-card flex items-center gap-4 px-5 py-4 hover:shadow-md transition"
              >
                <div
                  className="w-10 h-10 rounded-xl flex items-center justify-center shrink-0"
                  style={{
                    background:
                      "linear-gradient(135deg, hsl(var(--admin-primary) / 0.12), hsl(22 95% 58% / 0.1))",
                    color: "hsl(var(--admin-primary))",
                  }}
                >
                  <Pencil className="w-5 h-5" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="font-semibold truncate" style={{ color: "hsl(var(--admin-primary))" }}>
                    {p.code ? `${p.code} — ${p.title}` : p.title}
                  </p>
                </div>
                <div className="flex items-center gap-1 shrink-0">
                  <button
                    onClick={() => openEdit(p)}
                    className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted"
                  >
                    <Pencil className="w-3.5 h-3.5" /> {t("common.edit")}
                  </button>
                  <button
                    onClick={() => remove(p)}
                    className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted"
                    style={{ color: "hsl(var(--admin-danger))" }}
                  >
                    <Trash2 className="w-3.5 h-3.5" /> {t("common.delete")}
                  </button>
                </div>
              </div>
            ))
          )}
        </div>
      )}

      {editor.open && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50"
          onClick={closeEditor}
        >
          <div
            className="bg-background rounded-2xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-hidden flex flex-col"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center justify-between gap-4 p-5 border-b">
              <h2 className="font-display text-lg font-extrabold">
                {editor.mode === "create" ? t("proc.add") : t("proc.edit")}
              </h2>
              <button onClick={closeEditor} className="p-2 rounded-lg hover:bg-muted">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-5 overflow-y-auto space-y-4">
              <div>
                <label className="block text-sm font-bold mb-1.5">{t("proc.title")} *</label>
                <input
                  className="admin-input w-full"
                  value={editor.draft.title}
                  onChange={(e) => updateDraft({ title: e.target.value })}
                />
              </div>

              <div>
                <label className="block text-sm font-bold mb-1.5">{t("proc.code")}</label>
                <input
                  className="admin-input w-full"
                  value={editor.draft.code}
                  onChange={(e) => updateDraft({ code: e.target.value })}
                />
              </div>
            </div>

            <div className="p-5 border-t flex items-center justify-end gap-2">
              <button className="admin-btn-ghost" onClick={closeEditor}>
                {t("proc.cancel")}
              </button>
              <button className="admin-btn-primary" onClick={submit} disabled={submitting}>
                {submitting ? t("common.loading") : t("proc.save")}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
};

export default ProcessList;
