import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ArrowUpRight, MapPin, Maximize2, Search } from "lucide-react";
import Layout from "@/components/layout/Layout";
import PageHeader from "@/components/PageHeader";
import { projects } from "@/data/projects";
import { cn } from "@/lib/utils";
import { useI18n } from "@/lib/i18n";

const filterIds = ["all", "ongoing", "completed"] as const;
type FilterId = (typeof filterIds)[number];

const Projects = () => {
  const { t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const initial = (searchParams.get("status") as FilterId) || "all";
  const [filter, setFilter] = useState<FilterId>(
    filterIds.includes(initial) ? initial : "all"
  );
  const [q, setQ] = useState("");

  const filters: { id: FilterId; label: string }[] = [
    { id: "all", label: t("projectsPage.filter.all") },
    { id: "ongoing", label: t("projectsPage.filter.ongoing") },
    { id: "completed", label: t("projectsPage.filter.completed") },
  ];

  useEffect(() => {
    const param = searchParams.get("status") as FilterId | null;
    if (param && filterIds.includes(param)) setFilter(param);
  }, [searchParams]);

  const updateFilter = (id: FilterId) => {
    setFilter(id);
    if (id === "all") {
      searchParams.delete("status");
    } else {
      searchParams.set("status", id);
    }
    setSearchParams(searchParams, { replace: true });
  };

  const filtered = useMemo(
    () =>
      projects.filter((p) => {
        const matchStatus = filter === "all" || p.status === filter;
        const matchQ =
          !q ||
          p.name.toLowerCase().includes(q.toLowerCase()) ||
          p.client.toLowerCase().includes(q.toLowerCase()) ||
          p.location.toLowerCase().includes(q.toLowerCase());
        return matchStatus && matchQ;
      }),
    [filter, q]
  );

  return (
    <Layout>
      <PageHeader
        eyebrow={t("projectsPage.eyebrow")}
        title={t("projectsPage.title")}
        description={t("projectsPage.desc")}
      />

      {/* Filter bar */}
      <section className="py-10 bg-background border-b border-border">
        <div className="container-custom">
          <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-5">
            <div className="flex flex-wrap gap-2">
              {filters.map((f) => (
                <button
                  key={f.id}
                  onClick={() => updateFilter(f.id)}
                  className={cn(
                    "btn-pill px-5 py-2.5 text-xs uppercase tracking-wider border transition-all",
                    filter === f.id
                      ? "btn-gradient text-white border-transparent"
                      : "bg-secondary border-border text-foreground/70 hover:text-foreground"
                  )}
                >
                  {f.label}
                </button>
              ))}
            </div>
            <div className="flex items-center gap-2 bg-secondary rounded-full px-5 py-2.5 w-full lg:w-80 border border-border">
              <Search className="w-4 h-4 text-muted-foreground shrink-0" />
              <input
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder={t("projectsPage.searchPh")}
                className="bg-transparent text-sm focus:outline-none flex-1 placeholder:text-muted-foreground"
              />
            </div>
          </div>
        </div>
      </section>

      {/* Grid */}
      <section className="py-16 lg:py-20 bg-background">
        <div className="container-custom">
          {filtered.length === 0 ? (
            <p className="text-center py-20 text-muted-foreground">{t("projectsPage.empty")}</p>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6 lg:gap-8">
              {filtered.map((p) => (
                <Link
                  key={p.id}
                  to={`/projects/${p.id}`}
                  className="group block card-hover bg-card rounded-3xl overflow-hidden border border-border"
                >
                  <div className="image-zoom relative aspect-[4/3] overflow-hidden bg-muted">
                    <img src={p.img} alt={p.name} loading="lazy" className="w-full h-full object-cover" />
                    <div className="absolute top-5 left-5 flex gap-2">
                      <span className={cn("chip", p.status === "ongoing" ? "chip-orange" : "chip-success", "bg-white/95 backdrop-blur")}>
                        {p.status === "ongoing" ? t("projectsPage.filter.ongoing") : t("projectsPage.filter.completed")}
                      </span>
                    </div>
                    {p.year && (
                      <div className="absolute bottom-5 right-5 bg-white/95 backdrop-blur rounded-full px-4 py-1.5 text-xs font-extrabold text-foreground">
                        {p.year}
                      </div>
                    )}
                  </div>
                  <div className="p-6">
                    <p className="text-[10px] uppercase tracking-[0.18em] font-bold text-muted-foreground mb-2">{p.category}</p>
                    <h3 className="font-display text-xl lg:text-2xl font-extrabold mb-3 group-hover:text-primary transition-colors leading-tight">
                      {p.name}
                    </h3>
                    <div className="space-y-1.5 text-sm text-muted-foreground mb-5">
                      <p className="flex items-center gap-2"><MapPin className="w-3.5 h-3.5" /> {p.location}</p>
                      <p className="flex items-center gap-2"><Maximize2 className="w-3.5 h-3.5" /> {p.scale}</p>
                    </div>
                    <div className="flex items-center justify-between pt-4 border-t border-border">
                      <span className="text-xs uppercase tracking-wider font-bold text-foreground/70">{p.scope}</span>
                      <span className="w-9 h-9 rounded-full bg-secondary group-hover:bg-gradient-primary group-hover:text-white flex items-center justify-center transition-all">
                        <ArrowUpRight className="w-4 h-4" />
                      </span>
                    </div>
                  </div>
                </Link>
              ))}
            </div>
          )}
        </div>
      </section>
    </Layout>
  );
};

export default Projects;
