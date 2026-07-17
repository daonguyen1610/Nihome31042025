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
import { toHostRelativeUrl } from "@/lib/url";
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

// Fields stored as JSON in the DB that need plain-text conversion for the editor.
const JSON_FIELDS = ["Content", "Sections", "Challenges", "Solutions", "Highlights", "IntroBlocks", "ItemsJson", "Requirements"];

/** i18n key for the hint shown below the translation textarea so users know the expected format. */
function fieldHintKey(field: string): string | null {
  if (field === "Sections") return "adminTranslations.hint.sections";
  if (field === "Highlights") return "adminTranslations.hint.highlights";
  if (field === "IntroBlocks") return "adminTranslations.hint.introBlocks";
  if (field === "Content") return "adminTranslations.hint.content";
  return null;
}

/**
 * Convert a backend JSON blob to human-readable plain text for the editor.
 * Must be paired with plainTextToJson when saving.
 */
function jsonToPlainText(raw: string, field: string): string {
  if (!raw) return "";
  try {
    const parsed = JSON.parse(raw);
    if (!Array.isArray(parsed)) return raw;

    // ContentItem[] — mixed strings and ContentBlock objects (activities/news/projects Content field)
    if (field === "Content") {
      // Repair: previous bug stored the whole ContentItem JSON as a single string element.
      // Detect pattern ["[{...},...text...]"] and unwrap it before converting.
      if (parsed.length === 1 && typeof parsed[0] === "string") {
        const inner = (parsed[0] as string).trim();
        if (inner.startsWith("[")) {
          try {
            const repaired = JSON.parse(inner);
            if (Array.isArray(repaired)) return jsonToPlainText(inner, field);
          } catch { /* not JSON, fall through to render as text */ }
        }
      }

      return (parsed as Array<unknown>).map((item) => {
        if (typeof item === "string") return item;
        const b = item as Record<string, unknown>;
        if (b.type === "text" && typeof b.value === "string") return b.value;
        if (b.type === "image" && typeof b.url === "string") {
          const url = toHostRelativeUrl(b.url);
          const caption = typeof b.caption === "string" ? ` | ${b.caption}` : "";
          return `[IMAGE: ${url}${caption}]`;
        }
        if (b.type === "youtube" && typeof b.url === "string") {
          return `[YOUTUBE: ${b.url}]`;
        }
        return "";
      }).filter(Boolean).join("\n\n");
    }

    // string[] — Highlights (one per line), IntroBlocks (--- separator), Content/etc (blank-line-separated)
    if (parsed.every((v: unknown) => typeof v === "string")) {
      if (field === "Highlights") return (parsed as string[]).join("\n");
      if (field === "IntroBlocks") return (parsed as string[]).join("\n---\n");
      return (parsed as string[]).join("\n\n");
    }

    // object[] — Sections: ## Heading\nBullet1\nBullet2
    if (parsed.every((v: unknown) => typeof v === "object" && v !== null)) {
      return (parsed as Record<string, unknown>[]).map((s) => {
        const heading = typeof s.heading === "string" ? s.heading : "";
        const bodyArr = Array.isArray(s.body)
          ? (s.body as unknown[]).map(String)
          : typeof s.body === "string" ? [s.body] : [];
        return `## ${heading}\n${bodyArr.join("\n")}`;
      }).join("\n\n");
    }
  } catch { /* not JSON */ }
  return raw;
}

/** Convert plain text back to JSON for storage. Must mirror jsonToPlainText exactly. */
function plainTextToJson(text: string, field: string): string {
  if (!text.trim()) return "";

  if (field === "Content") {
    // Mixed ContentItem[] — paragraphs + [IMAGE: url] / [IMAGE: url | caption] / [YOUTUBE: url]
    const parts = text.split(/\n\n+/).filter(Boolean);
    const items = parts.map((part) => {
      const t = part.trim();
      const imgMatch = t.match(/^\[IMAGE:\s*(.*?)(?:\s*\|\s*(.*?))?\]$/s);
      if (imgMatch) {
        const url = imgMatch[1].trim();
        const caption = imgMatch[2]?.trim();
        return caption ? { type: "image", url, caption } : { type: "image", url };
      }
      const ytMatch = t.match(/^\[YOUTUBE:\s*(.*?)\]$/s);
      if (ytMatch) return { type: "youtube", url: ytMatch[1].trim() };
      return t; // plain text paragraph stored as string
    }).filter(Boolean);
    return JSON.stringify(items);
  }

  if (field === "Sections") {
    // ## Heading\nBullet1\nBullet2\n\n## Next section...
    const sections = text.split(/\n\n+/).filter(Boolean).map((block) => {
      const lines = block.split("\n");
      const m = lines[0].match(/^##\s+(.*)/);
      const heading = m ? m[1].trim() : "";
      const body = (m ? lines.slice(1) : lines).map((l) => l.trim()).filter(Boolean);
      return { heading, body };
    });
    return JSON.stringify(sections);
  }

  if (field === "Highlights") {
    // One per line → string[]
    const items = text.split("\n").map((l) => l.trim()).filter(Boolean);
    return JSON.stringify(items);
  }

  if (field === "IntroBlocks") {
    // Blocks separated by "---" on its own line → string[]; blank lines within blocks are preserved
    const items = text.split(/\n-{3,}\n/).map((l) => l.trim()).filter(Boolean);
    return JSON.stringify(items);
  }

  // Challenges, Solutions, Requirements — blank-line separated → string[]
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

      // Keep raw original so we can extract media blocks for pre-population
      const rawOrig: Record<string, string> = { ...(data.original ?? {}) };

      // Convert JSON fields to plain text for display
      const orig: Record<string, string> = { ...rawOrig };
      for (const f of JSON_FIELDS) {
        if (orig[f]) orig[f] = jsonToPlainText(orig[f], f);
      }
      setEntityOriginal(orig);

      const trans: Record<string, Record<string, string>> = data.translations ?? {};
      for (const lang of Object.keys(trans)) {
        for (const f of JSON_FIELDS) {
          if (trans[lang][f]) trans[lang][f] = jsonToPlainText(trans[lang][f], f);
        }
      }

      // Pre-populate empty Content translations with media blocks from original.
      // Images and videos are language-neutral — only text paragraphs need translation.
      if (rawOrig["Content"]) {
        try {
          const origItems = JSON.parse(rawOrig["Content"]) as Array<unknown>;
          const mediaText = origItems
            .filter((it) => typeof it === "object" && it !== null &&
              ((it as Record<string, string>).type === "image" || (it as Record<string, string>).type === "youtube"))
            .map((it) => {
              const b = it as Record<string, string>;
              if (b.type === "image") {
                const url = toHostRelativeUrl(b.url);
                const caption = b.caption ? ` | ${b.caption}` : "";
                return `[IMAGE: ${url}${caption}]`;
              }
              return `[YOUTUBE: ${b.url}]`;
            })
            .join("\n\n");

          if (mediaText) {
            for (const lang of SUPPORTED_LANGS) {
              if (!trans[lang]) trans[lang] = {};
              if (!trans[lang]["Content"]) {
                trans[lang]["Content"] = mediaText;
              }
            }
          }
        } catch { /* ignore parse errors */ }
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
          <h1 className="text-2xl font-semibold">{t("nav.translations")}</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            {t("translations.subtitle")}
          </p>
        </header>

        <Tabs value={tab} onValueChange={(v) => setTab(v as "static" | "entity")} className="w-fit">
          <TabsList>
            <TabsTrigger value="static" className="gap-1.5">
              <LanguagesIcon className="h-4 w-4" /> {t("translations.tab.ui")}
            </TabsTrigger>
            <TabsTrigger value="entity" className="gap-1.5">
              <FileText className="h-4 w-4" /> {t("translations.tab.entity")}
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
                  placeholder={t("translations.searchPlaceholder")}
                  className="h-9 pl-9"
                />
              </div>
              <Select
                value={filterCat || "__all__"}
                onValueChange={(v) => { setFilterCat(v === "__all__" ? "" : v); setPage(1); }}
              >
                <SelectTrigger className="h-9 w-[200px]">
                  <SelectValue placeholder={t("translations.allCategories")} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="__all__">{t("translations.allCategories")}</SelectItem>
                  {categories.map((c) => (
                    <SelectItem key={c} value={c}>{c}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <AdminExportButton onClick={handleExportStaticTranslations} disabled={loading || pairs.length === 0} />
              <Button onClick={openAdd}>
                <Plus className="mr-1.5 h-4 w-4" /> {t("translations.addKey")}
              </Button>
            </div>

            {/* Table */}
            <section className="overflow-hidden rounded-lg border bg-card">
              {loading ? (
                <div className="flex items-center justify-center py-16">
                  <Loader2 className="h-6 w-6 animate-spin text-primary" />
                </div>
              ) : paged.length === 0 ? (
                <div className="px-4 py-12 text-center text-muted-foreground">
                  {t("translations.empty")}
                </div>
              ) : (
                <>
                  {/* Mobile / tablet card view (<lg). Table has 7 columns and 4
                      of them are language values, which do not fit on a phone. */}
                  <ul className="grid gap-3 p-3 lg:hidden">
                    {paged.map((p) => (
                      <li key={p.key} className="rounded-lg border bg-card p-3 shadow-sm">
                        <div className="flex items-start justify-between gap-2">
                          <code className="break-all rounded bg-muted px-2 py-0.5 text-xs">{p.key}</code>
                          {p.category && (
                            <Badge
                              variant="outline"
                              className="shrink-0 whitespace-normal border-indigo-200 bg-indigo-50 font-medium text-indigo-700"
                            >
                              {p.category}
                            </Badge>
                          )}
                        </div>
                        <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
                          <dt className="text-muted-foreground">🇻🇳 VI</dt>
                          <dd className="min-w-0 break-words">{p.vietnameseValue || "—"}</dd>
                          <dt className="text-muted-foreground">🇺🇸 EN</dt>
                          <dd className="min-w-0 break-words">{p.translations["en"] ?? "—"}</dd>
                          <dt className="text-muted-foreground">🇨🇳 ZH</dt>
                          <dd className="min-w-0 break-words">{p.translations["zh"] ?? "—"}</dd>
                          <dt className="text-muted-foreground">🇯🇵 JA</dt>
                          <dd className="min-w-0 break-words">{p.translations["ja"] ?? "—"}</dd>
                        </dl>
                        <div className="mt-3 flex items-center justify-end gap-1 border-t pt-2">
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-8 w-8"
                            onClick={() => openEdit(p)}
                            aria-label={t("common.edit")}
                          >
                            <Pencil className="h-3.5 w-3.5" />
                          </Button>
                          <Button
                            size="icon"
                            variant="ghost"
                            className="h-8 w-8 text-destructive hover:text-destructive"
                            onClick={() => deletePair(p.key)}
                            aria-label={t("common.delete")}
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                      </li>
                    ))}
                  </ul>

                  {/* Desktop table (lg+) */}
                  <div className="hidden overflow-x-auto lg:block">
                    <table className="w-full min-w-[900px] divide-y text-sm">
                      <thead className="bg-muted/50 text-xs uppercase tracking-wide text-muted-foreground">
                        <tr>
                          <th className="px-4 py-3 text-left font-medium">{t("translations.field.key")}</th>
                          {ALL_LANGS.map((lang) => (
                            <th key={lang} className="px-4 py-3 text-left font-medium">{LANG_LABELS[lang]}</th>
                          ))}
                          <th className="px-4 py-3 text-left font-medium">{t("translations.field.category")}</th>
                          <th className="w-32 px-4 py-3 text-left font-medium">{t("common.actions")}</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y">
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
                                  aria-label={t("common.edit")}
                                >
                                  <Pencil className="h-3.5 w-3.5" />
                                </Button>
                                <Button
                                  size="icon"
                                  variant="ghost"
                                  className="h-8 w-8 text-destructive hover:text-destructive"
                                  onClick={() => deletePair(p.key)}
                                  aria-label={t("common.delete")}
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
                </>
              )}

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="flex items-center justify-between border-t px-4 py-3">
                  <span className="text-xs text-muted-foreground">
                    {t("translations.pageInfo")
                      .replace("{count}", String(pairs.length))
                      .replace("{page}", String(page))
                      .replace("{total}", String(totalPages))}
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
              <Label className="text-sm">{t("translations.contentType")}:</Label>
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
                {t("translations.entityEmpty")}
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
                          <Check className="h-3 w-3" /> {t("translations.translated")} ({item.translationCount}/{item.expectedFields})
                        </Badge>
                      ) : (
                        <Badge variant="outline" className="gap-1 border-amber-200 bg-amber-50 font-medium text-amber-700">
                          <X className="h-3 w-3" /> {t("translations.notTranslated")}
                        </Badge>
                      )}
                      <Button size="sm" onClick={() => openEntityTranslate(item)}>
                        <Pencil className="mr-1.5 h-3 w-3" /> {t("translations.translate")}
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
        <DialogContent className="w-[95vw] max-w-2xl max-h-[90vh] overflow-y-auto sm:w-full">
          <DialogHeader>
            <DialogTitle>
              {modalMode === "add" ? t("translations.addModalTitle") : t("translations.editModalTitle")}
            </DialogTitle>
          </DialogHeader>

          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="tk-key" className="text-xs">{t("translations.field.key")} *</Label>
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
              <Label htmlFor="tk-cat" className="text-xs">{t("translations.field.category")}</Label>
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

          <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
            <Button variant="outline" onClick={() => setModalOpen(false)}>{t("common.cancel")}</Button>
            <Button onClick={savePair}>{t("common.save")}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* ════════ Entity Translation Modal ════════ */}
      <Dialog open={entityModalOpen} onOpenChange={setEntityModalOpen}>
        <DialogContent className="w-[95vw] max-w-4xl max-h-[90vh] overflow-y-auto sm:w-full">
          {entityModalItem && entityModalType && (
            <>
              <DialogHeader className="space-y-1">
                <DialogTitle className="break-words">
                  {t("translations.entityModalTitle").replace("{name}", entityModalItem.title)}
                </DialogTitle>
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
                  const rows =
                    ["Content", "Sections", "Challenges", "Solutions", "Description", "IntroBlocks"].includes(field) ? 8
                    : ["Highlights", "Requirements"].includes(field) ? 5
                    : 3;
                  const hintKey = fieldHintKey(field);
                  return (
                    <div key={field} className="grid grid-cols-2 gap-4">
                      <div className="space-y-1.5">
                        <Label className="text-xs">🇻🇳 {field} (Original)</Label>
                        <Textarea
                          value={entityOriginal[field] ?? ""}
                          readOnly
                          rows={rows}
                          className="resize-y bg-muted/50 font-mono"
                        />
                      </div>
                      <div className="space-y-1.5">
                        <Label className="text-xs">{LANG_LABELS[entityModalLang]} {field}</Label>
                        <Textarea
                          value={entityTranslations[entityModalLang]?.[field] ?? ""}
                          onChange={(e) => updateEntityField(field, e.target.value)}
                          rows={rows}
                          className="resize-y font-mono"
                        />
                        {hintKey && (
                          <p className="text-[11px] leading-relaxed text-muted-foreground">
                            {t(hintKey)}
                          </p>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>

              <DialogFooter className="flex-col-reverse gap-2 sm:flex-row">
                <Button variant="outline" onClick={() => setEntityModalOpen(false)}>
                  {t("common.cancel")}
                </Button>
                <Button onClick={saveEntityTranslations} disabled={entitySaving}>
                  {entitySaving && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
                  {t("common.save")}
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
