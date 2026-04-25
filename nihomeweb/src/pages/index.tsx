import { Link } from "react-router-dom";
import { useEffect, useState } from "react";
import { ArrowRight, ArrowUpRight, Award, Building2, Users, Calendar, Search, Sparkles, ChevronLeft, ChevronRight } from "lucide-react";
import Layout from "@/components/layout/Layout";
import heroImg from "@/assets/hero-factory.jpg";
import bma from "@/assets/project-bma.jpg";
import nbdc from "@/assets/project-nbdc.jpg";
import lhh from "@/assets/project-lhh.jpg";
import sports from "@/assets/project-sports.jpg";
import office from "@/assets/project-office.jpg";
import ceremony from "@/assets/activity-ceremony.jpg";
import handover from "@/assets/activity-handover.jpg";
import { useI18n } from "@/lib/i18n";

const heroSlides = [heroImg, bma, lhh, nbdc, sports];

const Index = () => {
  const { t } = useI18n();
  const [slide, setSlide] = useState(0);

  const goTo = (i: number) => setSlide((i + heroSlides.length) % heroSlides.length);
  const next = () => goTo(slide + 1);
  const prev = () => goTo(slide - 1);

  useEffect(() => {
    const id = setInterval(() => setSlide((s) => (s + 1) % heroSlides.length), 10000);
    return () => clearInterval(id);
  }, [slide]);

  const ongoingProjects = [
    { img: bma, name: t("home.proj.bma.name"), location: t("home.proj.bma.loc"), scale: "15.000 m²", category: t("home.cat.industrial") },
    { img: nbdc, name: t("home.proj.nbdc.name"), location: t("home.proj.nbdc.loc"), scale: t("home.proj.scaleDesign"), category: t("home.cat.manufacturing") },
    { img: lhh, name: t("home.proj.lhh.name"), location: t("home.proj.lhh.loc"), scale: "250.000 m²", category: t("home.cat.industrial") },
    { img: sports, name: t("home.proj.sports.name"), location: t("home.proj.sports.loc"), scale: t("home.proj.scaleDesign"), category: t("home.cat.public") },
  ];

  const completedProjects = [
    { img: bma, name: t("home.proj.trimas.name"), location: t("home.proj.trimas.loc"), scale: "10.000 m²", category: t("home.cat.industrial"), year: "2022" },
    { img: lhh, name: t("home.proj.apm.name"), location: t("home.proj.apm.loc"), scale: "6.500 m²", category: t("home.cat.logistics"), year: "2022" },
    { img: office, name: t("home.proj.b37.name"), location: t("home.proj.b37.loc"), scale: "1.200 m²", category: t("home.cat.interior"), year: "2024" },
    { img: nbdc, name: t("home.proj.stfm.name"), location: t("home.proj.stfm.loc"), scale: "12.000 m²", category: t("home.cat.fnb"), year: "2022" },
  ];

  const stats = [
    { num: "20+", label: t("home.stats.years"), icon: Calendar, gradient: "bg-gradient-primary" },
    { num: "150+", label: t("home.stats.projects"), icon: Building2, gradient: "bg-gradient-indigo" },
    { num: "80+", label: t("home.stats.clients"), icon: Users, gradient: "bg-gradient-success" },
    { num: "ISO", label: t("home.stats.iso"), icon: Award, gradient: "bg-gradient-primary" },
  ];

  const recentActivities = [
    { img: ceremony, date: "19.10.2025", cat: t("home.act.bma.cat"), title: t("home.act.bma.title"), desc: t("home.act.bma.desc") },
    { img: handover, date: "15.12.2025", cat: t("home.act.turnkey.cat"), title: t("home.act.turnkey.title"), desc: t("home.act.turnkey.desc") },
  ];

  return (
    <Layout>
      {/* HERO */}
      <section className="relative min-h-screen w-full overflow-hidden">
        {heroSlides.map((src, i) => {
          const offset = i - slide;
          return (
            <div
              key={i}
              className="absolute inset-0 overflow-hidden transition-transform duration-1000 ease-[cubic-bezier(0.7,0,0.3,1)]"
              style={{ transform: `translateX(${offset * 100}%)` }}
            >
              <img src={src} alt="NICON industrial factory" className="absolute inset-0 w-full h-full object-cover" />
            </div>
          );
        })}

        <button
          onClick={prev}
          aria-label={t("home.hero.prevAlt")}
          className="hidden md:flex absolute left-6 top-1/2 -translate-y-1/2 z-20 w-12 h-12 lg:w-14 lg:h-14 items-center justify-center rounded-full bg-white/15 backdrop-blur-md border border-white/25 text-white hover:bg-white hover:text-foreground transition-all hover:scale-110"
        >
          <ChevronLeft className="w-6 h-6" />
        </button>
        <button
          onClick={next}
          aria-label={t("home.hero.nextAlt")}
          className="hidden md:flex absolute right-6 top-1/2 -translate-y-1/2 z-20 w-12 h-12 lg:w-14 lg:h-14 items-center justify-center rounded-full bg-white/15 backdrop-blur-md border border-white/25 text-white hover:bg-white hover:text-foreground transition-all hover:scale-110"
        >
          <ChevronRight className="w-6 h-6" />
        </button>

        <div className="absolute bottom-8 left-1/2 -translate-x-1/2 z-20 flex gap-2">
          {heroSlides.map((_, i) => (
            <button
              key={i}
              onClick={() => goTo(i)}
              aria-label={`Slide ${i + 1}`}
              className={`h-1.5 rounded-full transition-all duration-500 ${i === slide ? "w-10 bg-primary" : "w-5 bg-white/40 hover:bg-white/70"}`}
            />
          ))}
        </div>
        <div className="pointer-events-none absolute inset-0 bg-gradient-to-b from-black/40 via-black/30 to-black/85" />
        <div className="pointer-events-none absolute inset-0 bg-gradient-mesh opacity-60 mix-blend-overlay" />

        <div className="relative z-10 min-h-screen flex flex-col justify-center pt-32 pb-32">
          <div className="container-custom">
            <div className="max-w-4xl">
              <span className="inline-flex items-center gap-2 px-4 py-1.5 rounded-full bg-white/10 backdrop-blur-md border border-white/20 text-xs uppercase tracking-[0.22em] text-white font-bold fade-in-up">
                <Sparkles className="w-3.5 h-3.5 text-primary-glow" /> {t("home.hero.tag")}
              </span>
              <h1 className="font-display mt-6 text-5xl md:text-7xl lg:text-[88px] font-extrabold text-white leading-[1.02] tracking-tight text-balance fade-in-up" style={{ animationDelay: "0.15s" }}>
                {t("home.hero.title1")}<br />
                <span className="text-gradient-primary">{t("home.hero.title2")}</span><br />
                {t("home.hero.title3")}
              </h1>
              <p className="mt-8 text-lg md:text-xl text-white/85 max-w-2xl leading-relaxed fade-in-up" style={{ animationDelay: "0.3s" }}>
                {t("home.hero.desc")}
              </p>
              <div className="mt-10 flex flex-wrap gap-4 fade-in-up" style={{ animationDelay: "0.45s" }}>
                <Link
                  to="/projects"
                  className="btn-pill btn-gradient text-white px-8 py-4 text-sm uppercase tracking-wider"
                >
                  {t("home.hero.cta1")}
                  <ArrowRight className="w-4 h-4" />
                </Link>
                <Link
                  to="/contact"
                  className="btn-pill bg-white/10 backdrop-blur-md border border-white/25 text-white px-8 py-4 text-sm uppercase tracking-wider hover:bg-white hover:text-foreground transition-all"
                >
                  {t("home.hero.cta2")}
                </Link>
              </div>
            </div>

            {/* Floating search pill */}
            <div className="mt-16 lg:mt-20 max-w-5xl fade-in-up" style={{ animationDelay: "0.6s" }}>
              <div className="bg-white/95 backdrop-blur-xl rounded-3xl md:rounded-full p-3 md:p-2 shadow-elegant flex flex-col md:flex-row md:items-center gap-2 md:gap-2">
                <div className="flex-1 min-w-0 md:min-w-[180px] px-5 py-2.5">
                  <p className="text-[10px] uppercase tracking-wider text-muted-foreground font-bold mb-0.5">{t("home.search.type")}</p>
                  <select className="w-full bg-transparent text-sm font-semibold focus:outline-none cursor-pointer">
                    <option>{t("home.search.typeAll")}</option>
                    <option>{t("home.search.typeFactory")}</option>
                    <option>{t("home.search.typeWorkshop")}</option>
                    <option>{t("home.search.typeOffice")}</option>
                  </select>
                </div>
                <div className="hidden md:block w-px h-10 bg-border" />
                <div className="md:hidden h-px w-full bg-border" />
                <div className="flex-1 min-w-0 md:min-w-[180px] px-5 py-2.5">
                  <p className="text-[10px] uppercase tracking-wider text-muted-foreground font-bold mb-0.5">{t("home.search.scale")}</p>
                  <select className="w-full bg-transparent text-sm font-semibold focus:outline-none cursor-pointer">
                    <option>{t("home.search.scaleAll")}</option>
                    <option>{"< 5.000 m²"}</option>
                    <option>5.000 – 20.000 m²</option>
                    <option>{"> 20.000 m²"}</option>
                  </select>
                </div>
                <div className="hidden md:block w-px h-10 bg-border" />
                <div className="md:hidden h-px w-full bg-border" />
                <div className="flex-1 min-w-0 md:min-w-[180px] px-5 py-2.5">
                  <p className="text-[10px] uppercase tracking-wider text-muted-foreground font-bold mb-0.5">{t("home.search.location")}</p>
                  <select className="w-full bg-transparent text-sm font-semibold focus:outline-none cursor-pointer">
                    <option>{t("home.search.locationAll")}</option>
                    <option>TP. HCM</option>
                    <option>{t("home.proj.lhh.loc")}</option>
                    <option>{t("home.proj.bma.loc")}</option>
                  </select>
                </div>
                <button className="btn-gradient text-white w-full md:w-14 h-12 md:h-14 rounded-2xl md:rounded-full flex items-center justify-center gap-2 shrink-0 hover:scale-[1.02] md:hover:scale-105 transition-transform">
                  <Search className="w-5 h-5" />
                  <span className="md:hidden text-sm font-bold uppercase tracking-wider">{t("home.search.button")}</span>
                </button>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* STATS */}
      <section className="py-16 lg:py-20 bg-background">
        <div className="container-custom">
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-5">
            {stats.map((s, i) => (
              <div key={i} className={`admin-stat-card-fake ${s.gradient} text-white rounded-3xl p-7 relative overflow-hidden card-hover`}>
                <div className="relative z-10">
                  <s.icon className="w-7 h-7 mb-4 opacity-80" strokeWidth={1.5} />
                  <p className="font-display text-4xl lg:text-5xl font-extrabold mb-1 leading-none">{s.num}</p>
                  <p className="text-sm text-white/85 font-medium">{s.label}</p>
                </div>
                <div className="absolute -top-8 -right-8 w-32 h-32 bg-white/10 rounded-full" />
                <div className="absolute -bottom-12 -right-4 w-24 h-24 bg-white/5 rounded-full" />
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* INTRO */}
      <section className="py-20 lg:py-28 bg-surface">
        <div className="container-custom">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 lg:gap-20 items-start">
            <div className="lg:col-span-5">
              <p className="eyebrow text-primary mb-6">{t("home.intro.eyebrow")}</p>
              <h2 className="font-display text-4xl md:text-5xl lg:text-6xl font-extrabold leading-[1.05] tracking-tight text-balance">
                {t("home.intro.titleA")} <span className="text-gradient-primary">{t("home.intro.titleB")}</span>.
              </h2>
            </div>
            <div className="lg:col-span-7 lg:pt-4">
              <p className="text-lg lg:text-xl text-foreground/85 leading-relaxed mb-6 font-medium">
                {t("home.intro.p1")}
              </p>
              <p className="text-base text-muted-foreground leading-relaxed mb-10">
                {t("home.intro.p2")}
              </p>
              <Link to="/profile" className="btn-pill btn-gradient text-white px-7 py-3.5 text-sm uppercase tracking-wider">
                {t("home.intro.cta")}
                <ArrowUpRight className="w-4 h-4" />
              </Link>
            </div>
          </div>
        </div>
      </section>

      {/* ONGOING PROJECTS */}
      <section className="py-20 lg:py-28 bg-background">
        <div className="container-custom">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-8 mb-14">
            <div className="max-w-2xl">
              <p className="eyebrow text-primary mb-6">{t("home.ongoing.eyebrow")}</p>
              <h2 className="font-display text-4xl md:text-5xl lg:text-6xl font-extrabold leading-[1.05] tracking-tight text-balance">
                {t("home.ongoing.titleA")}<br />{t("home.ongoing.titleB")} <span className="text-gradient-primary">{t("home.ongoing.titleC")}</span>{t("home.ongoing.titleEnd")}
              </h2>
            </div>
            <Link to="/projects?status=ongoing" className="btn-pill bg-secondary hover:bg-foreground hover:text-background border border-border px-6 py-3 text-xs uppercase tracking-wider self-start md:self-end">
              {t("common.viewAll")}
              <ArrowUpRight className="w-4 h-4" />
            </Link>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 lg:gap-8">
            {ongoingProjects.map((p, i) => (
              <Link
                to="/projects?status=ongoing"
                key={i}
                className="group block card-hover bg-card rounded-3xl overflow-hidden border border-border"
              >
                <div className="image-zoom relative aspect-[4/3] overflow-hidden bg-muted">
                  <img src={p.img} alt={p.name} loading="lazy" className="w-full h-full object-cover" />
                  <div className="absolute top-5 left-5 chip chip-primary bg-white/95 backdrop-blur">
                    {p.category}
                  </div>
                </div>
                <div className="p-6 flex items-start justify-between gap-4">
                  <div>
                    <h3 className="font-display text-2xl font-extrabold mb-1.5 group-hover:text-primary transition-colors">
                      {p.name}
                    </h3>
                    <p className="text-sm text-muted-foreground">{p.location} · {p.scale}</p>
                  </div>
                  <span className="w-11 h-11 rounded-full bg-secondary group-hover:bg-gradient-primary group-hover:text-white flex items-center justify-center transition-all shrink-0">
                    <ArrowUpRight className="w-5 h-5 group-hover:translate-x-0.5 group-hover:-translate-y-0.5 transition-transform" />
                  </span>
                </div>
              </Link>
            ))}
          </div>
        </div>
      </section>

      {/* COMPLETED PROJECTS */}
      <section className="py-20 lg:py-28 bg-surface">
        <div className="container-custom">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-8 mb-14">
            <div className="max-w-2xl">
              <p className="eyebrow text-primary mb-6">{t("home.completed.eyebrow")}</p>
              <h2 className="font-display text-4xl md:text-5xl lg:text-6xl font-extrabold leading-[1.05] tracking-tight text-balance">
                {t("home.completed.titleA")}<br /><span className="text-gradient-primary">{t("home.completed.titleB")}</span>.
              </h2>
            </div>
            <Link to="/projects?status=completed" className="btn-pill bg-card hover:bg-foreground hover:text-background border border-border px-6 py-3 text-xs uppercase tracking-wider self-start md:self-end">
              {t("common.viewAll")}
              <ArrowUpRight className="w-4 h-4" />
            </Link>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-5 lg:gap-6">
            {completedProjects.map((p, i) => (
              <Link
                to="/projects?status=completed"
                key={i}
                className="group block card-hover bg-card rounded-3xl overflow-hidden border border-border"
              >
                <div className="image-zoom relative aspect-[4/3] overflow-hidden bg-muted">
                  <img src={p.img} alt={p.name} loading="lazy" className="w-full h-full object-cover" />
                  <div className="absolute top-4 left-4 chip chip-success bg-white/95 backdrop-blur text-[10px]">
                    {t("home.completed.statusDone")}
                  </div>
                  <div className="absolute bottom-4 right-4 bg-white/95 backdrop-blur rounded-full px-3 py-1 text-[10px] font-extrabold text-foreground">
                    {p.year}
                  </div>
                </div>
                <div className="p-5">
                  <p className="text-[10px] uppercase tracking-[0.18em] font-bold text-muted-foreground mb-1.5">{p.category}</p>
                  <h3 className="font-display text-base font-extrabold mb-1.5 group-hover:text-primary transition-colors leading-tight line-clamp-2">
                    {p.name}
                  </h3>
                  <p className="text-xs text-muted-foreground line-clamp-1">{p.location} · {p.scale}</p>
                </div>
              </Link>
            ))}
          </div>
        </div>
      </section>

      {/* SERVICE BIG CTA */}
      <section className="relative py-24 lg:py-36 bg-surface-dark text-surface-dark-foreground overflow-hidden">
        <div className="absolute inset-0 opacity-25">
          <img src={ceremony} alt="" className="w-full h-full object-cover" loading="lazy" />
          <div className="absolute inset-0 bg-gradient-to-r from-surface-dark via-surface-dark/95 to-surface-dark/40" />
        </div>
        <div className="absolute -top-32 -right-32 w-96 h-96 rounded-full bg-primary/30 blur-3xl" />

        <div className="relative container-custom">
          <div className="max-w-3xl">
            <p className="eyebrow text-primary-glow mb-6">{t("home.cta.eyebrow")}</p>
            <h2 className="font-display text-4xl md:text-5xl lg:text-6xl font-extrabold leading-[1.05] mb-8 text-balance">
              {t("home.cta.titleA")}<br />
              <span className="text-gradient-primary">{t("home.cta.titleB")}</span>
            </h2>
            <p className="text-lg text-white/75 leading-relaxed max-w-2xl mb-10">
              {t("home.cta.body")}
            </p>
            <Link
              to="/services"
              className="btn-pill btn-gradient text-white px-8 py-4 text-sm uppercase tracking-wider"
            >
              {t("home.cta.button")}
              <ArrowRight className="w-4 h-4" />
            </Link>
          </div>
        </div>
      </section>

      {/* ACTIVITIES */}
      <section className="py-20 lg:py-28 bg-background">
        <div className="container-custom">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-8 mb-14">
            <div className="max-w-2xl">
              <p className="eyebrow text-primary mb-6">{t("home.activities.eyebrow")}</p>
              <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] text-balance">
                {t("home.activities.titleA")}<br /><span className="text-gradient-primary">{t("home.activities.titleB")}</span>.
              </h2>
            </div>
            <Link to="/activities" className="btn-pill bg-secondary hover:bg-foreground hover:text-background border border-border px-6 py-3 text-xs uppercase tracking-wider self-start md:self-end">
              {t("home.activities.viewAll")}
              <ArrowUpRight className="w-4 h-4" />
            </Link>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 lg:gap-8">
            {recentActivities.map((it, i) => (
              <article key={i} className="group cursor-pointer card-hover bg-card rounded-3xl overflow-hidden border border-border">
                <div className="image-zoom relative aspect-[4/3] overflow-hidden bg-muted">
                  <img src={it.img} alt="" loading="lazy" className="w-full h-full object-cover" />
                  <div className="absolute top-5 left-5 chip chip-orange bg-white/95 backdrop-blur">
                    {it.cat}
                  </div>
                </div>
                <div className="p-7">
                  <p className="text-xs uppercase tracking-wider text-muted-foreground font-bold mb-3">{it.date}</p>
                  <h3 className="font-display text-xl lg:text-2xl font-extrabold mb-3 group-hover:text-primary transition-colors text-balance leading-tight">
                    {it.title}
                  </h3>
                  <p className="text-muted-foreground line-clamp-2">{it.desc}</p>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      {/* PARTNER STRIP */}
      <section className="py-16 bg-gradient-soft">
        <div className="container-custom">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-10 items-center text-center md:text-left">
            <div>
              <p className="eyebrow text-primary mb-3 justify-center md:justify-start">{t("home.partner.strategicEyebrow")}</p>
              <h3 className="font-display text-2xl font-extrabold">{t("home.partner.strategicName")}</h3>
              <p className="text-sm text-muted-foreground mt-2">{t("home.partner.strategicDesc")}</p>
            </div>
            <div className="text-center">
              <div className="inline-flex flex-col items-center px-8 py-6 rounded-3xl bg-white shadow-soft border border-border">
                <div className="font-display text-2xl font-extrabold tracking-tight">QUACERT × JAS-ANZ</div>
                <p className="text-xs uppercase tracking-[0.2em] text-muted-foreground mt-2 font-bold">ISO 9001 : 2015</p>
              </div>
            </div>
            <div className="md:text-right">
              <p className="eyebrow text-primary mb-3 justify-center md:justify-end">{t("home.partner.brandEyebrow")}</p>
              <h3 className="font-display text-2xl font-extrabold">{t("home.partner.brandName")}</h3>
              <p className="text-sm text-muted-foreground mt-2">{t("home.partner.brandDesc")}</p>
            </div>
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default Index;
