import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, Globe2, Info, Loader2, Save, Search, Sparkles } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { translationApi, type TranslationPair } from "@/services/contentApi";
import { useToast } from "@/hooks/use-toast";

type Lang = "vi" | "en" | "zh" | "ja";

type RowValues = Record<Lang, string>;

const LANGS: Array<{ code: Lang; label: string }> = [
  { code: "vi", label: "VI" },
  { code: "en", label: "EN" },
  { code: "zh", label: "ZH" },
  { code: "ja", label: "JA" },
];

const normalizePair = (pair: TranslationPair): RowValues => ({
  vi: pair.vietnameseValue ?? "",
  en: pair.translations?.en ?? "",
  zh: pair.translations?.zh ?? "",
  ja: pair.translations?.ja ?? "",
});

const AboutContent = () => {
  const { t } = useI18n();
  const { toast } = useToast();

  const [loading, setLoading] = useState(true);
  const [savingKey, setSavingKey] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [activeLang, setActiveLang] = useState<Lang>("vi");
  const [pairs, setPairs] = useState<TranslationPair[]>([]);
  const [baseValues, setBaseValues] = useState<Record<string, RowValues>>({});
  const [draftValues, setDraftValues] = useState<Record<string, RowValues>>({});

  const loadPairs = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await translationApi.getPairs({ category: "profilePage" });
      const sorted = [...data].sort((a, b) => a.key.localeCompare(b.key));
      const normalized = sorted.reduce<Record<string, RowValues>>((acc, pair) => {
        acc[pair.key] = normalizePair(pair);
        return acc;
      }, {});
      setPairs(sorted);
      setBaseValues(normalized);
      setDraftValues(normalized);
    } catch {
      toast({
        title: t("auth.error"),
        description: t("aboutAdmin.loadError"),
        variant: "destructive",
      });
    } finally {
      setLoading(false);
    }
  }, [t, toast]);

  useEffect(() => {
    void loadPairs();
  }, [loadPairs]);

  const visiblePairs = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return pairs;

    return pairs.filter((pair) => {
      const values = draftValues[pair.key] ?? normalizePair(pair);
      return (
        pair.key.toLowerCase().includes(q) ||
        values.vi.toLowerCase().includes(q) ||
        values.en.toLowerCase().includes(q) ||
        values.zh.toLowerCase().includes(q) ||
        values.ja.toLowerCase().includes(q)
      );
    });
  }, [draftValues, pairs, search]);

  const updateValue = (key: string, lang: Lang, value: string) => {
    setDraftValues((prev) => ({
      ...prev,
      [key]: {
        ...(prev[key] ?? { vi: "", en: "", zh: "", ja: "" }),
        [lang]: value,
      },
    }));
  };

  const isDirty = (key: string) => {
    const base = baseValues[key];
    const draft = draftValues[key];
    if (!base || !draft) return false;
    return base.vi !== draft.vi || base.en !== draft.en || base.zh !== draft.zh || base.ja !== draft.ja;
  };

  const saveOne = async (key: string) => {
    const values = draftValues[key];
    if (!values) return;
    if (!values.vi.trim()) {
      toast({
        title: t("aboutAdmin.missingDataTitle"),
        description: t("aboutAdmin.viRequired"),
        variant: "destructive",
      });
      return;
    }

    setSavingKey(key);
    try {
      const translations: Record<string, string> = {};
      if (values.en.trim()) translations.en = values.en;
      if (values.zh.trim()) translations.zh = values.zh;
      if (values.ja.trim()) translations.ja = values.ja;

      await translationApi.upsertPair({
        key,
        vietnameseValue: values.vi,
        translations,
        category: "profilePage",
      });

      setBaseValues((prev) => ({ ...prev, [key]: { ...values } }));
      toast({ title: t("form.updated") });
    } catch {
      toast({
        title: t("auth.error"),
        description: t("aboutAdmin.saveError"),
        variant: "destructive",
      });
    } finally {
      setSavingKey(null);
    }
  };

  return (
    <AdminLayout>
      <div className="admin-card p-6 lg:p-8 mb-6" style={{ background: "linear-gradient(135deg, hsl(var(--admin-primary-soft)), hsl(var(--admin-surface)))" }}>
        <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.18em] mb-2" style={{ color: "hsl(var(--admin-primary))" }}>
              {t("nav.about")}
            </p>
            <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{t("aboutAdmin.title")}</h1>
            <p className="text-sm mt-2" style={{ color: "hsl(var(--admin-muted))" }}>
              {t("aboutAdmin.description")} <code>profilePage.*</code>.
            </p>
          </div>
          <div className="grid grid-cols-2 gap-3 text-xs">
            <div className="admin-chip inline-flex items-center gap-1.5">
              <Globe2 className="w-3.5 h-3.5" /> {t("aboutAdmin.languages")}
            </div>
            <div className="admin-chip inline-flex items-center gap-1.5">
              <Info className="w-3.5 h-3.5" /> {t("aboutAdmin.category")}
            </div>
          </div>
        </div>
      </div>

      <div className="admin-card p-4 mb-5">
        <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex items-center gap-2 admin-input w-full lg:max-w-md px-3 py-2">
            <Search className="w-4 h-4" style={{ color: "hsl(var(--admin-muted))" }} />
            <input
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t("aboutAdmin.searchPlaceholder")}
              className="bg-transparent outline-none w-full text-sm"
            />
          </div>

          <div className="flex items-center gap-2">
            {LANGS.map((lang) => (
              <button
                key={lang.code}
                onClick={() => setActiveLang(lang.code)}
                className="px-3 py-1.5 rounded-full text-xs font-bold transition"
                style={
                  activeLang === lang.code
                    ? { background: "hsl(var(--admin-primary))", color: "white" }
                    : { background: "hsl(var(--admin-bg))", color: "hsl(var(--admin-muted))" }
                }
              >
                {lang.label}
              </button>
            ))}
          </div>
        </div>
      </div>

      {loading ? (
        <div className="admin-card p-12 flex items-center justify-center">
          <Loader2 className="w-6 h-6 animate-spin" style={{ color: "hsl(var(--admin-primary))" }} />
        </div>
      ) : visiblePairs.length === 0 ? (
        <div className="admin-card p-10 text-center" style={{ color: "hsl(var(--admin-muted))" }}>
          {t("aboutAdmin.empty")}
        </div>
      ) : (
        <div className="space-y-3">
          {visiblePairs.map((pair) => {
            const values = draftValues[pair.key] ?? normalizePair(pair);
            const value = values[activeLang] ?? "";
            const longText = value.length > 120 || value.includes("\n");

            return (
              <div key={pair.key} className="admin-card p-4 lg:p-5">
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between mb-3">
                  <div className="min-w-0">
                    <p className="text-xs font-mono px-2 py-1 rounded-md inline-block" style={{ background: "hsl(var(--admin-bg))" }}>
                      {pair.key}
                    </p>
                    <p className="text-xs mt-2" style={{ color: "hsl(var(--admin-muted))" }}>
                      {activeLang === "vi" ? t("aboutAdmin.viHint") : `${t("aboutAdmin.langPrefix")} ${activeLang.toUpperCase()}`}
                    </p>
                  </div>
                  <button
                    onClick={() => void saveOne(pair.key)}
                    disabled={!isDirty(pair.key) || savingKey === pair.key}
                    className="admin-btn-primary px-4 py-2 text-sm inline-flex items-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    {savingKey === pair.key ? <Loader2 className="w-4 h-4 animate-spin" /> : <Save className="w-4 h-4" />}
                    {t("common.save")}
                  </button>
                </div>

                {longText ? (
                  <textarea
                    value={value}
                    onChange={(e) => updateValue(pair.key, activeLang, e.target.value)}
                    rows={4}
                    className="admin-input w-full"
                  />
                ) : (
                  <input
                    value={value}
                    onChange={(e) => updateValue(pair.key, activeLang, e.target.value)}
                    className="admin-input w-full"
                  />
                )}

                {isDirty(pair.key) && (
                  <div className="mt-2 text-xs inline-flex items-center gap-1.5" style={{ color: "hsl(var(--admin-warning))" }}>
                    <Sparkles className="w-3.5 h-3.5" /> {t("aboutAdmin.unsaved")}
                  </div>
                )}
                {!isDirty(pair.key) && (
                  <div className="mt-2 text-xs inline-flex items-center gap-1.5" style={{ color: "hsl(var(--admin-success))" }}>
                    <CheckCircle2 className="w-3.5 h-3.5" /> {t("aboutAdmin.synced")}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </AdminLayout>
  );
};

export default AboutContent;
