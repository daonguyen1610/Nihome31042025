import { useState } from "react";
import Layout from "@/components/layout/Layout";
import PageHeader from "@/components/PageHeader";
import { Quote, Building2, Globe2, Handshake, ExternalLink } from "lucide-react";
import { clientLogos, partnerLogos, supplierLogos, type LogoItem } from "@/data/clients";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";

const LogoCard = ({ item }: { item: LogoItem }) => {
  const inner = (
    <div className="bg-card rounded-2xl border border-border h-44 md:h-48 flex flex-col items-center justify-center p-6 hover-lift transition-all relative group">
      <div className="flex-1 flex items-center justify-center w-full">
        <img
          src={item.img}
          alt={item.name}
          className="max-h-24 max-w-full object-contain group-hover:scale-105 transition-transform"
          loading="lazy"
        />
      </div>
      <p className="mt-3 text-xs font-bold uppercase tracking-wider text-foreground/75 text-center line-clamp-1">
        {item.name}
      </p>
      {item.href && (
        <ExternalLink className="absolute top-3 right-3 w-3.5 h-3.5 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity" />
      )}
    </div>
  );
  return item.href ? (
    <a href={item.href} target="_blank" rel="noopener noreferrer">
      {inner}
    </a>
  ) : (
    <div>{inner}</div>
  );
};

const Clients = () => {
  const { t } = useI18n();
  const tabs = [
    { id: "clients", label: t("cli.tab.clients"), data: clientLogos },
    { id: "partners", label: t("cli.tab.partners"), data: partnerLogos },
    { id: "suppliers", label: t("cli.tab.suppliers"), data: supplierLogos },
  ] as const;

  const stats = [
    { icon: Building2, num: "80+", label: t("cli.stat.clients") },
    { icon: Globe2, num: "12", label: t("cli.stat.countries") },
    { icon: Handshake, num: "95%", label: t("cli.stat.repeat") },
  ];

  const testimonials = [
    { quote: t("cli.testi.q1"), author: "Mr. Tanaka Hiroshi", role: t("cli.testi.r1") },
    { quote: t("cli.testi.q2"), author: "Bà Nguyễn Thị Hương", role: t("cli.testi.r2") },
    { quote: t("cli.testi.q3"), author: "Mr. Lâm Quốc Hùng", role: t("cli.testi.r3") },
  ];

  const [active, setActive] = useState<(typeof tabs)[number]["id"]>("clients");
  const current = tabs.find((tab) => tab.id === active)!;

  return (
    <Layout>
      <PageHeader
        eyebrow={t("cli.eyebrow")}
        title={t("cli.title")}
        description={t("cli.desc")}
      />

      {/* Stats */}
      <section className="py-12 bg-background">
        <div className="container-custom grid grid-cols-1 md:grid-cols-3 gap-5">
          {stats.map((s, i) => (
            <div key={i} className="bg-gradient-soft border border-border rounded-3xl p-8 text-center hover-lift">
              <s.icon className="w-9 h-9 text-primary mx-auto mb-4" strokeWidth={1.5} />
              <p className="font-display text-5xl font-extrabold text-gradient-primary mb-1">{s.num}</p>
              <p className="text-sm text-muted-foreground font-bold uppercase tracking-wider">{s.label}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Tabbed logo grid */}
      <section className="py-20 bg-surface">
        <div className="container-custom">
          <div className="text-center max-w-2xl mx-auto mb-10">
            <p className="eyebrow text-primary mb-6 justify-center">{t("cli.network.eyebrow")}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
              {t("cli.network.titleA")} <span className="text-gradient-primary">{current.label.toLowerCase()}</span>.
            </h2>
          </div>

          {/* Tab pills */}
          <div className="flex flex-wrap gap-2 justify-center mb-12">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActive(tab.id)}
                className={cn(
                  "btn-pill px-6 py-3 text-xs uppercase tracking-wider border transition-all",
                  active === tab.id
                    ? "btn-gradient text-white border-transparent"
                    : "bg-card border-border text-foreground/70 hover:text-foreground"
                )}
              >
                {tab.label} <span className="opacity-60 ml-1">({tab.data.length})</span>
              </button>
            ))}
          </div>

          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-5">
            {current.data.map((item, i) => (
              <LogoCard key={`${item.name}-${i}`} item={item} />
            ))}
          </div>
        </div>
      </section>

      {/* Testimonials */}
      <section className="py-20 lg:py-28 bg-background">
        <div className="container-custom">
          <div className="text-center max-w-2xl mx-auto mb-14">
            <p className="eyebrow text-primary mb-6 justify-center">{t("cli.testi.eyebrow")}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
              {t("cli.testi.titleA")} <span className="text-gradient-primary">{t("cli.testi.titleB")}</span>.
            </h2>
          </div>
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {testimonials.map((tm, i) => (
              <figure key={i} className="bg-card rounded-3xl border border-border p-8 hover-lift relative">
                <Quote className="w-10 h-10 text-primary/20 absolute top-6 right-6" />
                <blockquote className="text-foreground/85 leading-relaxed mb-7">"{tm.quote}"</blockquote>
                <figcaption className="border-t border-border pt-5">
                  <p className="font-display font-extrabold">{tm.author}</p>
                  <p className="text-sm text-muted-foreground mt-1">{tm.role}</p>
                </figcaption>
              </figure>
            ))}
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default Clients;
