import { useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, Search, Calendar } from "lucide-react";
import Layout from "@/components/layout/Layout";
import PageHeader from "@/components/PageHeader";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";
import { useActivities, useActivityCategories } from "@/hooks/useContentApi";
import { resolveCategoryLabel } from "@/lib/category";
import { PageLoading, PageError, PageEmpty } from "@/components/PageState";

const Activities = () => {
  const { t, lang } = useI18n();
  const { data: activities, loading, error, refetch } = useActivities();
  const { data: categoryList } = useActivityCategories();
  const ALL = t("common.all");
  const [q, setQ] = useState("");
  const [catId, setCatId] = useState<number | "all">("all");

  const categoriesById = useMemo(
    () => new Map((categoryList ?? []).map((c) => [c.id, c])),
    [categoryList],
  );

  const sourceCategoryIds = useMemo(
    () =>
      Array.from(
        new Set(
          (activities ?? [])
            .map((a) => a.categoryId)
            .filter((id): id is number => id != null),
        ),
      ),
    [activities],
  );
  const isAll = catId === "all" || !sourceCategoryIds.includes(catId);

  const filtered = useMemo(() => {
    const ql = q.toLowerCase();
    return (activities ?? []).filter((a) => {
      const matchCat = isAll || a.categoryId === catId;
      const matchQ = !q || a.title.toLowerCase().includes(ql);
      return matchCat && matchQ;
    });
  }, [activities, catId, q, isAll]);

  const [featured, ...rest] = filtered;

  return (
    <Layout>
      <PageHeader
        eyebrow={t("actPage.eyebrow")}
        title={t("actPage.title")}
        description={t("actPage.desc")}
      />

      {/* Filter */}
      <section className="py-10 bg-background border-b border-border">
        <div className="container-custom flex flex-col lg:flex-row lg:items-center gap-5 justify-between">
          <div className="flex flex-wrap gap-2">
            <button
              onClick={() => setCatId("all")}
              className={cn(
                "btn-pill px-5 py-2.5 text-xs uppercase tracking-wider border transition-all",
                isAll
                  ? "btn-gradient text-white border-transparent"
                  : "bg-secondary border-border text-foreground/70 hover:text-foreground"
              )}
            >
              {ALL}
            </button>
            {sourceCategoryIds.map((id) => (
              <button
                key={id}
                onClick={() => setCatId(id)}
                className={cn(
                  "btn-pill px-5 py-2.5 text-xs uppercase tracking-wider border transition-all",
                  !isAll && catId === id
                    ? "btn-gradient text-white border-transparent"
                    : "bg-secondary border-border text-foreground/70 hover:text-foreground"
                )}
              >
                {resolveCategoryLabel(id, undefined, categoriesById, lang)}
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

      {/* Content */}
      <section className="py-16 lg:py-20 bg-background">
        <div className="container-custom">
          {loading ? (
            <PageLoading />
          ) : error ? (
            <PageError message={error} onRetry={refetch} />
          ) : filtered.length === 0 ? (
            <PageEmpty message={t("actPage.empty")} />
          ) : (
            <>
              {featured && (
                <Link
                  to={`/activities/${featured.slug}`}
                  className="group grid grid-cols-1 lg:grid-cols-2 gap-8 mb-16 card-hover bg-card border border-border rounded-3xl overflow-hidden"
                >
                  <div className="image-zoom aspect-[4/3] lg:aspect-auto bg-muted">
                    <img src={featured.imageUrl} alt="" loading="lazy" className="w-full h-full object-cover" />
                  </div>
                  <div className="p-8 lg:p-12 flex flex-col justify-center">
                    <div className="flex items-center gap-3 mb-5">
                      <span className="chip chip-orange">{resolveCategoryLabel(featured.categoryId, featured.category, categoriesById, lang)}</span>
                      <span className="text-xs uppercase tracking-wider text-muted-foreground font-bold flex items-center gap-1.5">
                        <Calendar className="w-3 h-3" /> {featured.date}
                      </span>
                    </div>
                    <h2 className="font-display text-3xl lg:text-4xl font-extrabold leading-tight tracking-tight mb-5 group-hover:text-primary transition-colors text-balance">
                      {featured.title}
                    </h2>
                    <p className="text-muted-foreground leading-relaxed mb-6">
                      {featured.excerpt}
                    </p>
                    <span className="inline-flex items-center gap-2 text-sm font-bold text-primary">
                      {t("common.readMore")} <ArrowUpRight className="w-4 h-4" />
                    </span>
                  </div>
                </Link>
              )}

              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 lg:gap-8">
                {rest.map((a) => (
                  <Link
                    key={a.id}
                    to={`/activities/${a.slug}`}
                    className="group card-hover bg-card rounded-3xl overflow-hidden border border-border"
                  >
                    <div className="image-zoom aspect-[4/3] bg-muted relative">
                      <img src={a.imageUrl} alt="" loading="lazy" className="w-full h-full object-cover" />
                      <span className="absolute top-5 left-5 chip chip-orange bg-white/95">
                        {resolveCategoryLabel(a.categoryId, a.category, categoriesById, lang)}
                      </span>
                    </div>
                    <div className="p-6">
                      <p className="text-xs uppercase tracking-wider text-muted-foreground font-bold mb-2">{a.date}</p>
                      <h3 className="font-display text-lg font-extrabold leading-tight mb-2 group-hover:text-primary transition-colors line-clamp-2">
                        {a.title}
                      </h3>
                      <p className="text-sm text-muted-foreground line-clamp-2">
                        {a.excerpt}
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

export default Activities;
