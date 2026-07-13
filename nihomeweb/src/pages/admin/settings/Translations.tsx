import { useCallback, useEffect, useState } from "react";
import {
  Search,
  Plus,
  Pencil,
  Trash2,
  Languages as LanguagesIcon,
  FileText,
  Check,
  X,
  Loader2,
} from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import api from "@/lib/api";
import AdminExportButton from "@/components/admin/AdminExportButton";
import { createCsvFilename, downloadCsv } from "@/lib/exportCsv";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { cn } from "@/lib/utils";

/* ─── Helpers ───────────────────────────────────── */
const JSON_FIELDS = ["Content", "Sections", "Challenges", "Solutions"];

/** Try to parse a JSON array of strings into plain text (paragraphs separated by blank lines). */
function jsonToPlainText(raw: string): string {
  if (!raw) return "";
  try {
    const parsed = JSON.parse(raw);
    if (Array.isArray(parsed)) {
      if (parsed.every((v: unknown) => typeof v === "string")) return parsed.join("\n\n");
      if (parsed.every((v: unknown) => typeof v === "object" && v !== null)) {
        return parsed.map((s: Record<string, string>) => {
          if (s.heading && s.body) return `## ${s.heading}\n${s.body}`;
          return JSON.stringify(s);
        }).join("\n\n");
      }
    }
  } catch { /* not JSON, return as-is */ }
  return raw;
}

/** Convert plain text back to JSON array string for storage. */
function plainTextToJson(text: string, field: string): string {
  if (!text.trim()) return "";
  if (field === "Sections") {
    const sections = text.split(/\n\n+/).filter(Boolean).map((block) => {
      const m = block.match(/^##\s+(.+)\n([\s\S]*)$/);
      if (m) return { heading: m[1].trim(), body: m[2].trim() };
      return { heading: "", body: block.trim() };
    });
    return JSON.stringify(sections);
  }
  const parts = text.split(/\n\n+/).filter(Boolean);
  return JSON.stringify(parts);
}

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
const LANG_LABELS: Record<string, string> = { vi: "🇻🇳 VI", en: "🇺🇸 English", zh: "🇨🇳 中文", ja: "🇯🇵 日本語" };
const ALL_LANGS = ["vi", "en", "zh", "ja"] as const;
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
  }, [selectedType, tab]);

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
      const orig: Record<string, string> = data.original ?? {};
      for (const f of JSON_FIELDS) {
        if (orig[f]) orig[f] = jsonToPlainText(orig[f]);
      }
      setEntityOriginal(orig);
      const trans: Record<string, Record<string, string>> = data.translations ?? {};
      for (const lang of Object.keys(trans)) {
        for (const f of JSON_FIELDS) {
          if (trans[lang][f]) trans[lang][f] = jsonToPlainText(trans[lang][f]);
        }
      }
      setEntityTranslations(trans);
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
      const raw = entityTranslations[entityModalLang] ?? {};
      const fields: Record<string, string> = {};
      for (const [k, v] of Object.entries(raw)) {
        fields[k] = JSON_FIELDS.includes(k) ? plainTextToJson(v, k) : v;
      }
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

  const handleExportStaticTranslations = () => {
    downloadCsv({
      filename: createCsvFilename("admin-translations"),
      columns: [
        { header: "Key", value: "key" },
        { header: "Category", value: (row) => row.category ?? "" },
        { header: "VI", value: "vietnameseValue" },
        { header: "EN", value: (row) => row.translations.en ?? "" },
        { header: "ZH", value: (row) => row.translations.zh ?? "" },
        { header: "JA", value: (row) => row.translations.ja ?? "" },
        { header: "Created at", value: "createdAt" },
      ],
      rows: pairs,
    });
  };

  /* ─── Pagination ─── */
  const totalPages = Math.ceil(pairs.length / PAGE_SIZE);
  const paged = pairs.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  /* ─── Render ─── */
  return (
    <AdminLayout>
      <div className="space-y-4 p-4 sm:p-6">
        <header>
          <h1 className="text-2xl font-semibold">{t("set.languages")} — Translations</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Manage all UI translation keys and content translations
          </p>
        </header>

        <Tabs value={tab} onValueChange={(v) => setTab(v as "static" | "entity")} className="w-fit">
          <TabsList>
            <TabsTrigger value="static" className="gap-1.5">
              <LanguagesIcon className="h-4 w-4" /> UI Translations
            </TabsTrigger>
            <TabsTrigger value="entity" className="gap-1.5">
              <FileText className="h-4 w-4" /> Content Translations
            </TabsTrigger>
          </TabsList>
        </Tabs>

        {/* ════════ TAB 1: Static UI Translations ════════ */}
        {tab === "static" && (
          <div className="space-y-4">
            {/* Toolbar */}
            <div className="flex flex-wrap items-center gap-3">
              <div className="relative min-w-[200px] max-w-md flex-1">
                <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                <Input
                  value={searchQ}
                  onChange={(e) => { setSearchQ(e.target.value); setPage(1); }}
                  placeholder="Search key or value..."
                  className="h-9 pl-9"
                />
              </div>
              <Select
                value={filterCat || "__all__"}
                onValueChange={(v) => { setFilterCat(v === "__all__" ? "" : v); setPage(1); }}
              >
                <SelectTrigger className="h-9 w-[200px]">
                  <SelectValue placeholder="All categories" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">All categories</SelectItem>
                  {categories.map((c) => (
                    <SelectItem key={c} value={c}>{c}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <AdminExportButton onClick={handleExportStaticTranslations} disabled={loading || pairs.length === 0} />
              <Button onClick={openAdd}>
                <Plus className="mr-1.5 h-4 w-4" /> Add Key
              </Button>
            </div>

            {/* Table */}
            <section className="overflow-hidden rounded-lg border bg-card">
              {loading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-primary" />
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full divide-y text-sm">
                    <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                      <tr>
                        <th className="px-4 py-3 text-left font-medium">Key</th>
                        {ALL_LANGS.map((lang) => (
                          <th key={lang} className="px-4 py-3 text-left font-medium">{LANG_LABELS[lang]}</th>
                        ))}
                        <th className="px-4 py-3 text-left font-medium">Category</th>
                        <th className="w-32 px-4 py-3 text-left font-medium">Actions</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y">
                      {paged.length === 0 && (
                        <tr>
                          <td colSpan={7} className="px-4 py-12 text-center text-muted-foreground">
                            No translations found.
                          </td>
                        </tr>
                      )}
                      {paged.map((p) => (
                        <tr key={p.key} className="hover:bg-muted/40 transition">
                          <td className="px-4 py-3">
                            <code className="rounded bg-muted px-2 py-0.5 text-xs">{p.key}</code>
                          </td>
                          <td className="max-w-[160px] truncate px-4 py-3 text-xs">{p.vietnameseValue}</td>
                          <td className="max-w-[160px] truncate px-4 py-3 text-xs">{p.translations["en"] ?? "—"}</td>
                          <td className="max-w-[160px] truncate px-4 py-3 text-xs">{p.translations["zh"] ?? "—"}</td>
                          <td className="max-w-[160px] truncate px-4 py-3 text-xs">{p.translations["ja"] ?? "—"}</td>
                          <td className="px-4 py-3">
                            {p.category && (
                              <Badge variant="outline" className="border-indigo-200 bg-indigo-50 font-medium text-indigo-700">
                                {p.category}
                              </Badge>
                            )}
                          </td>
                          <td className="px-4 py-3">
                            <div className="flex gap-1">
                              <Button
                                size="icon"
                                variant="ghost"
                                className="h-8 w-8"
                                onClick={() => openEdit(p)}
                                aria-label="Edit"
                              >
                                <Pencil className="h-3.5 w-3.5" />
                              </Button>
                              <Button
                                size="icon"
                                variant="ghost"
                                className="h-8 w-8 text-destructive hover:text-destructive"
                                onClick={() => deletePair(p.key)}
                                aria-label="Delete"
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </Button>
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
                <div className="flex items-center justify-between border-t px-4 py-3">
                  <span className="text-xs text-muted-foreground">
                    {pairs.length} keys · Page {page}/{totalPages}
                  </span>
                  <div className="flex gap-1">
                    {Array.from({ length: Math.min(totalPages, 10) }, (_, i) => i + 1).map((p) => (
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
                    {totalPages > 10 && (
                      <span className="px-2 py-1 text-xs text-muted-foreground">…</span>
                    )}
                  </div>
                </div>
              )}
            </section>
          </div>
        )}

        {/* ════════ TAB 2: Entity (Content) Translations ════════ */}
        {tab === "entity" && (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-3">
              <Label className="text-sm">Content type:</Label>
              <Select value={selectedType} onValueChange={setSelectedType}>
                <SelectTrigger className="h-9 w-[220px]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {entityTypes.map((et) => (
                    <SelectItem key={et.type} value={et.type}>{et.type}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {entityLoading ? (
              <div className="flex items-center justify-center py-16">
                <Loader2 className="h-6 w-6 animate-spin text-primary" />
              </div>
            ) : entityItems.length === 0 ? (
              <div className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
                No items found for this content type.
              </div>
            ) : (
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
                {entityItems.map((item) => (
                  <div key={item.id} className="flex flex-col gap-3 rounded-lg border bg-card p-5">
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <h3 className="truncate text-sm font-semibold">{item.title}</h3>
                        {item.description && (
                          <p className="mt-0.5 truncate text-xs text-muted-foreground">
                            {item.description}
                          </p>
                        )}
                      </div>
                      <span className="shrink-0 font-mono text-xs text-muted-foreground">#{item.id}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      {item.hasTranslation ? (
                        <Badge variant="outline" className="gap-1 border-emerald-200 bg-emerald-50 font-medium text-emerald-700">
                          <Check className="h-3 w-3" /> Translated ({item.translationCount}/{item.expectedFields})
                        </Badge>
                      ) : (
                        <Badge variant="outline" className="gap-1 border-amber-200 bg-amber-50 font-medium text-amber-700">
                          <X className="h-3 w-3" /> Not translated
                        </Badge>
                      )}
                      <Button size="sm" onClick={() => openEntityTranslate(item)}>
                        <Pencil className="mr-1.5 h-3 w-3" /> Translate
                      </Button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {/* ════════ Static Translation Modal ════════ */}
      <Dialog open={modalOpen} onOpenChange={setModalOpen}>
        <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              {modalMode === "add" ? "Add Translation Key" : "Edit Translation Key"}
            </DialogTitle>
          </DialogHeader>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <Label htmlFor="tk-key" className="text-xs">Key *</Label>
              <Input
                id="tk-key"
                value={draft.key}
                onChange={(e) => setDraft({ ...draft, key: e.target.value })}
                placeholder="e.g. home.hero.title"
                className="h-9 font-mono"
                disabled={modalMode === "edit"}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="tk-cat" className="text-xs">Category</Label>
              <Input
                id="tk-cat"
                value={draft.category}
                onChange={(e) => setDraft({ ...draft, category: e.target.value })}
                list="cat-suggestions"
                placeholder="e.g. home"
                className="h-9"
              />
              <datalist id="cat-suggestions">
                {categories.map((c) => (
                  <option key={c} value={c} />
                ))}
              </datalist>
            </div>
          </div>

          <div className="space-y-3">
            {[
              { lang: "vi", label: "🇻🇳 Tiếng Việt *", key: "vi" as const },
              { lang: "en", label: "🇺🇸 English", key: "en" as const },
              { lang: "zh", label: "🇨🇳 中文", key: "zh" as const },
              { lang: "ja", label: "🇯🇵 日本語", key: "ja" as const },
            ].map((l) => (
              <div key={l.lang} className="space-y-1.5">
                <Label htmlFor={`tk-${l.lang}`} className="text-xs">{l.label}</Label>
                <Textarea
                  id={`tk-${l.lang}`}
                  value={draft[l.key]}
                  onChange={(e) => setDraft({ ...draft, [l.key]: e.target.value })}
                  rows={2}
                />
              </div>
            ))}
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setModalOpen(false)}>{t("common.cancel")}</Button>
            <Button onClick={savePair}>{t("proc.save")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ════════ Entity Translation Modal ════════ */}
      <Dialog open={entityModalOpen} onOpenChange={setEntityModalOpen}>
        <DialogContent className="max-h-[90vh] max-w-4xl overflow-y-auto">
          {entityModalItem && entityModalType && (
            <>
              <DialogHeader className="space-y-1">
                <DialogTitle>Translate: {entityModalItem.title}</DialogTitle>
                <p className="text-xs text-muted-foreground">
                  {entityModalType.type} #{entityModalItem.id}
                </p>
              </DialogHeader>

              <div className="flex justify-end">
                <div className="inline-flex rounded-md border bg-muted p-1">
                  {SUPPORTED_LANGS.map((lang) => (
                    <button
                      key={lang}
                      type="button"
                      onClick={() => setEntityModalLang(lang)}
                      className={cn(
                        "rounded-sm px-3 py-1.5 text-xs font-medium transition",
                        entityModalLang === lang
                          ? "bg-background text-foreground shadow-sm"
                          : "text-muted-foreground hover:text-foreground",
                      )}
                    >
                      {LANG_LABELS[lang]}
                    </button>
                  ))}
                </div>
              </div>

              <div className="space-y-4">
                {entityModalType.fields.map((field) => {
                  const isLong = ["Content", "Sections", "Challenges", "Solutions", "Description"].includes(field);
                  const rows = isLong ? 8 : 3;
                  return (
                    <div key={field} className="grid grid-cols-2 gap-4">
                      <div className="space-y-1.5">
                        <Label className="text-xs">🇻🇳 {field} (Original)</Label>
                        <Textarea
                          value={entityOriginal[field] ?? ""}
                          readOnly
                          rows={rows}
                          className="resize-y bg-muted/50"
                        />
                      </div>
                      <div className="space-y-1.5">
                        <Label className="text-xs">{LANG_LABELS[entityModalLang]} {field}</Label>
                        <Textarea
                          value={entityTranslations[entityModalLang]?.[field] ?? ""}
                          onChange={(e) => updateEntityField(field, e.target.value)}
                          rows={rows}
                          className="resize-y"
                        />
                      </div>
                    </div>
                  );
                })}
              </div>

              <DialogFooter>
                <Button variant="outline" onClick={() => setEntityModalOpen(false)}>
                  {t("common.cancel")}
                </Button>
                <Button onClick={saveEntityTranslations} disabled={entitySaving}>
                  {entitySaving && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
                  {t("proc.save")}
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

export default TranslationsPage;
