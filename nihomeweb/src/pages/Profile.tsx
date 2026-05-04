import Layout from "@/components/layout/Layout";
import PageHeader from "@/components/PageHeader";
import {
  Award,
  Download,
  FileText,
} from "lucide-react";
import { useI18n } from "@/lib/i18n";
import { contentApi, type AboutSectionResponse } from "@/services/contentApi";
import { useEffect, useMemo, useState } from "react";
import {
  ABOUT_ICON_META,
  DEFAULT_STATS_ICON_KEYS,
  DEFAULT_STRATEGY_ICON_KEYS,
  DEFAULT_VALUE_ICON_KEYS,
  parseOrganizationContent,
  resolveAboutIconKey,
  sortItemsBySortOrder,
} from "@/lib/aboutSectionContent";

const Section = ({ id, children, bg = "bg-background" }: { id?: string; children: React.ReactNode; bg?: string }) => (
  <section id={id} className={`py-20 lg:py-28 ${bg} scroll-mt-24`}>
    <div className="container-custom">{children}</div>
  </section>
);

type SimpleItem = { title: string; desc: string };
type IconTextItem = { iconKey?: string; iconClass?: string; title: string; desc: string; isActive?: boolean; sortOrder?: number };
type StatItem = { iconKey?: string; iconClass?: string; num: string; label: string; isActive?: boolean; sortOrder?: number };
type LeaderItem = { role: string; name: string; isActive?: boolean; sortOrder?: number };
type OrganizationItems = { board: LeaderItem[]; directors: LeaderItem[] };
type TimelineItem = { year: string; title: string; desc: string; sortOrder?: number };
type CertificationItem = { name: string; desc: string; sortOrder?: number };
type DownloadItem = { name: string; size: string; type: string; url?: string; sortOrder?: number };

const hasOrganizationMembers = (items: { board: readonly unknown[]; directors: readonly unknown[] }) =>
  items.board.length > 0 || items.directors.length > 0;

const normalizeLeadershipItems = (items: Array<Partial<LeaderItem>>): LeaderItem[] =>
  items.map((item) => ({
    role: item.role ?? "",
    name: item.name ?? "",
    isActive: item.isActive ?? true,
    sortOrder: item.sortOrder,
  }));

const parseItems = <T,>(value: string | null | undefined, fallback: T): T => {
  if (!value) return fallback;

  try {
    return JSON.parse(value) as T;
  } catch {
    return fallback;
  }
};

const Profile = () => {
  const { t } = useI18n();
  const [aboutSections, setAboutSections] = useState<AboutSectionResponse[]>([]);

  useEffect(() => {
    let canceled = false;
    const load = async () => {
      try {
        const { data } = await contentApi.getAboutSections(true);
        if (!canceled) setAboutSections(data);
      } catch {
        if (!canceled) setAboutSections([]);
      }
    };
    void load();
    return () => {
      canceled = true;
    };
  }, []);

  const aboutMain = useMemo(
    () => aboutSections.find((x) => x.slug === "about-main") ?? aboutSections[0],
    [aboutSections],
  );
  const statsMain = useMemo(
    () => aboutSections.find((x) => x.slug === "stats-main"),
    [aboutSections],
  );
  const valuesMain = useMemo(
    () => aboutSections.find((x) => x.slug === "values-main"),
    [aboutSections],
  );
  const strategyMain = useMemo(
    () => aboutSections.find((x) => x.slug === "strategy-main"),
    [aboutSections],
  );
  const organizationMain = useMemo(
    () => aboutSections.find((x) => x.slug === "organization-main"),
    [aboutSections],
  );
  const timelineMain = useMemo(
    () => aboutSections.find((x) => x.slug === "timeline-main"),
    [aboutSections],
  );
  const certsMain = useMemo(
    () => aboutSections.find((x) => x.slug === "certs-main"),
    [aboutSections],
  );
  const downloadsMain = useMemo(
    () => aboutSections.find((x) => x.slug === "downloads-main"),
    [aboutSections],
  );

  const milestones = sortItemsBySortOrder(parseItems<TimelineItem[]>(timelineMain?.itemsJson, []));
  const values = sortItemsBySortOrder(parseItems<IconTextItem[]>(valuesMain?.itemsJson, [])).filter((item) => item.isActive !== false);
  const stats = sortItemsBySortOrder(parseItems<StatItem[]>(statsMain?.itemsJson, [])).filter((item) => item.isActive !== false);
  const businessLines = sortItemsBySortOrder(parseItems<IconTextItem[]>(strategyMain?.itemsJson, [])).filter((item) => item.isActive !== false);
  const leadershipData = useMemo(() => {
    if (!organizationMain?.itemsJson?.trim()) {
      return { board: [], directors: [] } as OrganizationItems;
    }

    const parsed = parseOrganizationContent(organizationMain.itemsJson);
    if (!hasOrganizationMembers(parsed)) {
      return { board: [], directors: [] } as OrganizationItems;
    }

    return {
      board: normalizeLeadershipItems(parsed.board),
      directors: normalizeLeadershipItems(parsed.directors),
    };
  }, [organizationMain?.itemsJson]);
  const certifications = sortItemsBySortOrder(parseItems<CertificationItem[]>(certsMain?.itemsJson, []));
  const downloads = sortItemsBySortOrder(parseItems<DownloadItem[]>(downloadsMain?.itemsJson, []));
  const boardMembers = sortItemsBySortOrder(leadershipData.board).filter((item) => item.isActive !== false);
  const directors = sortItemsBySortOrder(leadershipData.directors).filter((item) => item.isActive !== false);

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
            {aboutMain?.imageUrl ? (
              <img src={aboutMain.imageUrl} alt="NICON leadership" className="w-full h-full object-cover" loading="lazy" />
            ) : null}
          </div>
          <div className="lg:col-span-5 lg:pt-6">
            <p className="eyebrow text-primary mb-6">{aboutMain?.eyebrow}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] mb-8 tracking-tight text-balance">
              {aboutMain?.titleA}{" "}
              <span className="text-gradient-primary">{aboutMain?.titleB}</span>.
            </h2>
            <p className="text-lg text-foreground/80 leading-relaxed mb-6">{aboutMain?.paragraph1}</p>
            <p className="text-base text-muted-foreground leading-relaxed">{aboutMain?.paragraph2}</p>
          </div>
        </div>
      </Section>

      {/* Stats */}
      <section className="py-12 bg-surface">
        <div className="container-custom grid grid-cols-2 lg:grid-cols-4 gap-5">
          {stats.map((s, i) => (
            <div key={`${s.iconKey ?? "calendar"}-${s.num}-${s.label}-${i}`} className="bg-card border border-border rounded-3xl p-7 text-center hover-lift">
              {(() => {
                const Icon = ABOUT_ICON_META[resolveAboutIconKey(s.iconKey ?? s.iconClass, DEFAULT_STATS_ICON_KEYS[i] ?? "award")].icon ?? Award;
                return <Icon className="w-7 h-7 text-primary mx-auto mb-4" strokeWidth={1.5} />;
              })()}
              <p className="font-display text-4xl font-extrabold text-gradient-primary mb-1">{s.num}</p>
              <p className="text-sm text-muted-foreground font-medium">{s.label}</p>
            </div>
          ))}
        </div>
      </section>

      {/* Core Values */}
      <Section bg="bg-background">
        <div className="text-center max-w-2xl mx-auto mb-16">
          <p className="eyebrow text-primary mb-6 justify-center">{valuesMain?.eyebrow}</p>
          <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
            {valuesMain?.titleA}{" "}
            <span className="text-gradient-primary">{valuesMain?.titleB}</span>.
          </h2>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          {values.map((v, i) => (
            <div key={i} className="bg-card border border-border rounded-3xl p-7 hover-lift">
              <div className="w-14 h-14 rounded-2xl bg-gradient-primary text-white flex items-center justify-center mb-5 shadow-glow">
                {(() => {
                  const Icon = ABOUT_ICON_META[resolveAboutIconKey(v.iconKey ?? v.iconClass, DEFAULT_VALUE_ICON_KEYS[i] ?? "target")].icon;
                  return <Icon className="w-6 h-6" strokeWidth={1.75} />;
                })()}
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
            <p className="eyebrow text-primary mb-6">{strategyMain?.eyebrow}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight text-balance">
              {strategyMain?.titleA}{" "}
              <span className="text-gradient-primary">{strategyMain?.titleB}</span>.
            </h2>
          </div>
          <div className="lg:col-span-7 lg:pt-4 space-y-5 text-foreground/80 leading-relaxed">
            <p>{strategyMain?.paragraph1}</p>
            <p>{strategyMain?.paragraph2}</p>
          </div>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {businessLines.map((b, i) => (
            <div key={i} className="bg-card rounded-3xl border border-border p-7 hover-lift">
              <div className="w-12 h-12 rounded-2xl bg-gradient-soft text-primary flex items-center justify-center mb-5">
                {(() => {
                  const Icon = ABOUT_ICON_META[resolveAboutIconKey(b.iconKey ?? b.iconClass, DEFAULT_STRATEGY_ICON_KEYS[i] ?? "building")].icon;
                  return <Icon className="w-6 h-6" strokeWidth={1.75} />;
                })()}
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
          <p className="eyebrow text-primary mb-6 justify-center">{organizationMain?.eyebrow}</p>
          <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
            {organizationMain?.titleA}{" "}
            <span className="text-gradient-primary">{organizationMain?.titleB}</span>.
          </h2>
        </div>
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <div className="bg-card border border-border rounded-3xl p-8">
            <h3 className="font-display text-xl font-extrabold mb-6 inline-flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-primary" /> {t("profilePage.org.board")}
            </h3>
            <ul className="divide-y divide-border">
              {boardMembers.map((p, i) => (
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
              {directors.map((p, i) => (
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
            <p className="eyebrow text-primary mb-6">{timelineMain?.eyebrow}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight text-balance">
              {timelineMain?.titleA}<br />
              <span className="text-gradient-primary">{timelineMain?.titleB}</span>.
            </h2>
            {timelineMain?.imageUrl ? <img src={timelineMain.imageUrl} alt="" className="mt-10 rounded-3xl w-full hover-lift" loading="lazy" /> : null}
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
          <p className="eyebrow text-primary mb-6 justify-center">{certsMain?.eyebrow}</p>
          <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight">
            {certsMain?.titleA}{" "}
            <span className="text-gradient-primary">{certsMain?.titleB}</span>.
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
            <p className="eyebrow text-primary mb-6">{downloadsMain?.eyebrow}</p>
            <h2 className="font-display text-4xl md:text-5xl font-extrabold leading-[1.05] tracking-tight text-balance">
              {downloadsMain?.titleA}<br />
              <span className="text-gradient-primary">{downloadsMain?.titleB}</span>.
            </h2>
            <p className="mt-6 text-muted-foreground leading-relaxed">{downloadsMain?.paragraph1}</p>
          </div>
          <div className="lg:col-span-7 space-y-3">
            {downloads.map((d, i) => (
              <a
                key={i}
                href={d.url || "#"}
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
