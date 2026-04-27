import Layout from "@/components/layout/Layout";
import PageHeader from "@/components/PageHeader";
import {
  Award, Building2, Users, Calendar, Target, Heart, Compass, Shield,
  Briefcase, Users2, Hammer, Layers, Wrench, Download, FileText
} from "lucide-react";
import leadership from "@/assets/profile-leadership.jpg";
import activitiesImg from "@/assets/profile-activities.jpg";
import { useI18n } from "@/lib/i18n";

const Section = ({ id, children, bg = "bg-background" }: { id?: string; children: React.ReactNode; bg?: string }) => (
  <section id={id} className={`py-20 lg:py-28 ${bg} scroll-mt-24`}>
    <div className="container-custom">{children}</div>
  </section>
);

const Profile = () => {
  const { t } = useI18n();

  const milestones = [
    { year: "2006", title: t("profilePage.ms.2006.t"), desc: t("profilePage.ms.2006.d") },
    { year: "2007", title: t("profilePage.ms.2007.t"), desc: t("profilePage.ms.2007.d") },
    { year: "2010", title: t("profilePage.ms.2010.t"), desc: t("profilePage.ms.2010.d") },
    { year: "2016", title: t("profilePage.ms.2016.t"), desc: t("profilePage.ms.2016.d") },
    { year: "2018", title: t("profilePage.ms.2018.t"), desc: t("profilePage.ms.2018.d") },
    { year: "2024", title: t("profilePage.ms.2024.t"), desc: t("profilePage.ms.2024.d") },
  ];

  const values = [
    { icon: Target, title: t("profilePage.v1.title"), desc: t("profilePage.v1.desc") },
    { icon: Shield, title: t("profilePage.v2.title"), desc: t("profilePage.v2.desc") },
    { icon: Compass, title: t("profilePage.v3.title"), desc: t("profilePage.v3.desc") },
    { icon: Heart, title: t("profilePage.v4.title"), desc: t("profilePage.v4.desc") },
  ];

  const stats = [
    { num: t("profilePage.stat.yearsValue"), label: t("profilePage.stat.years"), icon: Calendar },
    { num: t("profilePage.stat.projectsValue"), label: t("profilePage.stat.projects"), icon: Building2 },
    { num: t("profilePage.stat.clientsValue"), label: t("profilePage.stat.clients"), icon: Users },
    { num: t("profilePage.stat.isoTop"), label: t("profilePage.stat.isoBottom"), icon: Award },
  ];

  const businessLines = [
    { icon: Building2, title: t("profilePage.bl1.title"), desc: t("profilePage.bl1.desc") },
    { icon: Hammer, title: t("profilePage.bl2.title"), desc: t("profilePage.bl2.desc") },
    { icon: Layers, title: t("profilePage.bl3.title"), desc: t("profilePage.bl3.desc") },
    { icon: Wrench, title: t("profilePage.bl4.title"), desc: t("profilePage.bl4.desc") },
    { icon: Briefcase, title: t("profilePage.bl5.title"), desc: t("profilePage.bl5.desc") },
    { icon: Users2, title: t("profilePage.bl6.title"), desc: t("profilePage.bl6.desc") },
  ];

  const leadership_data = {
    board: [
      { role: t("profilePage.ld.role.chair"), name: t("profilePage.ld.name.chair") },
      { role: t("profilePage.ld.role.viceChair"), name: t("profilePage.ld.name.viceChair1") },
      { role: t("profilePage.ld.role.viceChair"), name: t("profilePage.ld.name.viceChair2") },
      { role: t("profilePage.ld.role.secretary"), name: t("profilePage.ld.name.secretary") },
    ],
    directors: [
      { role: t("profilePage.ld.role.ceo"), name: t("profilePage.ld.name.ceo") },
      { role: t("profilePage.ld.role.bdJp"), name: t("profilePage.ld.name.bdJp") },
      { role: t("profilePage.ld.role.bdAsia"), name: t("profilePage.ld.name.bdAsia") },
      { role: t("profilePage.ld.role.design"), name: t("profilePage.ld.name.design") },
    ],
  };

  const certifications = [
    { name: t("profilePage.cert.iso2008.n"), desc: t("profilePage.cert.iso2008.d") },
    { name: t("profilePage.cert.iso2015.n"), desc: t("profilePage.cert.iso2015.d") },
    { name: t("profilePage.cert.iso14001.n"), desc: t("profilePage.cert.iso14001.d") },
  ];

  const downloads = [
    { name: t("profilePage.dl.f1"), size: t("profilePage.dl.f1.size"), type: t("profilePage.dl.f1.type") },
    { name: t("profilePage.dl.f2"), size: t("profilePage.dl.f2.size"), type: t("profilePage.dl.f2.type") },
    { name: t("profilePage.dl.f3"), size: t("profilePage.dl.f3.size"), type: t("profilePage.dl.f3.type") },
    { name: t("profilePage.dl.f4"), size: t("profilePage.dl.f4.size"), type: t("profilePage.dl.f4.type") },
  ];

  const navItems = [
    { href: "#about", label: t("profilePage.nav.about") },
    { href: "#strategy", label: t("profilePage.nav.strategy") },
    { href: "#org", label: t("profilePage.nav.org") },
    { href: "#timeline", label: t("profilePage.nav.timeline") },
    { href: "#certs", label: t("profilePage.nav.certs") },
    { href: "#downloads", label: t("profilePage.nav.downloads") },
  ];

  return (
    <Layout>
      <PageHeader
        eyebrow={t("profilePage.eyebrow")}
        title={t("profilePage.title")}
        description={t("profilePage.desc")}
      />

      {/* Anchor nav */}
      <nav className="sticky top-[80px] z-30 bg-background/85 backdrop-blur-xl border-b border-border">
        <div className="container-custom flex gap-2 overflow-x-auto py-3 scrollbar-hide">
          {navItems.map((l) => (
            <a
              key={l.href}
              href={l.href}
              className="shrink-0 px-4 py-2 rounded-full text-xs font-bold uppercase tracking-wider bg-secondary hover:bg-gradient-primary hover:text-white transition-all"
            >
              {l.label}
            </a>
          ))}
        </div>
      </nav>

      {/* About / Vision */}
      <Section id="about">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 lg:gap-20 items-start">
          <div className="lg:col-span-7 image-zoom rounded-3xl overflow-hidden">
            <img src={leadership} alt="NICON leadership" className="w-full h-full object-cover" loading="lazy" />
          </div>
          <div className="lg:col-span-5 lg:pt-6">
            <p className="eyebrow text-primary mb-6">{t("profilePage.about.eyebrow")}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] mb-8 tracking-tight text-balance">
              {t("profilePage.about.titleA")} <span className="text-gradient-primary">{t("profilePage.about.titleB")}</span>.
            </h2>
            <p className="text-lg text-foreground/80 leading-relaxed mb-6">{t("profilePage.about.p1")}</p>
            <p className="text-base text-muted-foreground leading-relaxed">{t("profilePage.about.p2")}</p>
          </div>
        </div>
      </Section>

      {/* Stats */}
      <section className="py-12 bg-surface">
        <div className="container-custom grid grid-cols-2 lg:grid-cols-4 gap-5">
          {stats.map((s, i) => (
            <div key={i} className="bg-card border border-border rounded-3xl p-7 text-center hover-lift">
              <s.icon className="w-7 h-7 text-primary mx-auto mb-4" strokeWidth={1.5} />
              <p className="font-display text-4xl font-extrabold text-gradient-primary mb-1">{s.num}</p>
              <p className="text-sm text-muted-foreground font-medium">{s.label}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Core Values */}
      <Section bg="bg-background">
        <div className="text-center max-w-2xl mx-auto mb-16">
          <p className="eyebrow text-primary mb-6 justify-center">{t("profilePage.values.eyebrow")}</p>
          <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
            {t("profilePage.values.titleA")} <span className="text-gradient-primary">NICON</span>.
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          {values.map((v, i) => (
            <div key={i} className="bg-card border border-border rounded-3xl p-7 hover-lift">
              <div className="w-14 h-14 rounded-2xl bg-gradient-primary text-white flex items-center justify-center mb-5 shadow-glow">
                <v.icon className="w-6 h-6" strokeWidth={1.75} />
              </div>
              <h3 className="font-display text-xl font-extrabold mb-2">{v.title}</h3>
              <p className="text-muted-foreground text-sm leading-relaxed">{v.desc}</p>
            </div>
          ))}
        </div>
      </Section>

      {/* Strategy / Business lines */}
      <Section id="strategy" bg="bg-surface">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 lg:gap-16 items-start mb-14">
          <div className="lg:col-span-5">
            <p className="eyebrow text-primary mb-6">{t("profilePage.strategy.eyebrow")}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight text-balance">
              {t("profilePage.strategy.titleA")} <span className="text-gradient-primary">{t("profilePage.strategy.titleB")}</span>.
            </h2>
          </div>
          <div className="lg:col-span-7 lg:pt-4 space-y-5 text-foreground/80 leading-relaxed">
            <p><strong>{t("profilePage.strategy.visionLabel")}:</strong> {t("profilePage.strategy.visionText")}</p>
            <p><strong>{t("profilePage.strategy.futureLabel")}:</strong> {t("profilePage.strategy.futureText")}</p>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {businessLines.map((b, i) => (
            <div key={i} className="bg-card rounded-3xl border border-border p-7 hover-lift">
              <div className="w-12 h-12 rounded-2xl bg-gradient-soft text-primary flex items-center justify-center mb-5">
                <b.icon className="w-6 h-6" strokeWidth={1.75} />
              </div>
              <h3 className="font-display text-lg font-extrabold mb-2">{b.title}</h3>
              <p className="text-muted-foreground text-sm leading-relaxed">{b.desc}</p>
            </div>
          ))}
        </div>
      </Section>

      {/* Organization */}
      <Section id="org" bg="bg-background">
        <div className="text-center max-w-2xl mx-auto mb-14">
          <p className="eyebrow text-primary mb-6 justify-center">{t("profilePage.org.eyebrow")}</p>
          <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
            {t("profilePage.org.titleA")} <span className="text-gradient-primary">{t("profilePage.org.titleB")}</span>.
          </h2>
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="bg-card border border-border rounded-3xl p-8">
            <h3 className="font-display text-xl font-extrabold mb-6 inline-flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-primary" /> {t("profilePage.org.board")}
            </h3>
            <ul className="divide-y divide-border">
              {leadership_data.board.map((p, i) => (
                <li key={i} className="py-3 flex justify-between gap-4">
                  <span className="text-sm text-muted-foreground font-bold uppercase tracking-wider">{p.role}</span>
                  <span className="font-display font-extrabold text-right">{p.name}</span>
                </li>
              ))}
            </ul>
          </div>
          <div className="bg-card border border-border rounded-3xl p-8">
            <h3 className="font-display text-xl font-extrabold mb-6 inline-flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-accent-orange" /> {t("profilePage.org.exec")}
            </h3>
            <ul className="divide-y divide-border">
              {leadership_data.directors.map((p, i) => (
                <li key={i} className="py-3 flex justify-between gap-4">
                  <span className="text-sm text-muted-foreground font-bold uppercase tracking-wider">{p.role}</span>
                  <span className="font-display font-extrabold text-right">{p.name}</span>
                </li>
              ))}
            </ul>
          </div>
        </div>
      </Section>

      {/* Timeline */}
      <Section id="timeline" bg="bg-surface">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-12">
          <div className="lg:col-span-4">
            <p className="eyebrow text-primary mb-6">{t("profilePage.timeline.eyebrow")}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight text-balance">
              {t("profilePage.timeline.titleA")}<br /><span className="text-gradient-primary">{t("profilePage.timeline.titleB")}</span>.
            </h2>
            <img src={activitiesImg} alt="" className="mt-10 rounded-3xl w-full hover-lift" loading="lazy" />
          </div>
          <div className="lg:col-span-8 lg:pl-8 relative">
            <div className="absolute left-[18px] top-2 bottom-2 w-px bg-gradient-to-b from-primary via-accent-orange to-transparent lg:left-7" />
            <div className="space-y-10">
              {milestones.map((m, i) => (
                <div key={i} className="relative pl-12 lg:pl-20">
                  <div className="absolute left-0 top-1 w-9 h-9 rounded-full bg-gradient-primary text-white flex items-center justify-center shadow-glow lg:w-12 lg:h-12">
                    <span className="text-[10px] font-extrabold tracking-wider lg:text-xs">{m.year}</span>
                  </div>
                  <h3 className="font-display text-2xl font-extrabold mb-2">{m.title}</h3>
                  <p className="text-muted-foreground leading-relaxed">{m.desc}</p>
                </div>
              ))}
            </div>
          </div>
        </div>
      </Section>

      {/* Certifications */}
      <Section id="certs" bg="bg-background">
        <div className="text-center max-w-2xl mx-auto mb-14">
          <p className="eyebrow text-primary mb-6 justify-center">{t("profilePage.certs.eyebrow")}</p>
          <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
            {t("profilePage.certs.titleA")} <span className="text-gradient-primary">{t("profilePage.certs.titleB")}</span>.
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {certifications.map((c, i) => (
            <div key={i} className="bg-gradient-soft rounded-3xl border border-border p-8 text-center hover-lift">
              <div className="w-16 h-16 rounded-full bg-gradient-primary text-white flex items-center justify-center mx-auto mb-5 shadow-glow">
                <Award className="w-8 h-8" strokeWidth={1.5} />
              </div>
              <h3 className="font-display text-xl font-extrabold mb-2">{c.name}</h3>
              <p className="text-sm text-muted-foreground">{c.desc}</p>
            </div>
          ))}
        </div>
      </Section>

      {/* Downloads */}
      <Section id="downloads" bg="bg-surface">
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-10 items-start">
          <div className="lg:col-span-5">
            <p className="eyebrow text-primary mb-6">{t("profilePage.dl.eyebrow")}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight text-balance">
              {t("profilePage.dl.titleA")}<br /><span className="text-gradient-primary">{t("profilePage.dl.titleB")}</span>.
            </h2>
            <p className="mt-6 text-muted-foreground leading-relaxed">{t("profilePage.dl.desc")}</p>
          </div>
          <div className="lg:col-span-7 space-y-3">
            {downloads.map((d, i) => (
              <a
                key={i}
                href="#"
                onClick={(e) => e.preventDefault()}
                className="flex items-center gap-5 bg-card rounded-2xl border border-border p-5 hover-lift group"
              >
                <span className="w-12 h-12 rounded-xl bg-gradient-soft text-primary flex items-center justify-center shrink-0">
                  <FileText className="w-6 h-6" strokeWidth={1.75} />
                </span>
                <div className="flex-1 min-w-0">
                  <p className="font-display font-extrabold truncate">{d.name}</p>
                  <p className="text-xs text-muted-foreground mt-0.5 uppercase tracking-wider font-bold">{d.type} · {d.size}</p>
                </div>
                <span className="w-10 h-10 rounded-full bg-secondary group-hover:bg-gradient-primary group-hover:text-white flex items-center justify-center transition-all shrink-0">
                  <Download className="w-4 h-4" />
                </span>
              </a>
            ))}
          </div>
        </div>
      </Section>
    </Layout>
  );
};

export default Profile;
