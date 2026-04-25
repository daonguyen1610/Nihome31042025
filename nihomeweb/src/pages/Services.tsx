import { Link } from "react-router-dom";
import { ArrowUpRight, HardHat, Wrench, Building2, Zap } from "lucide-react";
import Layout from "@/components/layout/Layout";
import PageHeader from "@/components/PageHeader";
import { useI18n } from "@/lib/i18n";
import { useServices } from "@/hooks/useContentApi";
import { PageLoading, PageError, PageEmpty } from "@/components/PageState";

const icons = [Building2, HardHat, Wrench, Zap];

const Services = () => {
  const { t } = useI18n();
  const { data: services, loading, error, refetch } = useServices();
  return (
    <Layout>
      <PageHeader
        eyebrow={t("services.eyebrow")}
        title={t("services.title")}
        description={t("services.desc")}
      />

      <section className="py-20 lg:py-28 bg-background">
        <div className="container-custom">
          {loading ? (
            <PageLoading />
          ) : error ? (
            <PageError message={error} onRetry={refetch} />
          ) : !services || services.length === 0 ? (
            <PageEmpty message={t("common.noData")} />
          ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 lg:gap-8">
            {services.map((s, i) => {
              const Icon = icons[i % icons.length];
              return (
                <Link
                  key={s.slug}
                  to={`/services/${s.slug}`}
                  className="group bg-card rounded-3xl border border-border p-8 lg:p-10 card-hover relative overflow-hidden"
                >
                  <div className="absolute -top-12 -right-12 w-40 h-40 rounded-full bg-gradient-soft pointer-events-none opacity-60 group-hover:scale-110 transition-transform" />
                  <div className="relative">
                    <div className="w-14 h-14 rounded-2xl bg-gradient-primary text-white flex items-center justify-center mb-6 shadow-glow">
                      <Icon className="w-7 h-7" strokeWidth={1.75} />
                    </div>
                    <p className="eyebrow text-primary mb-4">{s.shortTitle}</p>
                    <h3 className="font-display text-2xl lg:text-3xl font-extrabold leading-tight mb-3 group-hover:text-primary transition-colors">
                      {s.title}
                    </h3>
                    <p className="text-muted-foreground leading-relaxed mb-6">{s.tagline}</p>
                    <div className="flex flex-wrap gap-2 mb-6">
                      {s.highlights.slice(0, 3).map((h) => (
                        <span key={h} className="chip chip-orange">{h}</span>
                      ))}
                    </div>
                    <span className="inline-flex items-center gap-2 text-sm font-bold text-primary">
                      {t("common.viewDetail")} <ArrowUpRight className="w-4 h-4" />
                    </span>
                  </div>
                </Link>
              );
            })}
          </div>
          )}
        </div>
      </section>

      {/* CTA */}
      <section className="py-20 bg-surface-dark text-surface-dark-foreground relative overflow-hidden">
        <div className="absolute -top-32 -right-32 w-96 h-96 rounded-full bg-primary/30 blur-3xl" />
        <div className="container-custom relative grid grid-cols-1 md:grid-cols-2 gap-10 items-center">
          <div>
            <p className="eyebrow text-primary-glow mb-6">{t("services.cta.eyebrow")}</p>
            <h2 className="font-display text-3xl md:text-4xl lg:text-5xl font-extrabold leading-tight tracking-tight">
              {t("services.cta.titleA")} <span className="text-gradient-primary">{t("services.cta.titleB")}</span>.
            </h2>
          </div>
          <div className="md:text-right">
            <Link to="/contact" className="btn-pill btn-gradient text-white px-8 py-4 text-sm uppercase tracking-wider">
              {t("common.contactNow")} <ArrowUpRight className="w-4 h-4" />
            </Link>
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default Services;
