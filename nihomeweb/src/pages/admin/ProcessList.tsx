import { useEffect, useMemo, useRef, useState } from "react";
import { Plus, Search as SearchIcon, Pencil, Trash2, FileText, Eye, X, Upload, Image as ImageIcon } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { toast } from "sonner";

export type ProcessItem = {
  id: string;
  title: string;
  code?: string;
  fileUrl?: string;
  contentType?: "text" | "image";
  content?: string; // text body or image data URL / URL
};

type Props = {
  storageKey: string;
  titleKey: string;
  seed: ProcessItem[];
};

const load = (key: string, seed: ProcessItem[]): ProcessItem[] => {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as ProcessItem[]) : seed;
  } catch {
    return seed;
  }
};

type EditorState = {
  open: boolean;
  mode: "create" | "edit";
  draft: ProcessItem;
};

const emptyDraft = (): ProcessItem => ({
  id: "",
  title: "",
  code: "",
  contentType: "text",
  content: "",
});

const ProcessList = ({ storageKey, titleKey, seed }: Props) => {
  const { t } = useI18n();
  const [items, setItems] = useState<ProcessItem[]>(() => load(storageKey, seed));
  const [q, setQ] = useState("");
  const [viewing, setViewing] = useState<ProcessItem | null>(null);
  const [editor, setEditor] = useState<EditorState>({ open: false, mode: "create", draft: emptyDraft() });
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setItems(load(storageKey, seed));
    setQ("");
  }, [storageKey, seed]);

  const persist = (next: ProcessItem[]) => {
    setItems(next);
    try {
      localStorage.setItem(storageKey, JSON.stringify(next));
    } catch {
      /* ignore */
    }
  };

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
      draft: { ...p, contentType: p.contentType ?? "text", content: p.content ?? "" },
    });
  const closeEditor = () => setEditor((s) => ({ ...s, open: false }));

  const updateDraft = (patch: Partial<ProcessItem>) =>
    setEditor((s) => ({ ...s, draft: { ...s.draft, ...patch } }));

  const handleFile = (file: File) => {
    if (!file.type.startsWith("image/")) {
      toast.error("File must be an image");
      return;
    }
    const reader = new FileReader();
    reader.onload = () => updateDraft({ content: reader.result as string });
    reader.readAsDataURL(file);
  };

  const submit = () => {
    const { draft, mode } = editor;
    if (!draft.title.trim()) {
      toast.error(t("proc.title"));
      return;
    }
    const clean: ProcessItem = {
      ...draft,
      title: draft.title.trim(),
      code: draft.code?.trim() || undefined,
      content: draft.content?.trim() || undefined,
    };
    if (mode === "create") {
      persist([...items, { ...clean, id: `p${Date.now()}` }]);
      toast.success(t("form.created"));
    } else {
      persist(items.map((i) => (i.id === clean.id ? clean : i)));
      toast.success(t("form.updated"));
    }
    closeEditor();
  };

  const remove = (p: ProcessItem) => {
    if (!window.confirm(t("form.confirmDelete"))) return;
    persist(items.filter((i) => i.id !== p.id));
    toast.success(t("form.deleted"));
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
                {p.contentType === "image" ? <ImageIcon className="w-5 h-5" /> : <FileText className="w-5 h-5" />}
              </div>
              <div className="min-w-0 flex-1">
                <p className="font-semibold truncate" style={{ color: "hsl(var(--admin-primary))" }}>
                  {p.code ? `${p.code} — ${p.title}` : p.title}
                </p>
              </div>
              <div className="flex items-center gap-1 shrink-0">
                <button
                  onClick={() => setViewing(p)}
                  className="inline-flex items-center gap-1 text-xs font-bold px-3 py-1.5 rounded-lg hover:bg-muted"
                >
                  <Eye className="w-3.5 h-3.5" /> {t("proc.view")}
                </button>
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

      {/* View detail modal */}
      {viewing && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50"
          onClick={() => setViewing(null)}
        >
          <div
            className="bg-background rounded-2xl shadow-2xl max-w-3xl w-full max-h-[90vh] overflow-hidden flex flex-col"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-start justify-between gap-4 p-5 border-b">
              <div className="min-w-0">
                {viewing.code && (
                  <p className="text-xs font-bold uppercase tracking-wider" style={{ color: "hsl(var(--admin-primary))" }}>
                    {viewing.code}
                  </p>
                )}
                <h2 className="font-display text-xl font-extrabold mt-1 break-words">{viewing.title}</h2>
              </div>
              <button onClick={() => setViewing(null)} className="p-2 rounded-lg hover:bg-muted shrink-0">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-5 overflow-y-auto">
              {viewing.contentType === "image" && viewing.content ? (
                <img src={viewing.content} alt={viewing.title} className="w-full h-auto rounded-lg border" />
              ) : viewing.content ? (
                <div className="prose prose-sm max-w-none whitespace-pre-wrap">{viewing.content}</div>
              ) : (
                <p className="text-sm text-center py-10" style={{ color: "hsl(var(--admin-muted))" }}>
                  {t("proc.noContent")}
                </p>
              )}
            </div>
          </div>
        </div>
      )}

      {/* Editor modal */}
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
                  value={editor.draft.code ?? ""}
                  onChange={(e) => updateDraft({ code: e.target.value })}
                />
              </div>

              <div>
                <label className="block text-sm font-bold mb-1.5">{t("proc.contentType")}</label>
                <div className="flex gap-2">
                  {(["text", "image"] as const).map((tp) => (
                    <button
                      key={tp}
                      type="button"
                      onClick={() => updateDraft({ contentType: tp, content: "" })}
                      className={`px-4 py-2 rounded-lg text-sm font-bold border transition ${
                        editor.draft.contentType === tp
                          ? "bg-primary text-primary-foreground border-primary"
                          : "bg-background hover:bg-muted"
                      }`}
                    >
                      {tp === "text" ? t("proc.text") : t("proc.image")}
                    </button>
                  ))}
                </div>
              </div>

              {editor.draft.contentType === "image" ? (
                <div className="space-y-3">
                  <label className="block text-sm font-bold">{t("proc.content")}</label>
                  <input
                    ref={fileInputRef}
                    type="file"
                    accept="image/*"
                    className="hidden"
                    onChange={(e) => e.target.files?.[0] && handleFile(e.target.files[0])}
                  />
                  <button
                    type="button"
                    onClick={() => fileInputRef.current?.click()}
                    className="inline-flex items-center gap-2 px-4 py-2 rounded-lg border hover:bg-muted text-sm font-bold"
                  >
                    <Upload className="w-4 h-4" /> {t("proc.uploadImage")}
                  </button>
                  <input
                    type="url"
                    placeholder={t("proc.imageUrl")}
                    className="admin-input w-full"
                    value={editor.draft.content?.startsWith("data:") ? "" : editor.draft.content ?? ""}
                    onChange={(e) => updateDraft({ content: e.target.value })}
                  />
                  {editor.draft.content && (
                    <div className="border rounded-lg p-2">
                      <img
                        src={editor.draft.content}
                        alt="preview"
                        className="max-h-64 mx-auto rounded"
                      />
                    </div>
                  )}
                </div>
              ) : (
                <div>
                  <label className="block text-sm font-bold mb-1.5">{t("proc.content")}</label>
                  <textarea
                    className="admin-input w-full min-h-[200px] resize-y"
                    placeholder={t("proc.contentPh")}
                    value={editor.draft.content ?? ""}
                    onChange={(e) => updateDraft({ content: e.target.value })}
                  />
                </div>
              )}
            </div>

            <div className="flex items-center justify-end gap-2 p-5 border-t">
              <button
                onClick={closeEditor}
                className="px-4 py-2 rounded-lg border text-sm font-bold hover:bg-muted"
              >
                {t("proc.cancel")}
              </button>
              <button
                onClick={submit}
                className="admin-btn-primary inline-flex items-center justify-center gap-2 px-5 py-2.5 whitespace-nowrap leading-none"
              >
                {t("proc.save")}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
};

export default ProcessList;
