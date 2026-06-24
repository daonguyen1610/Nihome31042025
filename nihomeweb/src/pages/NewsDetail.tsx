import { Link, useParams } from "react-router-dom";
import { useState } from "react";
import { ArrowLeft, Calendar, Grid3X3, List, Tag } from "lucide-react";
import Layout from "@/components/layout/Layout";
import { useI18n } from "@/lib/i18n";
import { useNewsItem, useNews } from "@/hooks/useContentApi";
import { PageLoading, PageError } from "@/components/PageState";
import ContentBlocks from "@/components/ContentBlocks";
import { Dialog, DialogContent } from "@/components/ui/dialog";

const NewsDetail = () => {
  const { t } = useI18n();
  const { slug } = useParams();
  const { data: item, loading, error, refetch } = useNewsItem(slug ?? "");
  const { data: allNews } = useNews();
  const [galleryMode, setGalleryMode] = useState<"grid" | "list">("grid");
  const [selectedImage, setSelectedImage] = useState<string | null>(null);

  if (loading) return <Layout><PageLoading /></Layout>;
  if (error) return <Layout><PageError message={error} onRetry={refetch} /></Layout>;

  if (!item) {
    return (
      <Layout>
        <div className="container-custom py-32 text-center">
          <h1 className="font-display text-3xl font-extrabold mb-4">{t("newsPage.notFound")}</h1>
          <Link to="/news" className="text-primary font-bold">← {t("newsPage.backToList")}</Link>
        </div>
      </Layout>
    );
  }

  const related = (allNews ?? []).filter((n) => n.slug !== item.slug).slice(0, 3);

  return (
    <Layout>
      {/* Hero */}
      <section className="relative pt-32 lg:pt-40 pb-12 bg-gradient-soft">
        <div className="container-custom">
          <Link to="/news" className="inline-flex items-center gap-2 text-sm font-bold text-primary mb-6 hover:gap-3 transition-all">
            <ArrowLeft className="w-4 h-4" /> {t("newsPage.backToList")}
          </Link>
          <div className="flex items-center gap-3 mb-5">
            <span className="chip chip-primary">{item.category}</span>
            <span className="text-xs uppercase tracking-wider text-muted-foreground font-bold flex items-center gap-1.5">
              <Calendar className="w-3 h-3" /> {item.date}
            </span>
          </div>
          <h1 className="font-display text-4xl md:text-5xl lg:text-6xl font-extrabold leading-[1.05] tracking-tight max-w-4xl text-balance">
            {item.title}
          </h1>
        </div>
      </section>

      {/* Image */}
      <section className="bg-background pb-12">
        <div className="container-custom">
          <div className="rounded-3xl overflow-hidden aspect-[16/9] bg-muted">
            <img src={item.imageUrl} alt={item.title} className="w-full h-full object-cover" />
          </div>
        </div>
      </section>

      {/* Body */}
      <section className="pb-20 bg-background">
        <div className="container-custom max-w-3xl">
          <ContentBlocks items={item.content} paragraphClassName="text-lg text-foreground/85 leading-relaxed" />
        </div>
      </section>

      {item.gallery && item.gallery.length > 0 && (
        <section className="pb-20 bg-background">
          <div className="container-custom">
            <div className="flex items-center justify-between gap-4 mb-6">
              <p className="eyebrow text-primary">{t("newsDetail.gallery")}</p>
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
              {item.gallery.map((g, i) => (
                <button
                  key={`${g}-${i}`}
                  type="button"
                  onClick={() => setSelectedImage(g)}
                  className={galleryMode === "grid" ? "image-zoom rounded-3xl overflow-hidden aspect-[4/3] bg-muted w-full text-left" : "image-zoom rounded-3xl overflow-hidden aspect-video bg-muted w-full text-left"}
                >
                  <img src={g} alt={`${item.title} ${i + 1}`} className="w-full h-full object-cover" loading="lazy" />
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
              alt={item.title}
              className="w-full max-h-[85vh] object-contain rounded-xl"
            />
          ) : null}
        </DialogContent>
      </Dialog>

      {/* Related */}
      {related.length > 0 && (
        <section className="py-20 bg-surface border-t border-border">
          <div className="container-custom">
            <p className="eyebrow text-primary mb-6">{t("newsPage.related")}</p>
            <h2 className="font-display text-3xl md:text-4xl font-extrabold mb-10 tracking-tight">{t("newsDetail.readMore")}</h2>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {related.map((n) => (
                <Link key={n.id} to={`/news/${n.slug}`} className="group card-hover bg-card rounded-3xl overflow-hidden border border-border">
                  <div className="image-zoom aspect-[4/3] bg-muted">
                    <img src={n.imageUrl} alt="" loading="lazy" className="w-full h-full object-cover" />
                  </div>
                  <div className="p-6">
                    <p className="text-xs text-muted-foreground font-bold uppercase tracking-wider mb-2 inline-flex items-center gap-1.5">
                      <Tag className="w-3 h-3" /> {n.category}
                    </p>
                    <h3 className="font-display text-lg font-extrabold leading-tight group-hover:text-primary transition-colors line-clamp-2">{n.title}</h3>
                  </div>
                </Link>
              ))}
            </div>
          </div>
        </section>
      )}
    </Layout>
  );
};

export default NewsDetail;
