import { Link, useParams } from "react-router-dom";
import { useMemo, useState } from "react";
import { ArrowLeft, ArrowUpRight, Calendar, Grid3X3, List, User } from "lucide-react";
import Layout from "@/components/layout/Layout";
import { useI18n } from "@/lib/i18n";
import { useActivity, useActivities, useActivityCategories } from "@/hooks/useContentApi";
import { resolveCategoryLabel } from "@/lib/category";
import { PageLoading, PageError } from "@/components/PageState";
import ContentBlocks from "@/components/ContentBlocks";
import { Dialog, DialogContent } from "@/components/ui/dialog";

const ActivityDetail = () => {
  const { t, lang } = useI18n();
  const { slug } = useParams();
  const { data: a, loading, error, refetch } = useActivity(slug ?? "");
  const { data: allActivities } = useActivities();
  const { data: categoryList } = useActivityCategories();
  const categoriesById = useMemo(
    () => new Map((categoryList ?? []).map((c) => [c.id, c])),
    [categoryList],
  );
  const [galleryMode, setGalleryMode] = useState<"grid" | "list">("grid");
  const [selectedImage, setSelectedImage] = useState<string | null>(null);

  if (loading) return <Layout><PageLoading /></Layout>;
  if (error) return <Layout><PageError message={error} onRetry={refetch} /></Layout>;

  if (!a) {
    return (
      <Layout>
        <section className="pt-40 pb-20 container-custom text-center">
          <h1 className="font-display text-4xl font-extrabold mb-4">{t("actPage.notFound")}</h1>
          <Link to="/activities" className="btn-pill btn-gradient text-white px-6 py-3 text-xs uppercase tracking-wider">
            <ArrowLeft className="w-4 h-4" /> {t("common.back")}
          </Link>
        </section>
      </Layout>
    );
  }

  const related = (allActivities ?? []).filter((x) => x.slug !== a.slug).slice(0, 3);

  return (
    <Layout>
      <section className="relative h-[60vh] min-h-[420px] overflow-hidden">
        <img src={a.imageUrl} alt={a.title} className="absolute inset-0 w-full h-full object-cover" />
        <div className="absolute inset-0 bg-gradient-to-b from-black/40 to-black/85" />
        <div className="relative z-10 h-full container-custom flex flex-col justify-end pb-14 pt-32">
          <Link to="/activities" className="inline-flex items-center gap-2 text-white/80 hover:text-white text-xs uppercase tracking-[0.22em] font-bold mb-5 w-fit">
            <ArrowLeft className="w-4 h-4" /> {t("actPage.backToList")}
          </Link>
          <span className="chip chip-orange bg-white/95 mb-5 w-fit">{resolveCategoryLabel(a.categoryId, a.category, categoriesById, lang)}</span>
          <h1 className="font-display text-3xl md:text-5xl lg:text-6xl font-extrabold text-white leading-[1.05] tracking-tight max-w-4xl text-balance">
            {a.title}
          </h1>
          <div className="mt-6 flex items-center gap-5 text-white/80 text-sm">
            <span className="flex items-center gap-1.5"><Calendar className="w-4 h-4" /> {a.date}</span>
            {a.author && <span className="flex items-center gap-1.5"><User className="w-4 h-4" /> {a.author}</span>}
          </div>
        </div>
      </section>

      <article className="py-16 lg:py-24 bg-background">
        <div className="container-custom max-w-3xl">
          <p className="text-xl leading-relaxed text-foreground/85 font-medium mb-10 first-letter:font-display first-letter:text-6xl first-letter:font-extrabold first-letter:text-gradient-primary first-letter:mr-2 first-letter:float-left first-letter:leading-none">
            {a.excerpt}
          </p>
          <ContentBlocks items={a.content} className="prose prose-lg max-w-none" paragraphClassName="text-foreground/85 leading-relaxed" />
        </div>
      </article>

      {a.gallery && a.gallery.length > 0 && (
        <section className="py-16 bg-surface">
          <div className="container-custom">
            <div className="flex items-center justify-between gap-4 mb-6">
              <p className="eyebrow text-primary">{t("actDetail.gallery")}</p>
              <div className="inline-flex rounded-full border border-border overflow-hidden bg-card">
                <button type="button" onClick={() => setGalleryMode("grid")} className="p-2 hover:bg-muted" aria-label={t("gallery.viewGrid")}>
                  <Grid3X3 className="w-4 h-4" />
                </button>
                <button type="button" onClick={() => setGalleryMode("list")} className="p-2 hover:bg-muted border-l border-border" aria-label={t("gallery.viewList")}>
                  <List className="w-4 h-4" />
                </button>
              </div>
            </div>
            <div className={galleryMode === "grid" ? "grid grid-cols-1 md:grid-cols-3 gap-5" : "space-y-5 max-w-5xl mx-auto"}>
              {a.gallery.map((g, i) => (
                <button
                  key={`${g}-${i}`}
                  type="button"
                  onClick={() => setSelectedImage(g)}
                  className={galleryMode === "grid" ? "image-zoom rounded-3xl overflow-hidden aspect-[4/3] bg-muted w-full text-left" : "image-zoom rounded-3xl overflow-hidden aspect-video bg-muted w-full text-left"}
                >
                  <img src={g} alt={`${a.title} ${i + 1}`} className="w-full h-full object-cover" loading="lazy" />
                </button>
              ))}
            </div>
          </div>
        </section>
      )}

      <Dialog open={Boolean(selectedImage)} onOpenChange={(open) => !open && setSelectedImage(null)}>
        <DialogContent className="p-1 sm:max-w-6xl bg-transparent border-0 shadow-none">
          {selectedImage ? (
            <img
              src={selectedImage}
              alt={a.title}
              className="w-full max-h-[85vh] object-contain rounded-xl"
            />
          ) : null}
        </DialogContent>
      </Dialog>

      <section className="py-16 bg-surface">
        <div className="container-custom">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6 mb-10">
            <h2 className="font-display text-3xl md:text-4xl font-extrabold tracking-tight">{t("actPage.related")}</h2>
            <Link to="/activities" className="btn-pill bg-card border border-border px-5 py-2.5 text-xs uppercase tracking-wider hover:bg-foreground hover:text-background">
              {t("actPage.allNews")} <ArrowUpRight className="w-4 h-4" />
            </Link>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {related.map((r) => (
              <Link key={r.id} to={`/activities/${r.slug}`} className="group card-hover bg-card rounded-3xl overflow-hidden border border-border">
                <div className="image-zoom aspect-[4/3] bg-muted">
                  <img src={r.imageUrl} alt="" loading="lazy" className="w-full h-full object-cover" />
                </div>
                <div className="p-5">
                  <p className="text-xs uppercase tracking-wider text-muted-foreground font-bold mb-1.5">{r.date}</p>
                  <h3 className="font-display text-lg font-extrabold group-hover:text-primary transition-colors line-clamp-2">{r.title}</h3>
                </div>
              </Link>
            ))}
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default ActivityDetail;
