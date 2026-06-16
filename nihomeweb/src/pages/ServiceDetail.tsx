import { Link, useParams } from "react-router-dom";
import { ArrowLeft, ArrowUpRight, CheckCircle2 } from "lucide-react";
import Layout from "@/components/layout/Layout";
import { useI18n } from "@/lib/i18n";
import { useService, useServices } from "@/hooks/useContentApi";
import { PageLoading, PageError } from "@/components/PageState";

function RichText({ text, className }: { text: string; className?: string }) {
  const paras = text.split(/\n\n+/);
  return (
    <div className={className}>
      {paras.map((para, i) => (
        <p key={i} className={`whitespace-pre-wrap leading-relaxed${i < paras.length - 1 ? " mb-4" : ""}`}>
          {para}
        </p>
      ))}
    </div>
  );
}

const ServiceDetail = () => {
  const { t } = useI18n();
  const { slug } = useParams();
  const { data: svc, loading, error, refetch } = useService(slug ?? "");
  const { data: allServices } = useServices();

  if (loading) return <Layout><PageLoading /></Layout>;
  if (error) return <Layout><PageError message={error} onRetry={refetch} /></Layout>;

  if (!svc) {
    return (
      <Layout>
        <div className="container-custom py-32 text-center">
          <h1 className="font-display text-3xl font-extrabold mb-4">{t("svc.notFound")}</h1>
          <Link to="/services" className="text-primary font-bold">← {t("svc.back")}</Link>
        </div>
      </Layout>
    );
  }

  const others = (allServices ?? []).filter((s) => s.slug !== svc.slug);

  return (
    <Layout>
      {/* Hero */}
      <section className="relative pt-28 lg:pt-36 pb-14 lg:pb-16 bg-gradient-soft overflow-hidden">
        <div className="absolute inset-0 bg-gradient-mesh opacity-45" />
        <div className="absolute -top-24 -right-24 w-80 h-80 rounded-full bg-primary/15 blur-3xl" />
        <div className="absolute -bottom-24 -left-24 w-72 h-72 rounded-full bg-secondary/20 blur-3xl" />
        <div className="container-custom relative">
          <Link to="/services" className="inline-flex items-center gap-2 text-sm font-bold text-primary mb-6 hover:gap-3 transition-all">
            <ArrowLeft className="w-4 h-4" /> {t("svc.back")}
          </Link>

          <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,1fr)_360px] gap-8 lg:gap-10 items-start">
            <div>
              <p className="eyebrow text-primary mb-5">{svc.shortTitle}</p>
              <h1 className="font-display text-4xl md:text-5xl lg:text-[54px] font-extrabold leading-[1.06] tracking-tight max-w-4xl text-balance mb-6">
                {svc.title}
              </h1>
              <p className="text-lg md:text-xl text-foreground/80 max-w-3xl leading-relaxed">{svc.tagline}</p>
              <div className="flex flex-wrap gap-2.5 mt-7">
                {svc.highlights.map((h) => (
                  <span key={h} className="chip chip-primary bg-white/90 border border-primary/20">{h}</span>
                ))}
              </div>
            </div>

            <aside className="bg-card/95 rounded-3xl border border-border p-6 shadow-xl">
              <p className="text-xs uppercase tracking-[0.2em] font-bold text-primary mb-4">{svc.shortTitle}</p>
              <p className="font-display text-2xl font-extrabold leading-tight mb-5">{svc.shortTitle}</p>
              <ul className="space-y-3.5">
                {svc.highlights.slice(0, 4).map((h) => (
                  <li key={h} className="flex gap-2.5 text-sm text-foreground/85">
                    <CheckCircle2 className="w-4.5 h-4.5 text-primary shrink-0 mt-0.5" strokeWidth={2.25} />
                    <span>{h}</span>
                  </li>
                ))}
              </ul>
            </aside>
          </div>
        </div>
      </section>

      {/* Intro */}
      <section className="py-14 lg:py-18 bg-background">
        <div className="container-custom max-w-5xl">
          <div className="rounded-3xl border border-border bg-card p-6 lg:p-8">
            <RichText text={svc.intro} className="text-lg lg:text-xl text-foreground/85" />
          </div>

          {/* Intro blocks: alternating 2-col image + text */}
          {(svc.introBlocks ?? []).length > 0 && (
            <div className="mt-10 space-y-12">
              {svc.introBlocks.map((block, i) => {
                const hasImage = !!block.imageUrl;
                const hasText = !!block.text.trim();
                const imageLeft = i % 2 === 0;

                // Image only — full width centered
                if (hasImage && !hasText) {
                  return (
                    <div key={i} className="rounded-2xl overflow-hidden border border-border shadow-lg">
                      <img src={block.imageUrl} alt="" className="w-full h-auto block" loading="lazy" />
                    </div>
                  );
                }

                // Text only — full width
                if (hasText && !hasImage) {
                  return (
                    <RichText key={i} text={block.text} className="text-base lg:text-lg text-foreground/80" />
                  );
                }

                // Both — alternating 2-col layout
                return (
                  <div
                    key={i}
                    className={`flex flex-col gap-6 lg:gap-10 lg:items-center ${imageLeft ? "lg:flex-row" : "lg:flex-row-reverse"}`}
                  >
                    <div className="lg:w-1/2 rounded-2xl overflow-hidden border border-border shadow-lg shrink-0">
                      <img src={block.imageUrl} alt="" className="w-full h-auto block" loading="lazy" />
                    </div>
                    <div className="lg:w-1/2">
                      <RichText text={block.text} className="text-base lg:text-lg text-foreground/80" />
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </section>

      {/* Sections */}
      <section className="pb-20 bg-background">
        <div className="container-custom max-w-5xl space-y-7 lg:space-y-8">
          {svc.sections.map((sec, i) => (
            <div key={i} className="bg-card rounded-3xl border border-border p-6 lg:p-8">
              <div className="flex items-center gap-3.5 mb-5">
                <span className="w-10 h-10 rounded-xl bg-gradient-primary text-white font-display font-extrabold flex items-center justify-center shadow-glow shrink-0">
                  {String(i + 1).padStart(2, "0")}
                </span>
                <h2 className="font-display text-2xl lg:text-[30px] font-extrabold leading-tight tracking-tight">{sec.heading}</h2>
              </div>
              <ul className="space-y-3.5">
                {sec.body.map((p, j) => (
                  <li key={j} className="flex gap-3 text-foreground/80 leading-relaxed">
                    <CheckCircle2 className="w-5 h-5 text-primary shrink-0 mt-1" strokeWidth={2.1} />
                    <span>{p}</span>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </section>

      {/* Other services */}
      <section className="py-20 bg-surface border-t border-border">
        <div className="container-custom">
          <p className="eyebrow text-primary mb-6">{t("svc.others")}</p>
          <h2 className="font-display text-3xl md:text-4xl font-extrabold mb-10 tracking-tight">{t("svc.exploreMore")}</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {others.map((o) => (
              <Link key={o.slug} to={`/services/${o.slug}`} className="group card-hover bg-card rounded-3xl border border-border p-7">
                <p className="eyebrow text-primary mb-4">{o.shortTitle}</p>
                <h3 className="font-display text-xl font-extrabold leading-tight mb-3 group-hover:text-primary transition-colors">{o.title}</h3>
                <p className="text-sm text-muted-foreground leading-relaxed mb-5 line-clamp-2">{o.tagline}</p>
                <span className="inline-flex items-center gap-2 text-xs uppercase tracking-wider font-bold text-primary">
                  {t("common.viewDetail")} <ArrowUpRight className="w-3.5 h-3.5" />
                </span>
              </Link>
            ))}
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default ServiceDetail;
