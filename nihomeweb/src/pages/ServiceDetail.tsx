import { Link, useParams } from "react-router-dom";
import { ArrowLeft, ArrowUpRight, CheckCircle2 } from "lucide-react";
import Layout from "@/components/layout/Layout";
import { getServiceBySlug, services } from "@/data/services";
import { useI18n } from "@/lib/i18n";

const ServiceDetail = () => {
  const { t } = useI18n();
  const { slug } = useParams();
  const svc = slug ? getServiceBySlug(slug) : undefined;

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

  const others = services.filter((s) => s.slug !== svc.slug);

  return (
    <Layout>
      {/* Hero */}
      <section className="relative pt-32 lg:pt-40 pb-16 bg-gradient-soft overflow-hidden">
        <div className="absolute inset-0 bg-gradient-mesh opacity-50" />
        <div className="container-custom relative">
          <Link to="/services" className="inline-flex items-center gap-2 text-sm font-bold text-primary mb-6 hover:gap-3 transition-all">
            <ArrowLeft className="w-4 h-4" /> {t("svc.back")}
          </Link>
          <p className="eyebrow text-primary mb-6">{svc.shortTitle}</p>
          <h1 className="font-display text-4xl md:text-5xl lg:text-6xl font-extrabold leading-[1.05] tracking-tight max-w-4xl text-balance mb-6">
            {svc.title}
          </h1>
          <p className="text-xl text-foreground/80 max-w-3xl leading-relaxed">{svc.tagline}</p>
          <div className="flex flex-wrap gap-2 mt-8">
            {svc.highlights.map((h) => (
              <span key={h} className="chip chip-primary bg-white/95">{h}</span>
            ))}
          </div>
        </div>
      </section>

      {/* Intro */}
      <section className="py-16 lg:py-20 bg-background">
        <div className="container-custom max-w-4xl">
          <p className="text-lg lg:text-xl text-foreground/85 leading-relaxed">{svc.intro}</p>
        </div>
      </section>

      {/* Sections */}
      <section className="pb-20 bg-background">
        <div className="container-custom max-w-4xl space-y-12">
          {svc.sections.map((sec, i) => (
            <div key={i} className="bg-card rounded-3xl border border-border p-8 lg:p-12">
              <div className="flex items-center gap-4 mb-6">
                <span className="w-10 h-10 rounded-full bg-gradient-primary text-white font-display font-extrabold flex items-center justify-center shadow-glow shrink-0">
                  {String(i + 1).padStart(2, "0")}
                </span>
                <h2 className="font-display text-2xl lg:text-3xl font-extrabold leading-tight tracking-tight">{sec.heading}</h2>
              </div>
              <ul className="space-y-3">
                {sec.body.map((p, j) => (
                  <li key={j} className="flex gap-3 text-foreground/80 leading-relaxed">
                    <CheckCircle2 className="w-5 h-5 text-primary shrink-0 mt-1" strokeWidth={2} />
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
