import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, Search, Calendar } from "lucide-react";
import Layout from "@/components/layout/Layout";
import PageHeader from "@/components/PageHeader";
import { news, newsCategoryI18n } from "@/data/news";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { localizeCategory, pickLocalized } from "@/lib/localize";

const News = () => {
  const { t, lang } = useI18n();
  const ALL = t("common.all");
  const [q, setQ] = useState("");
  const [cat, setCat] = useState(ALL);

  // Source categories remain in Vietnamese (keys); displayed labels are translated.
  const sourceCategories = useMemo(() => Array.from(new Set(news.map((n) => n.category))), []);
  const categories = useMemo(() => [ALL, ...sourceCategories], [ALL, sourceCategories]);

  // Reset filter when language toggle changes the ALL label.
  const isAll = cat === ALL || !sourceCategories.includes(cat);

  const filtered = useMemo(() => {
    const ql = q.toLowerCase();
    return news.filter((n) => {
      const matchCat = isAll || n.category === cat;
      const localizedTitle = pickLocalized(n, "title", lang) as string;
      const matchQ = !q || localizedTitle.toLowerCase().includes(ql) || n.title.toLowerCase().includes(ql);
      return matchCat && matchQ;
    });
  }, [cat, q, isAll, lang]);

  const [featured, ...rest] = filtered;

  const renderCategory = (c: string) =>
    c === ALL ? ALL : localizeCategory(c, lang, newsCategoryI18n);

  return (
    <Layout>
      <PageHeader
        eyebrow={t("newsPage.eyebrow")}
        title={t("newsPage.title")}
        description={t("newsPage.desc")}
      />

      <section className="py-10 bg-background border-b border-border">
        <div className="container-custom flex flex-col lg:flex-row lg:items-center gap-5 justify-between">
          <div className="flex flex-wrap gap-2">
            {categories.map((c) => (
              <button
                key={c}
                onClick={() => setCat(c)}
                className={cn(
                  "btn-pill px-5 py-2.5 text-xs uppercase tracking-wider border transition-all",
                  (isAll && c === ALL) || cat === c
                    ? "btn-gradient text-white border-transparent"
                    : "bg-secondary border-border text-foreground/70 hover:text-foreground"
                )}
              >
                {renderCategory(c)}
              </button>
            ))}
          </div>
          <div className="flex items-center gap-2 bg-secondary rounded-full px-5 py-2.5 w-full lg:w-80 border border-border">
            <Search className="w-4 h-4 text-muted-foreground shrink-0" />
            <input
              value={q}
              onChange={(e) => setQ(e.target.value)}
              placeholder={t("newsPage.searchPh")}
              className="bg-transparent text-sm focus:outline-none flex-1"
            />
          </div>
        </div>
      </section>

      <section className="py-16 lg:py-20 bg-background">
        <div className="container-custom">
          {filtered.length === 0 ? (
            <p className="text-center py-20 text-muted-foreground">{t("newsPage.empty")}</p>
          ) : (
            <>
              {featured && (
                <Link
                  to={`/news/${featured.id}`}
                  className="group grid grid-cols-1 lg:grid-cols-2 gap-8 mb-16 card-hover bg-card border border-border rounded-3xl overflow-hidden"
                >
                  <div className="image-zoom aspect-[4/3] lg:aspect-auto bg-muted">
                    <img src={featured.img} alt="" loading="lazy" className="w-full h-full object-cover" />
                  </div>
                  <div className="p-8 lg:p-12 flex flex-col justify-center">
                    <div className="flex items-center gap-3 mb-5">
                      <span className="chip chip-primary">{renderCategory(featured.category)}</span>
                      <span className="text-xs uppercase tracking-wider text-muted-foreground font-bold flex items-center gap-1.5">
                        <Calendar className="w-3 h-3" /> {featured.date}
                      </span>
                    </div>
                    <h2 className="font-display text-3xl lg:text-4xl font-extrabold leading-tight tracking-tight mb-5 group-hover:text-primary transition-colors text-balance">
                      {pickLocalized(featured, "title", lang)}
                    </h2>
                    <p className="text-muted-foreground leading-relaxed mb-6">
                      {pickLocalized(featured, "excerpt", lang)}
                    </p>
                    <span className="inline-flex items-center gap-2 text-sm font-bold text-primary">
                      {t("common.readMore")} <ArrowUpRight className="w-4 h-4" />
                    </span>
                  </div>
                </Link>
              )}

              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 lg:gap-8">
                {rest.map((n) => (
                  <Link
                    key={n.id}
                    to={`/news/${n.id}`}
                    className="group card-hover bg-card rounded-3xl overflow-hidden border border-border"
                  >
                    <div className="image-zoom aspect-[4/3] bg-muted relative">
                      <img src={n.img} alt="" loading="lazy" className="w-full h-full object-cover" />
                      <span className="absolute top-5 left-5 chip chip-primary bg-white/95">
                        {renderCategory(n.category)}
                      </span>
                    </div>
                    <div className="p-6">
                      <p className="text-xs uppercase tracking-wider text-muted-foreground font-bold mb-2">{n.date}</p>
                      <h3 className="font-display text-lg font-extrabold leading-tight mb-2 group-hover:text-primary transition-colors line-clamp-2">
                        {pickLocalized(n, "title", lang)}
                      </h3>
                      <p className="text-sm text-muted-foreground line-clamp-2">
                        {pickLocalized(n, "excerpt", lang)}
                      </p>
                    </div>
                  </Link>
                ))}
              </div>
            </>
          )}
        </div>
      </section>
    </Layout>
  );
};

export default News;
