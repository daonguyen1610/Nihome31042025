import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { ArrowLeft, ArrowUpRight, MapPin, Maximize2, Briefcase, Calendar } from "lucide-react";
import Layout from "@/components/layout/Layout";
import { useI18n } from "@/lib/i18n";
import { useProject, useProjects } from "@/hooks/useContentApi";
import { PageLoading, PageError } from "@/components/PageState";
import { Dialog, DialogContent } from "@/components/ui/dialog";

const ProjectDetail = () => {
  const { t } = useI18n();
  const { slug } = useParams();
  const [selectedImage, setSelectedImage] = useState<string | null>(null);
  const { data: project, loading, error, refetch } = useProject(slug ?? "");
  const { data: allProjects } = useProjects();

  if (loading) return <Layout><PageLoading /></Layout>;
  if (error) return <Layout><PageError message={error} onRetry={refetch} /></Layout>;

  if (!project) {
    return (
      <Layout>
        <section className="pt-40 pb-20 container-custom text-center">
          <h1 className="font-display text-4xl font-extrabold mb-4">{t("projDetail.notFound")}</h1>
          <Link to="/projects" className="btn-pill btn-gradient text-white px-6 py-3 text-xs uppercase tracking-wider">
            <ArrowLeft className="w-4 h-4" /> {t("common.back")}
          </Link>
        </section>
      </Layout>
    );
  }

  const related = (allProjects ?? []).filter((p) => p.slug !== project.slug).slice(0, 3);

  return (
    <Layout>
      {/* Hero */}
      <section className="relative h-[70vh] min-h-[500px] overflow-hidden">
        <img src={project.imageUrl} alt={project.name} className="absolute inset-0 w-full h-full object-cover" />
        <div className="absolute inset-0 bg-gradient-to-b from-black/40 to-black/85" />
        <div className="relative z-10 h-full container-custom flex flex-col justify-end pb-16 pt-32">
          <Link to="/projects" className="inline-flex items-center gap-2 text-white/80 hover:text-white text-xs uppercase tracking-[0.22em] font-bold mb-6 w-fit">
            <ArrowLeft className="w-4 h-4" /> {t("projDetail.back")}
          </Link>
          <span className={`chip ${project.status === "ongoing" ? "chip-orange" : "chip-success"} bg-white/95 mb-6 w-fit`}>
            {project.status === "ongoing" ? t("projDetail.statusOngoing") : t("projDetail.statusDone")}
          </span>
          <h1 className="font-display text-4xl md:text-6xl lg:text-7xl font-extrabold text-white leading-[1.05] tracking-tight max-w-4xl text-balance">
            {project.name}
          </h1>
          <p className="mt-6 text-white/80 text-lg max-w-2xl">{project.client}</p>
        </div>
      </section>

      {/* Meta */}
      <section className="py-10 bg-surface border-b border-border">
        <div className="container-custom">
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-6">
            {[
              { icon: MapPin, label: t("projDetail.location"), value: project.location },
              { icon: Maximize2, label: t("projDetail.scale"), value: project.scale },
              { icon: Briefcase, label: t("projDetail.scope"), value: project.scope },
              { icon: Calendar, label: t("projDetail.year"), value: project.year ?? "—" },
            ].map((m, i) => (
              <div key={i} className="flex items-start gap-4">
                <div className="w-11 h-11 rounded-2xl bg-gradient-primary text-white flex items-center justify-center shrink-0">
                  <m.icon className="w-5 h-5" strokeWidth={1.75} />
                </div>
                <div>
                  <p className="text-[10px] uppercase tracking-wider font-bold text-muted-foreground">{m.label}</p>
                  <p className="font-display text-base font-extrabold mt-1">{m.value}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Content */}
      <section className="py-20 bg-background">
        <div className="container-custom grid grid-cols-1 lg:grid-cols-12 gap-12">
          <div className="lg:col-span-8 space-y-10">
            {project.description && (
              <div>
                <p className="eyebrow text-primary mb-6">{t("projDetail.overview")}</p>
                <p className="text-lg leading-relaxed text-foreground/85">{project.description}</p>
              </div>
            )}
            {project.challenges && project.challenges.length > 0 && (
              <div>
                <h2 className="font-display text-3xl font-extrabold mb-5">{t("projDetail.challenges")}</h2>
                <ul className="space-y-3">
                  {project.challenges.map((c, i) => (
                    <li key={i} className="flex gap-3 text-foreground/80 leading-relaxed">
                      <span className="w-6 h-6 rounded-full bg-gradient-primary text-white shrink-0 flex items-center justify-center text-xs font-extrabold">{i + 1}</span>
                      {c}
                    </li>
                  ))}
                </ul>
              </div>
            )}
            {project.solutions && project.solutions.length > 0 && (
              <div>
                <h2 className="font-display text-3xl font-extrabold mb-5">{t("projDetail.solutions")}</h2>
                <ul className="space-y-3">
                  {project.solutions.map((s, i) => (
                    <li key={i} className="flex gap-3 text-foreground/80 leading-relaxed">
                      <span className="w-6 h-6 rounded-full bg-gradient-indigo text-white shrink-0 flex items-center justify-center text-xs font-extrabold">✓</span>
                      {s}
                    </li>
                  ))}
                </ul>
              </div>
            )}
          </div>

          <aside className="lg:col-span-4">
            <div className="sticky top-28 bg-surface rounded-3xl border border-border p-7">
              <p className="eyebrow text-primary mb-5">{t("projDetail.highlights")}</p>
              <div className="grid grid-cols-2 gap-4">
                {project.highlights?.map((h, i) => (
                  <div key={i} className="bg-card rounded-2xl p-4 border border-border">
                    <p className="text-[10px] uppercase tracking-wider text-muted-foreground font-bold">{h.label}</p>
                    <p className="font-display text-lg font-extrabold text-gradient-primary mt-1">{h.value}</p>
                  </div>
                ))}
              </div>
              <Link
                to="/contact"
                className="mt-7 w-full btn-pill btn-gradient text-white py-3.5 text-xs uppercase tracking-wider"
              >
                {t("projDetail.consult")} <ArrowUpRight className="w-4 h-4" />
              </Link>
            </div>
          </aside>
        </div>
      </section>

      {/* Gallery */}
      {project.gallery && project.gallery.length > 0 && (
        <section className="py-16 bg-surface">
          <div className="container-custom">
            <p className="eyebrow text-primary mb-6">{t("projDetail.gallery")}</p>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
              {project.gallery.map((g, i) => (
                <button
                  key={i}
                  type="button"
                  onClick={() => setSelectedImage(g)}
                  className="image-zoom rounded-3xl overflow-hidden aspect-[4/3] bg-muted w-full text-left"
                >
                  <img src={g} alt={`${project.name} ${i + 1}`} className="w-full h-full object-cover" loading="lazy" />
                </button>
              ))}
            </div>
          </div>
        </section>
      )}

      <Dialog open={Boolean(selectedImage)} onOpenChange={(open) => !open && setSelectedImage(null)}>
        <DialogContent className="p-1 sm:max-w-6xl bg-transparent border-0 shadow-none">
          {selectedImage ? (
            <img
              src={selectedImage}
              alt={project.name}
              className="w-full max-h-[85vh] object-contain rounded-xl"
            />
          ) : null}
        </DialogContent>
      </Dialog>

      {/* Related */}
      <section className="py-20 bg-background">
        <div className="container-custom">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6 mb-10">
            <h2 className="font-display text-3xl md:text-4xl font-extrabold tracking-tight">{t("projDetail.related")}</h2>
            <Link to="/projects" className="btn-pill bg-secondary border border-border px-5 py-2.5 text-xs uppercase tracking-wider hover:bg-foreground hover:text-background">
              {t("common.viewAll")} <ArrowUpRight className="w-4 h-4" />
            </Link>
          </div>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
            {related.map((p) => (
              <Link key={p.id} to={`/projects/${p.slug}`} className="group card-hover bg-card rounded-3xl overflow-hidden border border-border">
                <div className="image-zoom aspect-[4/3] bg-muted">
                  <img src={p.imageUrl} alt={p.name} loading="lazy" className="w-full h-full object-cover" />
                </div>
                <div className="p-5">
                  <h3 className="font-display text-lg font-extrabold group-hover:text-primary transition-colors">{p.name}</h3>
                  <p className="text-sm text-muted-foreground mt-1">{p.location}</p>
                </div>
              </Link>
            ))}
          </div>
        </div>
      </section>
    </Layout>
  );
};

export default ProjectDetail;
