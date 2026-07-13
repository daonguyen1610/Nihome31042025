import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Edit, Trash2, MapPin, Maximize2, Calendar, Building, Tag } from "lucide-react";
import AdminLayout from "@/components/layout/AdminLayout";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useToast } from "@/hooks/use-toast";
import { useProject } from "@/hooks/useContentApi";
import { adminApi } from "@/services/adminApi";
import { PageLoading, PageError } from "@/components/PageState";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent } from "@/components/ui/dialog";

const ProjectView = () => {
  const { slug } = useParams();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { toast } = useToast();
  const [selectedImage, setSelectedImage] = useState<string | null>(null);
  const { data: project, loading, error, refetch } = useProject(slug ?? "");

  if (loading) return <AdminLayout><PageLoading /></AdminLayout>;
  if (error) return <AdminLayout><PageError message={error} onRetry={refetch} /></AdminLayout>;

  if (!project) {
    return (
      <AdminLayout>
        <div className="rounded-lg border bg-card p-10 text-center">
          <p className="text-sm text-muted-foreground">Không tìm thấy dự án.</p>
          <Button asChild className="mt-4">
            <Link to="/admin/projects">
              <ArrowLeft className="mr-1.5 h-4 w-4" /> {t("form.back")}
            </Link>
          </Button>
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
      <div className="space-y-4 p-4 sm:p-6">
        <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-3">
            <Button asChild variant="outline" size="icon" className="rounded-full shrink-0">
              <Link to="/admin/projects">
                <ArrowLeft className="h-4 w-4" />
              </Link>
            </Button>
            <div>
              <p className="text-xs font-medium uppercase tracking-wide text-primary">
                {t("proj.detail")}
              </p>
              <h1 className="text-2xl font-semibold tracking-tight lg:text-3xl">{project.name}</h1>
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <Button asChild variant="outline">
              <Link to={`/admin/projects/${project.slug}/edit`}>
                <Edit className="mr-1.5 h-4 w-4" /> {t("common.edit")}
              </Link>
            </Button>
            <Button variant="outline" onClick={handleDelete} className="text-destructive hover:text-destructive">
              <Trash2 className="mr-1.5 h-4 w-4" /> {t("common.delete")}
            </Button>
          </div>
        </header>

        <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
          <div className="lg:col-span-2 space-y-4">
            <div className="overflow-hidden rounded-lg border bg-card">
              <div className="aspect-[16/9] bg-muted">
                <img src={project.imageUrl} alt={project.name} className="w-full h-full object-cover" />
              </div>
            </div>

            {project.description && (
              <div className="rounded-lg border bg-card p-6">
                <h2 className="mb-3 text-base font-semibold">{t("proj.field.description")}</h2>
                <p className="text-sm leading-relaxed text-muted-foreground">
                  {project.description}
                </p>
              </div>
            )}

            {(project.challenges?.length || project.solutions?.length) ? (
              <div className="grid gap-4 md:grid-cols-2">
                {project.challenges?.length ? (
                  <div className="rounded-lg border bg-card p-6">
                    <h2 className="mb-3 text-base font-semibold">{t("proj.field.challenges").split(" (")[0]}</h2>
                    <ul className="list-inside list-disc space-y-2 text-sm text-muted-foreground">
                      {project.challenges.map((c, i) => <li key={i}>{c}</li>)}
                    </ul>
                  </div>
                ) : null}
                {project.solutions?.length ? (
                  <div className="rounded-lg border bg-card p-6">
                    <h2 className="mb-3 text-base font-semibold">{t("proj.field.solutions").split(" (")[0]}</h2>
                    <ul className="list-inside list-disc space-y-2 text-sm text-muted-foreground">
                      {project.solutions.map((c, i) => <li key={i}>{c}</li>)}
                    </ul>
                  </div>
                ) : null}
              </div>
            ) : null}

            {project.gallery && project.gallery.length > 0 ? (
              <div className="rounded-lg border bg-card p-6">
                <h2 className="mb-4 text-base font-semibold">{t("media.gallery.label")}</h2>
                <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
                  {project.gallery.map((imageUrl, index) => (
                    <button
                      key={`${imageUrl}-${index}`}
                      type="button"
                      onClick={() => setSelectedImage(imageUrl)}
                      className="group relative aspect-[4/3] overflow-hidden rounded-md border bg-muted text-left"
                    >
                      <img
                        src={imageUrl}
                        alt={`${project.name} ${index + 1}`}
                        className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
                        loading="lazy"
                      />
                      <div className="absolute inset-0 bg-black/0 transition-colors group-hover:bg-black/20" />
                    </button>
                  ))}
                </div>
              </div>
            ) : null}
          </div>

          <div className="h-fit rounded-lg border bg-card p-6">
            <h2 className="mb-4 text-base font-semibold">{t("form.basicInfo")}</h2>
            <div className="space-y-3 text-sm">
              <Info icon={Building} label={t("proj.field.client")} value={project.client} />
              <Info icon={MapPin} label={t("proj.field.location")} value={project.location} />
              <Info icon={Maximize2} label={t("proj.scale")} value={project.scale} />
              <Info icon={Tag} label={t("proj.field.scope")} value={project.scope} />
              <Info icon={Calendar} label={t("proj.field.year")} value={project.year ?? "—"} />
              <Info icon={Tag} label={t("proj.field.category")} value={project.category ?? "—"} />
              <div className="pt-3 border-t">
                <Badge
                  variant="outline"
                  className={cn(
                    "gap-1.5 whitespace-nowrap font-medium",
                    project.status === "ongoing"
                      ? "border-amber-200 bg-amber-50 text-amber-700"
                      : "border-green-300 bg-green-100 text-green-800",
                  )}
                >
                  <span
                    className={cn(
                      "h-1.5 w-1.5 rounded-full",
                      project.status === "ongoing" ? "bg-amber-500" : "bg-green-600",
                    )}
                  />
                  {project.status === "ongoing" ? t("proj.ongoing") : t("proj.completed")}
                </Badge>
              </div>
            </div>
          </div>
        </div>
      </div>

      <Dialog open={Boolean(selectedImage)} onOpenChange={(open) => !open && setSelectedImage(null)}>
        <DialogContent className="border-0 bg-transparent p-1 shadow-none sm:max-w-5xl">
          {selectedImage ? (
            <img
              src={selectedImage}
              alt={project.name}
              className="max-h-[82vh] w-full rounded-md object-contain"
            />
          ) : null}
        </DialogContent>
      </Dialog>
    </AdminLayout>
  );
};

const Info = ({ icon: Icon, label, value }: { icon: React.ComponentType<React.SVGProps<SVGSVGElement>>; label: string; value: string }) => (
  <div className="flex items-start gap-3">
    <Icon className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" />
    <div className="min-w-0">
      <p className="text-[10px] font-medium uppercase tracking-wide text-muted-foreground">{label}</p>
      <p className="font-medium break-words">{value}</p>
    </div>
  </div>
);

export default ProjectView;
