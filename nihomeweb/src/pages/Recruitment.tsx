import Layout from "@/components/layout/Layout";
import { Link } from "react-router-dom";
import { ArrowUpRight, MapPin, Clock, Briefcase, Sparkles, Heart, GraduationCap, Coffee } from "lucide-react";
import recruitHero from "@/assets/recruit-hero.jpg";
import culture1 from "@/assets/recruit-culture-1.jpg";
import culture2 from "@/assets/recruit-culture-2.jpg";
import culture3 from "@/assets/recruit-culture-3.jpg";
import { useI18n } from "@/lib/i18n";

const Recruitment = () => {
  const { t } = useI18n();

  const positions = [
    { titleKey: "rec.pos.t1", deptKey: "rec.pos.dept.design", locKey: "rec.pos.loc.hcm", typeKey: "rec.pos.type.full", lvlKey: "rec.pos.lvl.5" },
    { titleKey: "rec.pos.t2", deptKey: "rec.pos.dept.design", locKey: "rec.pos.loc.hcm", typeKey: "rec.pos.type.full", lvlKey: "rec.pos.lvl.3" },
    { titleKey: "rec.pos.t3", deptKey: "rec.pos.dept.design", locKey: "rec.pos.loc.hcm", typeKey: "rec.pos.type.full", lvlKey: "rec.pos.lvl.2" },
    { titleKey: "rec.pos.t4", deptKey: "rec.pos.dept.constr", locKey: "rec.pos.loc.bd", typeKey: "rec.pos.type.full", lvlKey: "rec.pos.lvl.5" },
    { titleKey: "rec.pos.t5", deptKey: "rec.pos.dept.qa", locKey: "rec.pos.loc.all", typeKey: "rec.pos.type.full", lvlKey: "rec.pos.lvl.2" },
    { titleKey: "rec.pos.t6", deptKey: "rec.pos.dept.design", locKey: "rec.pos.loc.hcm", typeKey: "rec.pos.type.intern", lvlKey: "rec.pos.lvl.student" },
  ];

  const benefits = [
    { icon: Heart, title: t("rec.b1.title"), desc: t("rec.b1.desc") },
    { icon: GraduationCap, title: t("rec.b2.title"), desc: t("rec.b2.desc") },
    { icon: Coffee, title: t("rec.b3.title"), desc: t("rec.b3.desc") },
    { icon: Sparkles, title: t("rec.b4.title"), desc: t("rec.b4.desc") },
  ];

  return (
    <Layout>
      {/* Hero */}
      <section className="relative pt-32 pb-20 lg:pt-40 lg:pb-28 overflow-hidden">
        <img src={recruitHero} alt="" className="absolute inset-0 w-full h-full object-cover" />
        <div className="absolute inset-0 bg-gradient-to-r from-background via-background/95 to-background/40" />
        <div className="relative container-custom max-w-3xl">
          <p className="eyebrow text-primary mb-6">{t("rec.eyebrow")}</p>
          <h1 className="font-display text-5xl md:text-6xl lg:text-7xl font-extrabold leading-[1.05] tracking-tight text-balance">
            {t("rec.titleA")} <span className="text-gradient-primary">{t("rec.titleB")}</span>.
          </h1>
          <p className="mt-8 text-lg lg:text-xl text-muted-foreground leading-relaxed max-w-2xl">
            {t("rec.intro")}
          </p>
          <div className="mt-10 flex flex-wrap gap-4">
            <a href="#positions" className="btn-pill btn-gradient text-white px-8 py-4 text-sm uppercase tracking-wider">
              {t("rec.cta1")} <ArrowUpRight className="w-4 h-4" />
            </a>
            <Link to="/contact" className="btn-pill bg-secondary border border-border px-8 py-4 text-sm uppercase tracking-wider hover:bg-foreground hover:text-background">
              {t("rec.cta2")}
            </Link>
          </div>
        </div>
      </section>

      {/* Culture gallery */}
      <section className="py-16 bg-surface">
        <div className="container-custom">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
            {[culture1, culture2, culture3].map((img, i) => (
              <div key={i} className="image-zoom aspect-[4/3] rounded-3xl overflow-hidden">
                <img src={img} alt="" loading="lazy" className="w-full h-full object-cover" />
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Benefits */}
      <section className="py-20 lg:py-28 bg-background">
        <div className="container-custom">
          <div className="text-center max-w-2xl mx-auto mb-14">
            <p className="eyebrow text-primary mb-6 justify-center">{t("rec.benefits.eyebrow")}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
              {t("rec.benefits.titleA")} <span className="text-gradient-primary">{t("rec.benefits.titleB")}</span>.
            </h2>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {benefits.map((b, i) => (
              <div key={i} className="bg-card border border-border rounded-3xl p-7 hover-lift">
                <div className="w-14 h-14 rounded-2xl bg-gradient-primary text-white flex items-center justify-center mb-5 shadow-glow">
                  <b.icon className="w-6 h-6" strokeWidth={1.75} />
                </div>
                <h3 className="font-display text-xl font-extrabold mb-2">{b.title}</h3>
                <p className="text-muted-foreground text-sm leading-relaxed">{b.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Open positions */}
      <section id="positions" className="py-20 lg:py-28 bg-surface">
        <div className="container-custom">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6 mb-10">
            <div className="max-w-xl">
              <p className="eyebrow text-primary mb-6">{t("rec.positions.eyebrow")}</p>
              <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
                {positions.length} {t("rec.positions.countA")} <span className="text-gradient-primary">{t("rec.positions.countB")}</span>.
              </h2>
            </div>
          </div>
          <div className="space-y-3">
            {positions.map((p, i) => (
              <div
                key={i}
                className="group bg-card border border-border rounded-2xl p-5 lg:p-6 flex flex-col lg:flex-row lg:items-center gap-5 hover-lift"
              >
                <div className="flex-1 min-w-0">
                  <p className="text-[10px] uppercase tracking-wider font-bold text-primary mb-1">{t(p.deptKey)}</p>
                  <h3 className="font-display text-xl lg:text-2xl font-extrabold group-hover:text-primary transition-colors">
                    {t(p.titleKey)}
                  </h3>
                </div>
                <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
                  <span className="flex items-center gap-1.5"><MapPin className="w-3.5 h-3.5" /> {t(p.locKey)}</span>
                  <span className="flex items-center gap-1.5"><Clock className="w-3.5 h-3.5" /> {t(p.typeKey)}</span>
                  <span className="flex items-center gap-1.5"><Briefcase className="w-3.5 h-3.5" /> {t(p.lvlKey)}</span>
                </div>
                <Link
                  to="/contact"
                  className="btn-pill btn-gradient text-white px-6 py-3 text-xs uppercase tracking-wider shrink-0"
                >
                  {t("rec.apply")} <ArrowUpRight className="w-4 h-4" />
                </Link>
              </div>
            ))}
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default Recruitment;
