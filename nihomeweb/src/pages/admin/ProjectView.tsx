import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Edit, Trash2, MapPin, Maximize2, Calendar, Building, Tag } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { useToast } from "@/hooks/use-toast";
import { useProject } from "@/hooks/useContentApi";
import { adminApi } from "@/services/adminApi";
import { PageLoading, PageError } from "@/components/PageState";

const ProjectView = () => {
  const { slug } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const { data: project, loading, error, refetch } = useProject(slug ?? "");

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={refetch} /></AdminLayout>;

  if (!project) {
    return (
      <AdminLayout>
        <div className="admin-card p-10 text-center">
          <p className="text-sm" style={{ color: "hsl(var(--admin-muted))" }}>Không tìm thấy dự án.</p>
          <Link to="/admin/projects" className="admin-btn-primary inline-flex items-center gap-2 px-5 py-2.5 text-sm mt-4">
            <ArrowLeft className="w-4 h-4" /> {t("form.back")}
          </Link>
        </div>
      </AdminLayout>
    );
  }

  const handleDelete = async () => {
    if (!confirm(t("form.confirmDelete"))) return;
    try {
      await adminApi.deleteProject(project.id);
      toast({ title: t("form.deleted"), description: project.name });
      navigate("/admin/projects");
    } catch {
      toast({ title: t("common.error"), variant: "destructive" });
    }
  };

  return (
    <AdminLayout>
      <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4 mb-6">
        <div className="flex items-center gap-3">
          <Link
            to="/admin/projects"
            className="w-10 h-10 rounded-full bg-white border flex items-center justify-center hover:bg-muted transition"
            style={{ borderColor: "hsl(var(--admin-border))" }}
          >
            <ArrowLeft className="w-4 h-4" />
          </Link>
          <div>
            <p className="text-xs uppercase tracking-wider font-bold" style={{ color: "hsl(var(--admin-primary))" }}>
              {t("proj.detail")}
            </p>
            <h1 className="font-display text-2xl lg:text-3xl font-extrabold tracking-tight">{project.name}</h1>
          </div>
        </div>
        <div className="flex gap-2">
          <Link
            to={`/admin/projects/${project.slug}/edit`}
            className="inline-flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-bold border bg-white hover:bg-muted transition"
            style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-primary))" }}
          >
            <Edit className="w-4 h-4" /> {t("common.edit")}
          </Link>
          <button
            onClick={handleDelete}
            className="inline-flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-bold border bg-white hover:bg-muted transition"
            style={{ borderColor: "hsl(var(--admin-border))", color: "hsl(var(--admin-danger))" }}
          >
            <Trash2 className="w-4 h-4" /> {t("common.delete")}
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
        <div className="lg:col-span-2 space-y-5">
          <div className="admin-card overflow-hidden">
            <div className="aspect-[16/9] bg-muted">
              <img src={project.imageUrl} alt={project.name} className="w-full h-full object-cover" />
            </div>
          </div>

          {project.description && (
            <div className="admin-card p-6">
              <h2 className="font-bold mb-3">{t("proj.field.description")}</h2>
              <p className="text-sm leading-relaxed" style={{ color: "hsl(var(--admin-sidebar-text))" }}>
                {project.description}
              </p>
            </div>
          )}

          {(project.challenges?.length || project.solutions?.length) ? (
            <div className="grid md:grid-cols-2 gap-5">
              {project.challenges?.length ? (
                <div className="admin-card p-6">
                  <h2 className="font-bold mb-3">{t("proj.field.challenges").split(" (")[0]}</h2>
                  <ul className="space-y-2 text-sm list-disc list-inside" style={{ color: "hsl(var(--admin-sidebar-text))" }}>
                    {project.challenges.map((c, i) => <li key={i}>{c}</li>)}
                  </ul>
                </div>
              ) : null}
              {project.solutions?.length ? (
                <div className="admin-card p-6">
                  <h2 className="font-bold mb-3">{t("proj.field.solutions").split(" (")[0]}</h2>
                  <ul className="space-y-2 text-sm list-disc list-inside" style={{ color: "hsl(var(--admin-sidebar-text))" }}>
                    {project.solutions.map((c, i) => <li key={i}>{c}</li>)}
                  </ul>
                </div>
              ) : null}
            </div>
          ) : null}
        </div>

        <div className="admin-card p-6 h-fit">
          <h2 className="font-bold mb-4">{t("form.basicInfo")}</h2>
          <div className="space-y-3 text-sm">
            <Info icon={Building} label={t("proj.field.client")} value={project.client} />
            <Info icon={MapPin} label={t("proj.field.location")} value={project.location} />
            <Info icon={Maximize2} label={t("proj.scale")} value={project.scale} />
            <Info icon={Tag} label={t("proj.field.scope")} value={project.scope} />
            <Info icon={Calendar} label={t("proj.field.year")} value={project.year ?? "—"} />
            <Info icon={Tag} label={t("proj.field.category")} value={project.category ?? "—"} />
            <div className="pt-3 border-t" style={{ borderColor: "hsl(var(--admin-border))" }}>
              <span
                className="admin-chip"
                style={
                  project.status === "ongoing"
                    ? { background: "hsl(var(--admin-warning-soft))", color: "hsl(var(--admin-warning))" }
                    : { background: "hsl(var(--admin-success-soft))", color: "hsl(var(--admin-success))" }
                }
              >
                {project.status === "ongoing" ? t("proj.ongoing") : t("proj.completed")}
              </span>
            </div>
          </div>
        </div>
      </div>
    </AdminLayout>
  );
};

const Info = ({ icon: Icon, label, value }: { icon: React.ComponentType<React.SVGProps<SVGSVGElement>>; label: string; value: string }) => (
  <div className="flex items-start gap-3">
    <Icon className="w-4 h-4 mt-0.5 shrink-0" style={{ color: "hsl(var(--admin-muted))" }} />
    <div className="min-w-0">
      <p className="text-[10px] uppercase tracking-wider font-bold" style={{ color: "hsl(var(--admin-muted))" }}>{label}</p>
      <p className="font-semibold break-words">{value}</p>
    </div>
  </div>
);

export default ProjectView;
