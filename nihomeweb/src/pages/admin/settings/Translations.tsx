import { useCallback, useEffect, useState } from "react";
import {
  Search,
  Plus,
  Pencil,
  Trash2,
  Languages as LanguagesIcon,
  FileText,
  ChevronDown,
  Check,
  X,
  Loader2,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import api from "@/lib/api";

/* ─── Types ─────────────────────────────────────── */

interface TranslationPair {
  key: string;
  category?: string;
  vietnameseValue: string;
  translations: Record<string, string>;
  createdAt: string;
}

interface EntityTypeInfo {
  type: string;
  fields: string[];
}

interface EntityItem {
  id: number;
  title: string;
  description?: string;
  hasTranslation: boolean;
  translationCount: number;
  expectedFields: number;
}

/* ─── Constants ──────────────────────────────────── */

const SUPPORTED_LANGS = ["en", "zh", "ja"] as const;
const LANG_LABELS: Record<string, string> = { en: "English", zh: "中文", ja: "日本語" };
const PAGE_SIZE = 20;

/* ─── Component ──────────────────────────────────── */

const TranslationsPage = () => {
  const { t } = useI18n();
  const { toast } = useToast();

  const [tab, setTab] = useState<"static" | "entity">("static");

  /* ── Static translations state ── */
  const [pairs, setPairs] = useState<TranslationPair[]>([]);
  const [categories, setCategories] = useState<string[]>([]);
  const [filterCat, setFilterCat] = useState("");
  const [searchQ, setSearchQ] = useState("");
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);

  /* ── Static modal ── */
  const [modalOpen, setModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState<"add" | "edit">("add");
  const [draft, setDraft] = useState({
    key: "",
    category: "",
    vi: "",
    en: "",
    zh: "",
    ja: "",
  });

  /* ── Entity translations state ── */
  const [entityTypes, setEntityTypes] = useState<EntityTypeInfo[]>([]);
  const [selectedType, setSelectedType] = useState("");
  const [entityItems, setEntityItems] = useState<EntityItem[]>([]);
  const [entityLoading, setEntityLoading] = useState(false);

  /* ── Entity translate modal ── */
  const [entityModalOpen, setEntityModalOpen] = useState(false);
  const [entityModalItem, setEntityModalItem] = useState<EntityItem | null>(null);
  const [entityModalType, setEntityModalType] = useState<EntityTypeInfo | null>(null);
  const [entityOriginal, setEntityOriginal] = useState<Record<string, string>>({});
  const [entityTranslations, setEntityTranslations] = useState<Record<string, Record<string, string>>>({});
  const [entityModalLang, setEntityModalLang] = useState("en");
  const [entitySaving, setEntitySaving] = useState(false);

  /* ─── Fetch static translations ─── */
  const fetchPairs = useCallback(async () => {
    setLoading(true);
    try {
      const params: Record<string, string> = {};
      if (filterCat) params.category = filterCat;
      if (searchQ) params.search = searchQ;
      const { data } = await api.get<TranslationPair[]>("/translations/admin", { params });
      setPairs(data);
    } catch {
      toast({ title: "Error", description: "Failed to load translations", variant: "destructive" });
    } finally {
      setLoading(false);
    }
  }, [filterCat, searchQ, toast]);

  const fetchCategories = useCallback(async () => {
    try {
      const { data } = await api.get<string[]>("/translations/categories");
      setCategories(data);
    } catch { /* ignore */ }
  }, []);

  useEffect(() => {
    fetchPairs();
    fetchCategories();
  }, [fetchPairs, fetchCategories]);

  /* ─── Fetch entity types ─── */
  useEffect(() => {
    if (tab !== "entity") return;
    api.get<EntityTypeInfo[]>("/translations/entity/types").then(({ data }) => {
      setEntityTypes(data);
      if (data.length > 0 && !selectedType) setSelectedType(data[0].type);
    });
  }, [tab]);

  /* ─── Fetch entities for selected type ─── */
  const fetchEntities = useCallback(async () => {
    if (!selectedType) return;
    setEntityLoading(true);
    try {
      const { data } = await api.get<{ items: EntityItem[] }>(`/translations/entity/${selectedType}`);
      setEntityItems(data?.items ?? []);
    } catch {
      setEntityItems([]);
    } finally {
      setEntityLoading(false);
    }
  }, [selectedType]);

  useEffect(() => {
    if (tab === "entity" && selectedType) fetchEntities();
  }, [tab, selectedType, fetchEntities]);

  /* ─── Static CRUD handlers ─── */
  const openAdd = () => {
    setDraft({ key: "", category: "", vi: "", en: "", zh: "", ja: "" });
    setModalMode("add");
    setModalOpen(true);
  };

  const openEdit = (p: TranslationPair) => {
    setDraft({
      key: p.key,
      category: p.category ?? "",
      vi: p.vietnameseValue,
      en: p.translations["en"] ?? "",
      zh: p.translations["zh"] ?? "",
      ja: p.translations["ja"] ?? "",
    });
    setModalMode("edit");
    setModalOpen(true);
  };

  const savePair = async () => {
    if (!draft.key.trim() || !draft.vi.trim()) return;
    try {
      const translations: Record<string, string> = {};
      if (draft.en) translations.en = draft.en;
      if (draft.zh) translations.zh = draft.zh;
      if (draft.ja) translations.ja = draft.ja;
      await api.post("/translations/pair", {
        key: draft.key,
        vietnameseValue: draft.vi,
        translations,
        category: draft.category || null,
      });
      toast({ title: modalMode === "add" ? t("form.created") : t("form.updated") });
      setModalOpen(false);
      fetchPairs();
    } catch {
      toast({ title: t("auth.error"), variant: "destructive" });
    }
  };

  const deletePair = async (key: string) => {
    if (!confirm(t("form.confirmDelete"))) return;
    try {
      await api.delete(`/translations/key/${encodeURIComponent(key)}`);
      toast({ title: t("form.deleted") });
      fetchPairs();
    } catch {
      toast({ title: t("auth.error"), variant: "destructive" });
    }
  };

  /* ─── Entity translate modal ─── */
  const openEntityTranslate = async (item: EntityItem) => {
    const typeInfo = entityTypes.find((et) => et.type === selectedType);
    if (!typeInfo) return;
    setEntityModalItem(item);
    setEntityModalType(typeInfo);
    setEntityModalLang("en");
    try {
      const { data } = await api.get(`/translations/entity/${selectedType}/${item.id}`);
      setEntityOriginal(data.original ?? {});
      setEntityTranslations(data.translations ?? {});
    } catch {
      setEntityOriginal({});
      setEntityTranslations({});
    }
    setEntityModalOpen(true);
  };

  const saveEntityTranslations = async () => {
    if (!entityModalItem || !entityModalType) return;
    setEntitySaving(true);
    try {
      const fields = entityTranslations[entityModalLang] ?? {};
      await api.post(`/translations/entity/${entityModalType.type}/${entityModalItem.id}`, {
        languageCode: entityModalLang,
        translations: fields,
      });
      toast({ title: t("form.updated") });
      setEntityModalOpen(false);
      fetchEntities();
    } catch {
      toast({ title: t("auth.error"), variant: "destructive" });
    } finally {
      setEntitySaving(false);
    }
  };

  const updateEntityField = (field: string, value: string) => {
    setEntityTranslations((prev) => ({
      ...prev,
      [entityModalLang]: { ...(prev[entityModalLang] ?? {}), [field]: value },
    }));
  };

  /* ─── Pagination ─── */
  const totalPages = Math.ceil(pairs.length / PAGE_SIZE);
  const paged = pairs.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  /* ─── Render ─── */
  return (
    <AdminLayout>
      {/* Header */}
      <div className="mb-6">
        <h1 className="font-display text-3xl lg:text-4xl font-extrabold tracking-tight">
          {t("set.languages")} — Translations
        </h1>
        <p className="text-sm mt-1" style={{ color: "hsl(var(--admin-muted))" }}>
          Manage all UI translation keys and content translations
        </p>
      </div>

      {/* Tabs */}
      <div className="flex gap-1 mb-5 p-1 rounded-xl w-fit" style={{ background: "hsl(var(--admin-bg))" }}>
        {[
          { id: "static" as const, label: "UI Translations", icon: LanguagesIcon },
          { id: "entity" as const, label: "Content Translations", icon: FileText },
        ].map((tb) => (
          <button
            key={tb.id}
            onClick={() => setTab(tb.id)}
            className="inline-flex items-center gap-2 px-4 py-2 text-sm font-bold rounded-lg transition-all"
            style={
              tab === tb.id
                ? { background: "hsl(var(--admin-primary))", color: "#fff" }
                : { color: "hsl(var(--admin-muted))" }
            }
          >
            <tb.icon className="w-4 h-4" /> {tb.label}
          </button>
        ))}
      </div>

      {/* ════════ TAB 1: Static UI Translations ════════ */}
      {tab === "static" && (
        <>
          {/* Toolbar */}
          <div className="flex flex-wrap items-center gap-3 mb-4">
            <div
              className="flex items-center gap-2 rounded-xl px-4 py-2 border flex-1 min-w-[200px] max-w-md"
              style={{ borderColor: "hsl(var(--admin-border))" }}
            >
              <Search className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
              <input
                value={searchQ}
                onChange={(e) => { setSearchQ(e.target.value); setPage(1); }}
                placeholder="Search key or value..."
                className="bg-transparent text-sm outline-none flex-1"
              />
            </div>
            <div className="relative">
              <select
                value={filterCat}
                onChange={(e) => { setFilterCat(e.target.value); setPage(1); }}
                className="appearance-none rounded-xl px-4 py-2 pr-9 text-sm font-semibold border bg-white"
                style={{ borderColor: "hsl(var(--admin-border))" }}
              >
                <option value="">All categories</option>
                {categories.map((c) => (
                  <option key={c} value={c}>{c}</option>
                ))}
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none" style={{ color: "hsl(var(--admin-muted))" }} />
            </div>
            <button onClick={openAdd} className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2 text-sm">
              <Plus className="w-4 h-4" /> Add Key
            </button>
          </div>

          {/* Table */}
          <div className="admin-card overflow-hidden">
            {loading ? (
              <div className="flex items-center justify-center py-16">
                <Loader2 className="w-6 h-6 animate-spin" style={{ color: "hsl(var(--admin-primary))" }} />
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead style={{ background: "hsl(var(--admin-bg))" }}>
                    <tr className="text-left">
                      <th className="px-4 py-3 font-bold">Key</th>
                      <th className="px-4 py-3 font-bold">VI</th>
                      <th className="px-4 py-3 font-bold">EN</th>
                      <th className="px-4 py-3 font-bold">Category</th>
                      <th className="px-4 py-3 font-bold w-32">Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {paged.length === 0 && (
                      <tr>
                        <td colSpan={5} className="px-4 py-12 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
                          No translations found.
                        </td>
                      </tr>
                    )}
                    {paged.map((p) => (
                      <tr key={p.key} className="border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                        <td className="px-4 py-3">
                          <code className="text-xs px-2 py-0.5 rounded" style={{ background: "hsl(var(--admin-bg))" }}>
                            {p.key}
                          </code>
                        </td>
                        <td className="px-4 py-3 max-w-[200px] truncate text-xs">{p.vietnameseValue}</td>
                        <td className="px-4 py-3 max-w-[200px] truncate text-xs">{p.translations["en"] ?? "—"}</td>
                        <td className="px-4 py-3">
                          {p.category && (
                            <span
                              className="text-xs font-semibold px-2 py-0.5 rounded-full"
                              style={{ background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-primary))" }}
                            >
                              {p.category}
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3">
                          <div className="flex gap-1.5">
                            <button
                              onClick={() => openEdit(p)}
                              className="p-1.5 rounded-md hover:bg-muted"
                              title="Edit"
                            >
                              <Pencil className="w-3.5 h-3.5" />
                            </button>
                            <button
                              onClick={() => deletePair(p.key)}
                              className="p-1.5 rounded-md hover:bg-muted"
                              title="Delete"
                              style={{ color: "hsl(var(--admin-danger))" }}
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between px-4 py-3 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
                <span className="text-xs" style={{ color: "hsl(var(--admin-muted))" }}>
                  {pairs.length} keys · Page {page}/{totalPages}
                </span>
                <div className="flex gap-1">
                  {Array.from({ length: Math.min(totalPages, 10) }, (_, i) => i + 1).map((p) => (
                    <button
                      key={p}
                      onClick={() => setPage(p)}
                      className="px-3 py-1 rounded-lg text-xs font-bold"
                      style={
                        p === page
                          ? { background: "hsl(var(--admin-primary))", color: "#fff" }
                          : { color: "hsl(var(--admin-muted))" }
                      }
                    >
                      {p}
                    </button>
                  ))}
                  {totalPages > 10 && (
                    <span className="px-2 py-1 text-xs" style={{ color: "hsl(var(--admin-muted))" }}>…</span>
                  )}
                </div>
              </div>
            )}
          </div>
        </>
      )}

      {/* ════════ TAB 2: Entity (Content) Translations ════════ */}
      {tab === "entity" && (
        <>
          {/* Entity type selector */}
          <div className="flex flex-wrap items-center gap-3 mb-5">
            <label className="text-sm font-bold">Content type:</label>
            <div className="relative">
              <select
                value={selectedType}
                onChange={(e) => setSelectedType(e.target.value)}
                className="appearance-none rounded-xl px-4 py-2 pr-9 text-sm font-semibold border bg-white"
                style={{ borderColor: "hsl(var(--admin-border))" }}
              >
                {entityTypes.map((et) => (
                  <option key={et.type} value={et.type}>{et.type}</option>
                ))}
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 pointer-events-none" style={{ color: "hsl(var(--admin-muted))" }} />
            </div>
          </div>

          {entityLoading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="w-6 h-6 animate-spin" style={{ color: "hsl(var(--admin-primary))" }} />
            </div>
          ) : entityItems.length === 0 ? (
            <div className="admin-card p-8 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
              No items found for this content type.
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
              {entityItems.map((item) => (
                <div key={item.id} className="admin-card p-5 flex flex-col gap-3">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <h3 className="font-bold text-sm truncate">{item.title}</h3>
                      {item.description && (
                        <p className="text-xs mt-0.5 truncate" style={{ color: "hsl(var(--admin-muted))" }}>
                          {item.description}
                        </p>
                      )}
                    </div>
                    <span className="text-xs shrink-0 font-mono" style={{ color: "hsl(var(--admin-muted))" }}>
                      #{item.id}
                    </span>
                  </div>
                  <div className="flex items-center justify-between">
                    {item.hasTranslation ? (
                      <span
                        className="inline-flex items-center gap-1 text-xs font-semibold px-2 py-0.5 rounded-full"
                        style={{ background: "hsl(142 71% 45% / 0.1)", color: "hsl(142 71% 35%)" }}
                      >
                        <Check className="w-3 h-3" /> Translated ({item.translationCount}/{item.expectedFields})
                      </span>
                    ) : (
                      <span
                        className="inline-flex items-center gap-1 text-xs font-semibold px-2 py-0.5 rounded-full"
                        style={{ background: "hsl(38 92% 50% / 0.1)", color: "hsl(38 92% 40%)" }}
                      >
                        <X className="w-3 h-3" /> Not translated
                      </span>
                    )}
                    <button
                      onClick={() => openEntityTranslate(item)}
                      className="admin-btn-primary px-3 py-1.5 text-xs inline-flex items-center gap-1.5"
                    >
                      <Pencil className="w-3 h-3" /> Translate
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {/* ════════ Static Translation Modal ════════ */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" onClick={() => setModalOpen(false)}>
          <div className="bg-white rounded-2xl w-full max-w-2xl p-6 max-h-[90vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <h3 className="font-display text-xl font-extrabold mb-5">
              {modalMode === "add" ? "Add Translation Key" : "Edit Translation Key"}
            </h3>

            <div className="grid grid-cols-2 gap-4 mb-4">
              <div>
                <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>Key *</label>
                <input
                  value={draft.key}
                  onChange={(e) => setDraft({ ...draft, key: e.target.value })}
                  placeholder="e.g. home.hero.title"
                  className="w-full rounded-lg px-3 py-2 text-sm border outline-none font-mono"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                  disabled={modalMode === "edit"}
                />
              </div>
              <div>
                <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>Category</label>
                <input
                  value={draft.category}
                  onChange={(e) => setDraft({ ...draft, category: e.target.value })}
                  list="cat-suggestions"
                  placeholder="e.g. home"
                  className="w-full rounded-lg px-3 py-2 text-sm border outline-none"
                  style={{ borderColor: "hsl(var(--admin-border))" }}
                />
                <datalist id="cat-suggestions">
                  {categories.map((c) => (
                    <option key={c} value={c} />
                  ))}
                </datalist>
              </div>
            </div>

            {/* Language fields */}
            <div className="space-y-3">
              {[
                { lang: "vi", label: "🇻🇳 Tiếng Việt *", key: "vi" as const },
                { lang: "en", label: "🇺🇸 English", key: "en" as const },
                { lang: "zh", label: "🇨🇳 中文", key: "zh" as const },
                { lang: "ja", label: "🇯🇵 日本語", key: "ja" as const },
              ].map((l) => (
                <div key={l.lang}>
                  <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>
                    {l.label}
                  </label>
                  <textarea
                    value={draft[l.key]}
                    onChange={(e) => setDraft({ ...draft, [l.key]: e.target.value })}
                    rows={2}
                    className="w-full rounded-lg px-3 py-2 text-sm border outline-none resize-none"
                    style={{ borderColor: "hsl(var(--admin-border))" }}
                  />
                </div>
              ))}
            </div>

            <div className="flex justify-end gap-2 mt-5">
              <button onClick={() => setModalOpen(false)} className="px-4 py-2 rounded-lg text-sm font-bold bg-muted">
                {t("common.cancel")}
              </button>
              <button onClick={savePair} className="admin-btn-primary px-4 py-2 text-sm">
                {t("proc.save")}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ════════ Entity Translation Modal ════════ */}
      {entityModalOpen && entityModalItem && entityModalType && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4" onClick={() => setEntityModalOpen(false)}>
          <div className="bg-white rounded-2xl w-full max-w-4xl p-6 max-h-[90vh] overflow-y-auto" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center justify-between mb-5">
              <div>
                <h3 className="font-display text-xl font-extrabold">
                  Translate: {entityModalItem.title}
                </h3>
                <p className="text-xs mt-0.5" style={{ color: "hsl(var(--admin-muted))" }}>
                  {entityModalType.type} #{entityModalItem.id}
                </p>
              </div>
              {/* Language selector */}
              <div className="flex gap-1 p-1 rounded-lg" style={{ background: "hsl(var(--admin-bg))" }}>
                {SUPPORTED_LANGS.map((lang) => (
                  <button
                    key={lang}
                    onClick={() => setEntityModalLang(lang)}
                    className="px-3 py-1.5 text-xs font-bold rounded-md transition"
                    style={
                      entityModalLang === lang
                        ? { background: "hsl(var(--admin-primary))", color: "#fff" }
                        : { color: "hsl(var(--admin-muted))" }
                    }
                  >
                    {LANG_LABELS[lang]}
                  </button>
                ))}
              </div>
            </div>

            {/* Side-by-side fields */}
            <div className="space-y-4">
              {entityModalType.fields.map((field) => (
                <div key={field} className="grid grid-cols-2 gap-4">
                  {/* Original (VI) */}
                  <div>
                    <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>
                      🇻🇳 {field} (Original)
                    </label>
                    <textarea
                      value={entityOriginal[field] ?? ""}
                      readOnly
                      rows={3}
                      className="w-full rounded-lg px-3 py-2 text-sm border bg-gray-50 outline-none resize-none"
                      style={{ borderColor: "hsl(var(--admin-border))" }}
                    />
                  </div>
                  {/* Translation */}
                  <div>
                    <label className="text-xs uppercase tracking-wider font-bold mb-1 block" style={{ color: "hsl(var(--admin-muted))" }}>
                      {LANG_LABELS[entityModalLang]} {field}
                    </label>
                    <textarea
                      value={entityTranslations[entityModalLang]?.[field] ?? ""}
                      onChange={(e) => updateEntityField(field, e.target.value)}
                      rows={3}
                      className="w-full rounded-lg px-3 py-2 text-sm border outline-none resize-none"
                      style={{ borderColor: "hsl(var(--admin-border))" }}
                    />
                  </div>
                </div>
              ))}
            </div>

            <div className="flex justify-end gap-2 mt-5">
              <button onClick={() => setEntityModalOpen(false)} className="px-4 py-2 rounded-lg text-sm font-bold bg-muted">
                {t("common.cancel")}
              </button>
              <button
                onClick={saveEntityTranslations}
                disabled={entitySaving}
                className="admin-btn-primary px-4 py-2 text-sm inline-flex items-center gap-2"
              >
                {entitySaving && <Loader2 className="w-4 h-4 animate-spin" />}
                {t("proc.save")}
              </button>
            </div>
          </div>
        </div>
      )}
    </AdminLayout>
  );
};

export default TranslationsPage;
