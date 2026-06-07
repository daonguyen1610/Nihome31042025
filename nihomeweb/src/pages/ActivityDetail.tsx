import { Link, useParams } from "react-router-dom";
import { ArrowLeft, ArrowUpRight, Calendar, User } from "lucide-react";
import Layout from "@/components/layout/Layout";
import { useI18n } from "@/lib/i18n";
import { useActivity, useActivities } from "@/hooks/useContentApi";
import { PageLoading, PageError } from "@/components/PageState";

const ContentBlock = ({ value }: { value: string }) => {
  if (!value.trim()) {
    return <div className="h-4" aria-hidden="true" />;
  }

  return <p className="text-foreground/85 leading-relaxed whitespace-pre-line">{value}</p>;
};

const ActivityDetail = () => {
  const { t } = useI18n();
  const { slug } = useParams();
  const { data: a, loading, error, refetch } = useActivity(slug ?? "");
  const { data: allActivities } = useActivities();

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
          <span className="chip chip-orange bg-white/95 mb-5 w-fit">{a.category}</span>
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
          <div className="prose prose-lg max-w-none space-y-6">
            {a.content.map((p, i) => <ContentBlock key={i} value={p} />)}
          </div>
        </div>
      </article>

      {a.gallery && a.gallery.length > 0 && (
        <section className="py-16 bg-surface">
          <div className="container-custom">
            <p className="eyebrow text-primary mb-6">{t("actDetail.gallery")}</p>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
              {a.gallery.map((g, i) => (
                <div key={i} className="image-zoom rounded-3xl overflow-hidden aspect-[4/3] bg-muted">
                  <img src={g} alt={`${a.title} ${i + 1}`} className="w-full h-full object-cover" loading="lazy" />
                </div>
              ))}
            </div>
          </div>
        </section>
      )}

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
